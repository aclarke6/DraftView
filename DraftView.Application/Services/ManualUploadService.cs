using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Services;

/// <summary>
/// Orchestrates manual chapter upload, edit, reorder, replace, and version history management.
/// All content-change operations snapshot the previous content as a ManualChapterVersion
/// before overwriting. Unit-of-work semantics are enforced via IUnitOfWork.SaveChangesAsync.
/// </summary>
public class ManualUploadService(
    IProjectRepository projectRepo,
    IManualChapterRepository chapterRepo,
    IManualChapterVersionRepository versionRepo,
    IAuthorNotificationRepository notificationRepo,
    IChapterFileParserResolver parserResolver,
    IUnitOfWork unitOfWork) : IManualUploadService
{
    private const int MaxChaptersPerProject = 250;

    /// <summary>
    /// Parses and stores a chapter from a file upload. Rejects if the project is not Manual.
    /// Enforces the 250 chapter-per-project limit. Emits an AuthorNotification on success.
    /// </summary>
    public async Task<ManualChapter> UploadChapterAsync(
        Guid projectId, Guid authorId, string title,
        Stream content, string fileName,
        CancellationToken ct = default)
    {
        var project = await RequireManualProjectAsync(projectId, ct);

        var count = await chapterRepo.CountByProjectAsync(projectId, authorId, ct);
        if (count >= MaxChaptersPerProject)
            throw new InvalidOperationException(
                $"A project may not exceed {MaxChaptersPerProject} chapters.");

        var ext     = Path.GetExtension(fileName);
        var parser  = parserResolver.Resolve(ext);
        var rawText = await parser.ParseAsync(content, ct);
        var chapter = ManualChapter.Create(authorId, projectId, title, count, rawText, fileName, DateTime.UtcNow);

        await chapterRepo.AddAsync(chapter, ct);
        await EmitChapterUploadedNotificationAsync(authorId, project.Name, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return chapter;
    }

    /// <summary>
    /// Stores a chapter contributed via cut/paste. OriginalFileName is null.
    /// Emits an AuthorNotification on success.
    /// </summary>
    public async Task<ManualChapter> UploadChapterFromPasteAsync(
        Guid projectId, Guid authorId, string title,
        string rawContent,
        CancellationToken ct = default)
    {
        var project = await RequireManualProjectAsync(projectId, ct);

        var count = await chapterRepo.CountByProjectAsync(projectId, authorId, ct);
        if (count >= MaxChaptersPerProject)
            throw new InvalidOperationException(
                $"A project may not exceed {MaxChaptersPerProject} chapters.");

        var chapter = ManualChapter.Create(authorId, projectId, title, count, rawContent, null, DateTime.UtcNow);

        await chapterRepo.AddAsync(chapter, ct);
        await EmitChapterUploadedNotificationAsync(authorId, project.Name, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return chapter;
    }

    /// <summary>
    /// Assigns new SortOrder values to all chapters in the project according to the supplied order.
    /// </summary>
    public async Task ReorderChaptersAsync(
        Guid projectId, Guid authorId,
        IReadOnlyList<Guid> orderedChapterIds,
        CancellationToken ct = default)
    {
        var chapters = await chapterRepo.GetByProjectAsync(projectId, authorId, ct);
        var lookup   = chapters.ToDictionary(c => c.Id);

        for (var i = 0; i < orderedChapterIds.Count; i++)
        {
            if (!lookup.TryGetValue(orderedChapterIds[i], out var chapter))
                continue;

            chapter.UpdateSortOrder(i);
            await chapterRepo.UpdateAsync(chapter, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Hard-deletes the specified chapter.
    /// </summary>
    public async Task DeleteChapterAsync(
        Guid chapterId, Guid authorId,
        CancellationToken ct = default)
    {
        await chapterRepo.DeleteAsync(chapterId, authorId, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Replaces a chapter's content from a new file, creating a version snapshot first.
    /// </summary>
    public async Task<ManualChapter> ReplaceChapterAsync(
        Guid chapterId, Guid authorId,
        Stream content, string fileName,
        CancellationToken ct = default)
    {
        var chapter     = await RequireChapterAsync(chapterId, authorId, ct);
        var nextVersion = await versionRepo.GetNextVersionNumberAsync(chapterId, ct);

        var snapshot = ManualChapterVersion.Create(
            chapterId, authorId, nextVersion,
            chapter.RawContent, SnapshotReason.Replace, DateTime.UtcNow);
        await versionRepo.AddAsync(snapshot, ct);

        var ext     = Path.GetExtension(fileName);
        var parser  = parserResolver.Resolve(ext);
        var rawText = await parser.ParseAsync(content, ct);

        chapter.UpdateContent(rawText);
        await chapterRepo.UpdateAsync(chapter, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return chapter;
    }

    /// <summary>
    /// Updates a chapter's content via inline edit, creating a version snapshot with
    /// reason InlineEdit before overwriting.
    /// </summary>
    public async Task<ManualChapter> EditChapterAsync(
        Guid chapterId, Guid authorId,
        string rawContent,
        CancellationToken ct = default)
    {
        var chapter     = await RequireChapterAsync(chapterId, authorId, ct);
        var nextVersion = await versionRepo.GetNextVersionNumberAsync(chapterId, ct);

        var snapshot = ManualChapterVersion.Create(
            chapterId, authorId, nextVersion,
            chapter.RawContent, SnapshotReason.InlineEdit, DateTime.UtcNow);
        await versionRepo.AddAsync(snapshot, ct);

        chapter.UpdateContent(rawContent);
        await chapterRepo.UpdateAsync(chapter, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return chapter;
    }

    /// <summary>
    /// Hard-deletes all ManualChapterVersion records for the chapter.
    /// The live chapter content is not affected.
    /// </summary>
    public async Task ClearVersionHistoryAsync(
        Guid chapterId, Guid authorId,
        CancellationToken ct = default)
    {
        await RequireChapterAsync(chapterId, authorId, ct);
        await versionRepo.ClearForChapterAsync(chapterId, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private async Task<Project> RequireManualProjectAsync(Guid projectId, CancellationToken ct)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new EntityNotFoundException(nameof(Project), projectId);

        if (project.ProjectType != ProjectType.Manual)
            throw new UnauthorisedOperationException(
                "Manual chapter uploads are only permitted for Manual projects.");

        return project;
    }

    private async Task<ManualChapter> RequireChapterAsync(
        Guid chapterId, Guid authorId, CancellationToken ct) =>
        await chapterRepo.GetByIdAsync(chapterId, authorId, ct)
            ?? throw new EntityNotFoundException(nameof(ManualChapter), chapterId);

    private async Task EmitChapterUploadedNotificationAsync(
        Guid authorId, string projectName, CancellationToken ct)
    {
        var notification = AuthorNotification.Create(
            authorId,
            NotificationEventType.ChapterUploaded,
            $"Chapter uploaded to \"{projectName}\"",
            null,
            null,
            DateTime.UtcNow);

        await notificationRepo.AddAsync(notification, ct);
    }
}
