using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using Microsoft.Extensions.Logging;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Services;

#pragma warning disable CS9113 // clientFactory used internally by DropboxFileDownloader
public class ScrivenerSyncService(
    IProjectRepository projectRepo,
    ISectionRepository sectionRepo,
    IUnitOfWork unitOfWork,
    IScrivenerProjectParser parser,
    IRtfConverter converter,
    ILocalPathResolver pathResolver,
    ISyncProgressTracker progressTracker,
    IDropboxConnectionChecker connectionChecker,
    IDropboxClientFactory clientFactory,
    IDropboxFileDownloader fileDownloader,
    ILogger<ScrivenerSyncService> logger,
    IAuthorNotificationRepository notificationRepo,
    IUserRepository userRepo) : ISyncService
{
    public async Task ParseProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new EntityNotFoundException(nameof(Project), projectId);

        // Scope all per-author services to this project's author
        connectionChecker.SetUserId(project.AuthorId);
        pathResolver.SetUserId(project.AuthorId);

        if (!await connectionChecker.IsConnectedAsync(ct))
        {
            if (project.SyncStatus != SyncStatus.Stale)
            {
                project.UpdateSyncStatus(SyncStatus.Stale, DateTime.UtcNow,
                    "Dropbox not connected. Connect your Dropbox account to enable sync.");
                await unitOfWork.SaveChangesAsync(ct);
            }
            return;
        }

        try
        {
            // Download latest files from Dropbox into per-author cache
            await fileDownloader.DownloadProjectAsync(project, project.AuthorId, ct);

            var scrivxPath = await pathResolver.ResolveScrivxAsync(project, ct);
            var parsed     = parser.Parse(scrivxPath);
            var localPath  = await pathResolver.ResolveAsync(project, ct);

            if (parsed.ManuscriptRoot is null)
            {
                project.UpdateSyncStatus(SyncStatus.Error, DateTime.UtcNow,
                    "No DraftFolder found in project.scrivx.");
                await unitOfWork.SaveChangesAsync(ct);
                return;
            }

            var existingSections = await sectionRepo.GetByProjectIdAsync(projectId, ct);
            var seenUuids        = new HashSet<string>();

            var rootNode = parsed.ManuscriptRoot;
            if (!string.IsNullOrWhiteSpace(project.SyncRootId))
            {
                var found = FindNodeByUuid(parsed.ManuscriptRoot, project.SyncRootId);
                if (found is not null)
                    rootNode = found;
            }

            await ReconcileNodeAsync(rootNode, null, projectId, localPath, seenUuids, ct);

            foreach (var section in existingSections)
            {
                if (!seenUuids.Contains(section.ScrivenerUuid) && !section.IsSoftDeleted)
                {
                    var descendants = await sectionRepo.GetAllDescendantsAsync(section.Id, ct);
                    foreach (var descendant in descendants)
                        descendant.SoftDelete();
                    section.SoftDelete();
                }
            }

            var author = await userRepo.GetAuthorAsync(ct);
            if (author is not null)
            {
                var notification = AuthorNotification.Create(
                    author.Id,
                    NotificationEventType.SyncCompleted,
                    $"Sync completed for {project.Name}",
                    null,
                    null,
                    DateTime.UtcNow);
                await notificationRepo.AddAsync(notification, ct);
            }

            project.UpdateSyncStatus(SyncStatus.Healthy, DateTime.UtcNow, null);
            progressTracker.Clear(projectId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync failed for project {ProjectId}: {Message}", projectId, ex.Message);
            project.UpdateSyncStatus(SyncStatus.Error, DateTime.UtcNow, ex.Message);
            progressTracker.Clear(projectId);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DetectContentChangesAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new EntityNotFoundException(nameof(Project), projectId);

        connectionChecker.SetUserId(project.AuthorId);
        pathResolver.SetUserId(project.AuthorId);

        if (!await connectionChecker.IsConnectedAsync(ct))
            return;

        var localPath         = await pathResolver.ResolveAsync(project, ct);
        var publishedSections = await sectionRepo.GetPublishedByProjectIdAsync(projectId, ct);

        foreach (var section in publishedSections)
        {
            if (section.NodeType != NodeType.Document) continue;

            var result = await converter.ConvertAsync(localPath, section.ScrivenerUuid, ct);
            if (result is null) continue;

            if (result.Hash != section.ContentHash)
            {
                section.UpdateContent(result.Html, result.Hash);
                section.MarkContentChanged();
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ReconcileNodeAsync(
        ParsedBinderNode node, Guid? parentId, Guid projectId,
        string scrivFolderPath, HashSet<string> seenUuids, CancellationToken ct)
    {
        seenUuids.Add(node.Uuid);

        var existing = await sectionRepo.GetByScrivenerUuidAsync(projectId, node.Uuid, ct);

        if (existing is null)
        {
            existing = await CreateSectionAsync(node, parentId, projectId, scrivFolderPath, ct);
            await sectionRepo.AddAsync(existing, ct);
        }
        else
        {
            await UpdateSectionAsync(existing, node, scrivFolderPath, ct);
        }

        foreach (var child in node.Children)
            await ReconcileNodeAsync(child, existing.Id, projectId, scrivFolderPath, seenUuids, ct);
    }

    private async Task<Section> CreateSectionAsync(
        ParsedBinderNode node, Guid? parentId, Guid projectId,
        string scrivFolderPath, CancellationToken ct)
    {
        if (node.NodeType == ParsedNodeType.Folder)
            return Section.CreateFolder(projectId, node.Uuid, node.Title, parentId, node.SortOrder);

        var rtf = await converter.ConvertAsync(scrivFolderPath, node.Uuid, ct);
        return Section.CreateDocument(projectId, node.Uuid, node.Title, parentId,
            node.SortOrder, rtf?.Html, rtf?.Hash, node.ScrivenerStatus);
    }

    private async Task UpdateSectionAsync(
        Section existing, ParsedBinderNode node,
        string scrivFolderPath, CancellationToken ct)
    {
        existing.UpdateTitle(node.Title);
        existing.UpdateSortOrder(node.SortOrder);
        existing.UpdateScrivenerStatus(node.ScrivenerStatus);

        if (node.NodeType == ParsedNodeType.Document)
        {
            var rtf = await converter.ConvertAsync(scrivFolderPath, node.Uuid, ct);
            if (rtf is not null && rtf.Hash != existing.ContentHash)
            {
                existing.UpdateContent(rtf.Html, rtf.Hash);
                if (existing.IsPublished)
                    existing.MarkContentChanged();
            }
        }
    }

    private static ParsedBinderNode? FindNodeByUuid(ParsedBinderNode node, string uuid)
    {
        if (node.Uuid == uuid)
            return node;
        foreach (var child in node.Children)
        {
            var found = FindNodeByUuid(child, uuid);
            if (found is not null)
                return found;
        }
        return null;
    }
}

