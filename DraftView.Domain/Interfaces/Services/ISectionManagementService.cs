using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// One row of a depth-first sorted section tree, annotated with its
/// indentation depth for tree rendering.
/// </summary>
public sealed record SectionTreeRow(Section Section, int Depth);

/// <summary>
/// Aggregated view of a project's section tree for the author's Sections
/// page: depth-first ordering, which chapter folders are currently
/// publishable, which published chapters have unpublished changes, and the
/// advisory change classification for chapters that have changes.
/// </summary>
public sealed class SectionsSummaryDto
{
    public required Project Project { get; init; }
    public required IReadOnlyList<SectionTreeRow> SortedSections { get; init; }
    public required IReadOnlySet<Guid> Publishable { get; init; }
    public required IReadOnlySet<Guid> ChapterHasChanges { get; init; }
    public required IReadOnlyDictionary<Guid, ChangeClassification> ClassificationMap { get; init; }
}

/// <summary>
/// All data required to render the author's section detail page, including
/// the section itself, its parent chapter title, comments with author names,
/// and per-reader snapshot-based read state.
/// </summary>
public sealed class SectionDetailDto
{
    public required Section Section { get; init; }
    public string? ChapterTitle { get; init; }
    public required IReadOnlyList<Comment> Comments { get; init; }
    public required IReadOnlyDictionary<Guid, string> CommentAuthorNames { get; init; }

    /// <summary>Readers whose stored snapshot matches the current HtmlContent — up to date.</summary>
    public required int ReadCurrentCount { get; init; }

    /// <summary>Readers who have opened the scene but whose snapshot does not match current content.</summary>
    public required int NotReadCurrentCount { get; init; }

    /// <summary>Display names of readers on the current content version.</summary>
    public required IReadOnlyList<string> ReadCurrentNames { get; init; }

    /// <summary>Display names of readers not yet on the current content version.</summary>
    public required IReadOnlyList<string> NotReadCurrentNames { get; init; }
}

public interface ISectionManagementService
{
    /// <summary>
    /// Returns the full section tree for a project along with publishability
    /// and change-classification metadata used to render the author's
    /// Sections page. Returns null when the project does not exist.
    /// </summary>
    Task<SectionsSummaryDto?> GetSectionsSummaryAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Returns the section detail including comments, comment author names,
    /// and read count for the author's section view. Returns null when the
    /// section does not exist.
    /// </summary>
    Task<SectionDetailDto?> GetSectionDetailAsync(
        Guid sectionId, Guid authorId, CancellationToken ct = default);
}
