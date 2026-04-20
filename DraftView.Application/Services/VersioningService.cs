using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Policies;
using Microsoft.Extensions.Configuration;

namespace DraftView.Application.Services;

/// <summary>
/// Creates SectionVersion snapshots when the author publishes a chapter.
/// This is the only service permitted to create SectionVersion records.
/// Sync and import workflows never call this service.
/// </summary>
public class VersioningService(
    ISectionRepository sectionRepository,
    ISectionVersionRepository sectionVersionRepository,
    IHtmlDiffService htmlDiffService,
    IChangeClassificationService changeClassificationService,
    IAiSummaryService aiSummaryService,
    IUnitOfWork unitOfWork,
    IConfiguration configuration) : IVersioningService
{
    /// <summary>
    /// Creates a SectionVersion for each non-soft-deleted Document descendant
    /// of the given chapter. Sets Section.IsPublished = true on each versioned section.
    /// Throws if the chapter does not exist, is not a Folder, or has no publishable
    /// Document descendants.
    /// </summary>
    public async Task RepublishChapterAsync(Guid chapterId, Guid authorId, CancellationToken ct = default)
    {
        var chapter = await sectionRepository.GetByIdAsync(chapterId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), chapterId);

        EnsureFolderChapter(chapter);
        AssertChapterNotLocked(chapter);

        var descendants = await sectionRepository.GetAllDescendantsAsync(chapterId, ct);
        var publishableDocuments = descendants
            .Where(s => s.NodeType == NodeType.Document &&
                       !s.IsSoftDeleted &&
                       !string.IsNullOrEmpty(s.HtmlContent))
            .ToList();

        if (publishableDocuments.Count == 0)
            throw new InvariantViolationException("I-VER-NO-DOCS",
                "Chapter has no publishable Document sections.");

        var subscriptionTier = GetSubscriptionTier();

        foreach (var document in publishableDocuments)
        {
            var maxVersion = await sectionVersionRepository.GetMaxVersionNumberAsync(document.Id, ct);
            var shouldCreateVersion = document.ContentChangedSincePublish || maxVersion == 0;

            if (!shouldCreateVersion)
                continue;

            await CreateVersionForDocumentAsync(document, authorId, subscriptionTier, maxVersion, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Creates a SectionVersion for a single Document section.
    /// </summary>
    public async Task RepublishSectionAsync(Guid sectionId, Guid authorId, CancellationToken ct = default)
    {
        var section = await sectionRepository.GetByIdAsync(sectionId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), sectionId);

        EnsureDocumentPublishable(section);
        await AssertParentChapterNotLockedAsync(section, ct);

        var subscriptionTier = GetSubscriptionTier();
        await CreateVersionForDocumentAsync(section, authorId, subscriptionTier, null, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Revokes the latest SectionVersion for a single Document section.
    /// </summary>
    public async Task RevokeLatestVersionAsync(Guid sectionId, Guid authorId, CancellationToken ct = default)
    {
        var section = await sectionRepository.GetByIdAsync(sectionId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), sectionId);

        EnsureDocumentRevokable(section);

        var versions = await sectionVersionRepository.GetAllBySectionIdAsync(sectionId, ct);
        if (versions.Count == 0)
            throw new InvariantViolationException("I-VER-REVOKE-NONE", "No versions exist to revoke.");

        if (versions.Count == 1)
            throw new InvariantViolationException("I-VER-REVOKE-LAST", "Cannot revoke the only version. Use Unpublish instead.");

        var latestVersion = versions
            .OrderByDescending(v => v.VersionNumber)
            .First();

        await sectionVersionRepository.DeleteAsync(latestVersion.Id, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Locks a chapter to block publish actions.
    /// </summary>
    public async Task LockChapterAsync(Guid chapterId, Guid authorId, CancellationToken ct = default)
    {
        var chapter = await sectionRepository.GetByIdAsync(chapterId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), chapterId);

        EnsureLockableChapter(chapter);
        chapter.Lock();
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Unlocks a chapter to re-enable publish actions.
    /// </summary>
    public async Task UnlockChapterAsync(Guid chapterId, Guid authorId, CancellationToken ct = default)
    {
        var chapter = await sectionRepository.GetByIdAsync(chapterId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), chapterId);

        EnsureLockableChapter(chapter);
        chapter.Unlock();
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sets a suggested publish date for a chapter.
    /// </summary>
    public Task ScheduleChapterAsync(Guid chapterId, Guid authorId, DateTime scheduledAt,
        CancellationToken ct = default)
    {
        return ScheduleChapterInternalAsync(chapterId, scheduledAt, ct);
    }

    /// <summary>
    /// Clears a chapter's suggested publish date.
    /// </summary>
    public Task ClearScheduleAsync(Guid chapterId, Guid authorId,
        CancellationToken ct = default)
    {
        return ClearChapterScheduleInternalAsync(chapterId, ct);
    }

    private async Task ScheduleChapterInternalAsync(Guid chapterId, DateTime scheduledAt, CancellationToken ct)
    {
        var chapter = await LoadAndValidateChapterAsync(chapterId, ct);
        chapter.SchedulePublish(scheduledAt);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ClearChapterScheduleInternalAsync(Guid chapterId, CancellationToken ct)
    {
        var chapter = await LoadAndValidateChapterAsync(chapterId, ct);
        chapter.ClearSchedule();
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Creates, classifies, summarizes, and persists a new version for one document.
    /// </summary>
    private async Task CreateVersionForDocumentAsync(
        Section document,
        Guid authorId,
        SubscriptionTier subscriptionTier,
        int? maxVersion,
        CancellationToken ct)
    {
        await AssertWithinRetentionLimitAsync(document.Id, subscriptionTier, ct);

        var currentMaxVersion = maxVersion ?? await sectionVersionRepository.GetMaxVersionNumberAsync(document.Id, ct);
        var nextVersion = currentMaxVersion + 1;
        var version = SectionVersion.Create(document, authorId, nextVersion);

        var allVersions = await sectionVersionRepository.GetAllBySectionIdAsync(document.Id, ct);
        var previousVersion = allVersions
            .Where(v => v.VersionNumber < version.VersionNumber)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        if (previousVersion is not null)
            TryApplyClassification(version, previousVersion);

        var summary = await aiSummaryService.GenerateSummaryAsync(
            previousVersion?.HtmlContent,
            document.HtmlContent ?? string.Empty,
            ct);

        if (summary is not null)
            version.SetAiSummary(summary);

        await sectionVersionRepository.AddAsync(version, ct);
        document.PublishAsPartOfChapter(document.ContentHash ?? string.Empty);
    }

    private async Task AssertWithinRetentionLimitAsync(Guid sectionId, SubscriptionTier tier, CancellationToken ct)
    {
        var versionCount = await sectionVersionRepository.GetVersionCountAsync(sectionId, ct);

        if (VersionRetentionPolicy.IsAtLimit(versionCount, tier))
            throw new VersionRetentionLimitException(VersionRetentionPolicy.GetLimit(tier));
    }

    private SubscriptionTier GetSubscriptionTier()
    {
        var raw = configuration["DraftView:SubscriptionTier"];
        return Enum.TryParse<SubscriptionTier>(raw, ignoreCase: true, out var tier)
            ? tier
            : SubscriptionTier.Free;
    }

    private void TryApplyClassification(SectionVersion version, SectionVersion previousVersion)
    {
        try
        {
            var diffParagraphs = htmlDiffService.Compute(previousVersion.HtmlContent, version.HtmlContent);
            var classification = changeClassificationService.Classify(diffParagraphs);
            if (classification.HasValue)
                version.SetChangeClassification(classification.Value);
        }
        catch
        {
            // Classification is advisory and must not block republish.
        }
    }

    private static void EnsureFolderChapter(Section chapter)
    {
        if (chapter.NodeType != NodeType.Folder)
            throw new InvariantViolationException("I-VER-CHAPTER",
                "Only Folder sections can be republished. Document sections cannot be republished directly.");
    }

    private static void EnsureLockableChapter(Section chapter)
    {
        if (chapter.NodeType != NodeType.Folder)
            throw new InvariantViolationException("I-LOCK-NOT-CHAPTER", "Only Folder sections can be locked or unlocked.");
    }

    private static void EnsureSchedulableChapter(Section chapter)
    {
        if (chapter.NodeType != NodeType.Folder)
            throw new InvariantViolationException("I-SCHED-NOT-CHAPTER", "Only Folder sections can be scheduled.");
    }

    private async Task<Section> LoadAndValidateChapterAsync(Guid chapterId, CancellationToken ct)
    {
        var chapter = await sectionRepository.GetByIdAsync(chapterId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), chapterId);

        EnsureSchedulableChapter(chapter);
        return chapter;
    }

    private static void AssertChapterNotLocked(Section chapter)
    {
        if (chapter.IsLocked)
            throw new InvariantViolationException("I-LOCK-BLOCKED",
                "Cannot republish a locked chapter. Unlock the chapter first.");
    }

    private async Task AssertParentChapterNotLockedAsync(Section section, CancellationToken ct)
    {
        if (!section.ParentId.HasValue)
            return;

        var parentChapter = await sectionRepository.GetByIdAsync(section.ParentId.Value, ct);
        if (parentChapter is { IsLocked: true })
            throw new InvariantViolationException("I-LOCK-BLOCKED",
                "Cannot republish a document in a locked chapter. Unlock the chapter first.");
    }

    private static void EnsureDocumentPublishable(Section section)
    {
        if (section.NodeType != NodeType.Document)
            throw new InvariantViolationException("I-VER-SECTION", "Only Document sections can be republished directly.");

        if (section.IsSoftDeleted)
            throw new InvariantViolationException("I-VER-SECTION-DELETED", "Soft-deleted sections cannot be republished.");

        if (string.IsNullOrWhiteSpace(section.HtmlContent))
            throw new InvariantViolationException("I-VER-SECTION-EMPTY", "Document section must have HtmlContent before republish.");
    }

    private static void EnsureDocumentRevokable(Section section)
    {
        if (section.NodeType != NodeType.Document)
            throw new InvariantViolationException("I-VER-REVOKE-TYPE", "Only Document sections can revoke versions.");

        if (section.IsSoftDeleted)
            throw new InvariantViolationException("I-VER-REVOKE-DELETED", "Soft-deleted sections cannot revoke versions.");
    }
}
