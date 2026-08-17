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

public interface ISectionManagementService
{
    /// <summary>
    /// Returns the full section tree for a project along with publishability
    /// and change-classification metadata used to render the author's
    /// Sections page. Returns null when the project does not exist.
    /// </summary>
    Task<SectionsSummaryDto?> GetSectionsSummaryAsync(
        Guid projectId, CancellationToken ct = default);
}
