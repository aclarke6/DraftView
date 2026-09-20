using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using Microsoft.Extensions.Logging;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Services;

/// <summary>
/// Implements <see cref="ISyncService"/> for Scrivener projects synced via Dropbox.
/// Reconciles the live Scrivener binder tree against the database section records,
/// downloads changed file entries, and emits a SyncCompleted notification on success.
/// </summary>
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
    IUserRepository userRepo,
    IChangeNotificationService changeNotificationService) : ISyncService
{
    private Guid _currentAuthorId;
    private readonly HashSet<Guid> _contentChangedPublishedSectionIds = [];

    /// <summary>
    /// Runs a full sync cycle for the given project: checks Dropbox connectivity,
    /// performs full or incremental file listing, reconciles the binder tree, and
    /// emits a SyncCompleted notification. Updates the project SyncStatus on completion
    /// or failure and sends change notifications for any published sections whose
    /// content changed.
    /// </summary>
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

        _currentAuthorId = project.AuthorId;
        _contentChangedPublishedSectionIds.Clear();

        try
        {
            int fileCount;
            if (string.IsNullOrWhiteSpace(project.DropboxCursor))
                fileCount = await SyncUsingFullListingAsync(project, ct);
            else
                fileCount = await SyncUsingIncrementalListingAsync(project, ct);

            var author = await userRepo.GetAuthorAsync(ct);
            if (author is not null)
            {
                var fileWord = fileCount == 1 ? "file" : "files";
                var notification = AuthorNotification.Create(
                    author.Id,
                    NotificationEventType.SyncCompleted,
                    $"Sync completed for {project.Name} — {fileCount} {fileWord} transferred",
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

        foreach (var sectionId in _contentChangedPublishedSectionIds)
        {
            try { await changeNotificationService.SendChangeNotificationsAsync(sectionId, ct); }
            catch (Exception ex) { logger.LogError(ex, "Change notification failed for section {SectionId}", sectionId); }
        }
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
                if (!section.IsPublished)
                    section.MarkContentChanged();
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ReconcileNodeAsync(
        ParsedBinderNode node, Guid? parentId, Section? parentSection, Guid projectId,
        string scrivFolderPath, HashSet<string> seenUuids, CancellationToken ct)
    {
        seenUuids.Add(node.Uuid);

        var safeTitle = string.IsNullOrWhiteSpace(node.Title) ? "Untitled" : node.Title;
        if (safeTitle == "Untitled")
            logger.LogWarning(
                "Section {Uuid} in project {ProjectId} has a blank title; substituting 'Untitled'.",
                node.Uuid, projectId);

        var existing = await sectionRepo.GetByScrivenerUuidAsync(projectId, node.Uuid, ct);
        var created = false;

        if (existing is null)
        {
            existing = await CreateSectionAsync(node, safeTitle, parentId, projectId, scrivFolderPath, ct);
            await sectionRepo.AddAsync(existing, ct);
            created = true;

            if (created && node.NodeType == ParsedNodeType.Document)
            {
                if (parentSection is not null && parentSection.NodeType == NodeType.Folder && parentSection.IsPublished)
                    parentSection.MarkContentChanged();
            }
        }
        else
        {
            await UpdateSectionAsync(existing, node, safeTitle, parentId, scrivFolderPath, ct);
        }

        foreach (var child in node.Children)
            await ReconcileNodeAsync(child, existing.Id, existing, projectId, scrivFolderPath, seenUuids, ct);
    }

    /// <summary>
    /// Performs a full Dropbox listing, downloads all entries, reconciles the binder,
    /// stores the initial cursor, and returns the number of entries transferred.
    /// Used on first sync or after a cursor reset.
    /// </summary>
    private async Task<int> SyncUsingFullListingAsync(Project project, CancellationToken ct)
    {
        var (entries, initialCursor) = await fileDownloader
            .ListAllEntriesWithCursorAsync(project.AuthorId, project.DropboxPath, ct);

        await fileDownloader.DownloadChangedEntriesAsync(project, project.AuthorId, entries, ct);
        await ReconcileProjectFromScrivxAsync(project, ct);

        project.UpdateDropboxCursor(initialCursor);

        logger.LogInformation(
            "Sync full listing processed {EntryCount} entries. Project {ProjectId} cursor set to {CursorPrefix}",
            entries.Count,
            project.Id,
            TruncateCursor(initialCursor));

        return entries.Count;
    }

    /// <summary>
    /// Fetches only changed Dropbox entries since the last cursor, processes them,
    /// reconciles the binder, advances the cursor, and returns the entry count.
    /// Falls back to <see cref="SyncUsingFullListingAsync"/> if the cursor has expired.
    /// </summary>
    private async Task<int> SyncUsingIncrementalListingAsync(Project project, CancellationToken ct)
    {
        try
        {
            var (entries, newCursor) = await fileDownloader
                .ListChangedEntriesAsync(project.AuthorId, project.DropboxCursor!, ct);

            await ProcessSyncEntriesAsync(project, entries, ct);
            await ReconcileProjectFromScrivxAsync(project, ct);
            project.UpdateDropboxCursor(newCursor);

            logger.LogInformation(
                "Sync incremental listing processed {EntryCount} entries. Project {ProjectId} cursor set to {CursorPrefix}",
                entries.Count,
                project.Id,
                TruncateCursor(newCursor));

            return entries.Count;
        }
        catch (Exception ex) when (IsResetCursorError(ex))
        {
            logger.LogWarning(ex,
                "Dropbox cursor expired for project {ProjectId}. Falling back to full listing.",
                project.Id);

            project.ClearDropboxCursor();
            return await SyncUsingFullListingAsync(project, ct);
        }
    }

    private async Task ProcessSyncEntriesAsync(Project project, IReadOnlyList<DropboxChangedEntry> entries, CancellationToken ct)
    {
        await fileDownloader.DownloadChangedEntriesAsync(project, project.AuthorId, entries, ct);
        var localPath = await pathResolver.ResolveAsync(project, ct);

        foreach (var entry in entries)
        {
            var uuid = TryExtractSectionUuid(entry.Path);
            if (string.IsNullOrWhiteSpace(uuid))
                continue;

            var section = await sectionRepo.GetByScrivenerUuidAsync(project.Id, uuid, ct);
            if (section is null)
                continue;

            if (entry.EntryType == DropboxEntryType.Deleted)
            {
                await SoftDeleteSectionAsync(section, ct);
                continue;
            }

            if (section.NodeType != NodeType.Document)
                continue;

            var rtf = await converter.ConvertAsync(localPath, uuid, ct);
            if (rtf is not null && rtf.Hash != section.ContentHash)
            {
                section.UpdateContent(rtf.Html, rtf.Hash);
            }
        }
    }

    private async Task ReconcileProjectFromScrivxAsync(Project project, CancellationToken ct)
    {
        var scrivxPath = await pathResolver.ResolveScrivxAsync(project, ct);
        var parsed = parser.Parse(scrivxPath);
        var localPath = await pathResolver.ResolveAsync(project, ct);

        if (parsed.ManuscriptRoot is null)
        {
            project.UpdateSyncStatus(SyncStatus.Error, DateTime.UtcNow,
                "No DraftFolder found in project.scrivx.");
            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        var existingSections = await sectionRepo.GetByProjectIdAsync(project.Id, ct);
        var seenUuids = new HashSet<string>();

        var rootNode = parsed.ManuscriptRoot;
        if (!string.IsNullOrWhiteSpace(project.SyncRootId))
        {
            var found = FindNodeByUuid(parsed.ManuscriptRoot, project.SyncRootId);
            if (found is not null)
                rootNode = found;
        }

        await ReconcileNodeAsync(rootNode, null, null, project.Id, localPath, seenUuids, ct);

        foreach (var section in existingSections)
        {
            if (seenUuids.Contains(section.ScrivenerUuid) || section.IsSoftDeleted)
                continue;

            await SoftDeleteSectionAsync(section, ct);
        }
    }

    private async Task SoftDeleteSectionAsync(Section section, CancellationToken ct)
    {
        if (!section.IsSoftDeleted)
        {
            var descendants = await sectionRepo.GetAllDescendantsAsync(section.Id, ct);
            foreach (var descendant in descendants)
                descendant.SoftDelete();

            section.SoftDelete();
        }
    }

    private static string? TryExtractSectionUuid(string path)
    {
        if (!path.EndsWith("content.rtf", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
            return null;

        return parts[^2];
    }

    private static bool IsResetCursorError(Exception ex) =>
        ex.Message.Contains("reset_cursor", StringComparison.OrdinalIgnoreCase) ||
        ex.InnerException?.Message.Contains("reset_cursor", StringComparison.OrdinalIgnoreCase) == true;

    private static string TruncateCursor(string cursor) =>
        cursor.Length <= 16 ? cursor : cursor[..16];

    private async Task<Section> CreateSectionAsync(
        ParsedBinderNode node, string safeTitle, Guid? parentId, Guid projectId,
        string scrivFolderPath, CancellationToken ct)
    {
        if (node.NodeType == ParsedNodeType.Folder)
            return Section.CreateFolder(projectId, node.Uuid, safeTitle, parentId, node.SortOrder);

        var rtf = await converter.ConvertAsync(scrivFolderPath, node.Uuid, ct);
        return Section.CreateDocument(projectId, node.Uuid, safeTitle, parentId,
            node.SortOrder, rtf?.Html, rtf?.Hash, node.ScrivenerStatus);
    }

    /// <summary>
    /// Applies binder-sourced field updates to an existing section: title, sort order,
    /// parent, Scrivener status, and — for Document nodes — content when the hash has changed.
    /// </summary>
    private async Task UpdateSectionAsync(
        Section existing, ParsedBinderNode node, string safeTitle,
        Guid? parentId, string scrivFolderPath, CancellationToken ct)
    {
        existing.UpdateTitle(safeTitle);
        existing.UpdateSortOrder(node.SortOrder);
        existing.UpdateParent(parentId);
        existing.UpdateScrivenerStatus(node.ScrivenerStatus);

        if (node.NodeType == ParsedNodeType.Document)
        {
            var rtf = await converter.ConvertAsync(scrivFolderPath, node.Uuid, ct);
            if (rtf is not null && rtf.Hash != existing.ContentHash)
            {
                existing.UpdateContent(rtf.Html, rtf.Hash);
                if (existing.IsPublished)
                    _contentChangedPublishedSectionIds.Add(existing.Id);
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

