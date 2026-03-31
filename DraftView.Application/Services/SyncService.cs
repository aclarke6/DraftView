using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class SyncService(
    IScrivenerProjectRepository projectRepo,
    ISectionRepository sectionRepo,
    IUnitOfWork unitOfWork,
    IScrivenerProjectParser parser,
    IRtfConverter converter,
    ILocalPathResolver pathResolver,
    ISyncProgressTracker progressTracker,
    IDropboxConnectionChecker connectionChecker) : ISyncService
{
    public async Task ParseProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new EntityNotFoundException(nameof(ScrivenerProject), projectId);

        if (!await connectionChecker.IsConnectedAsync(ct))
        {
            if (project.SyncStatus != SyncStatus.Stale)
            {
                project.UpdateSyncStatus(SyncStatus.Stale, DateTime.UtcNow,
                    "Dropbox not connected. Configure Dropbox to enable sync.");
                await unitOfWork.SaveChangesAsync(ct);
            }
            return;
        }

        try
        {
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
            if (!string.IsNullOrWhiteSpace(project.ScrivenerRootUuid))
            {
                var found = FindNodeByUuid(parsed.ManuscriptRoot, project.ScrivenerRootUuid);
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

            project.UpdateSyncStatus(SyncStatus.Healthy, DateTime.UtcNow, null);
            progressTracker.Clear(projectId);
        }
        catch (Exception ex)
        {
            project.UpdateSyncStatus(SyncStatus.Error, DateTime.UtcNow, ex.Message);
            progressTracker.Clear(projectId);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DetectContentChangesAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!await connectionChecker.IsConnectedAsync())
            return;

        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new EntityNotFoundException(nameof(ScrivenerProject), projectId);

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
