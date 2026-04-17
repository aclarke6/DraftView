using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Creates SectionVersion snapshots when the author publishes a chapter.
/// This is the only service permitted to create SectionVersion records.
/// Sync and import workflows never call this service.
/// </summary>
public class VersioningService(
    ISectionRepository sectionRepository,
    ISectionVersionRepository sectionVersionRepository,
    IUnitOfWork unitOfWork) : IVersioningService
{
    /// <summary>
    /// Creates a SectionVersion for each non-soft-deleted Document descendant
    /// of the given chapter. Sets Section.IsPublished = true on each versioned section.
    /// Throws if the chapter does not exist, is not a Folder, or has no publishable
    /// Document descendants.
    /// </summary>
    public async Task RepublishChapterAsync(Guid chapterId, Guid authorId, CancellationToken ct = default)
    {
        // Load chapter
        var chapter = await sectionRepository.GetByIdAsync(chapterId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), chapterId);

        // Validate chapter is a Folder
        if (chapter.NodeType != NodeType.Folder)
            throw new InvariantViolationException("I-VER-CHAPTER",
                "Only Folder sections can be republished. Document sections cannot be republished directly.");

        // Load all descendants
        var descendants = await sectionRepository.GetAllDescendantsAsync(chapterId, ct);

        // Filter to publishable documents
        var publishableDocuments = descendants
            .Where(s => s.NodeType == NodeType.Document &&
                       !s.IsSoftDeleted &&
                       !string.IsNullOrEmpty(s.HtmlContent))
            .ToList();

        // Validate at least one publishable document exists
        if (publishableDocuments.Count == 0)
            throw new InvariantViolationException("I-VER-NO-DOCS",
                "Chapter has no publishable Document sections.");

        // Create versions for each publishable document
        foreach (var document in publishableDocuments)
        {
            var maxVersion = await sectionVersionRepository.GetMaxVersionNumberAsync(document.Id, ct);
            var nextVersion = maxVersion + 1;

            var version = SectionVersion.Create(document, authorId, nextVersion);
            await sectionVersionRepository.AddAsync(version, ct);

            document.PublishAsPartOfChapter(document.ContentHash ?? string.Empty);
        }

        // Save all changes once
        await unitOfWork.SaveChangesAsync(ct);
    }
}
