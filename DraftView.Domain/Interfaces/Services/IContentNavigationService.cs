using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>One entry in a document's version history, shown when the retention limit is reached.</summary>
public sealed record VersionHistoryData(
    Guid VersionId,
    int VersionNumber,
    string? VersionLabel,
    DateTime CreatedAt,
    ChangeClassification? Classification,
    bool CanDelete);

/// <summary>Data for a single document section on the Publishing page.</summary>
public sealed record PublishingDocumentData(
    Section Document,
    int? CurrentVersionNumber,
    string? CurrentVersionLabel,
    bool HasChanges,
    ChangeClassification? Classification,
    bool CanRevoke,
    IReadOnlyList<VersionHistoryData> VersionHistory,
    bool ShowVersionHistory,
    int RetentionLimit);

/// <summary>Data for a published leaf chapter and its document sections on the Publishing page.</summary>
public sealed record PublishingChapterData(
    Section Chapter,
    bool HasChanges,
    ChangeClassification? Classification,
    bool CanRevoke,
    bool ShowDocumentControls,
    IReadOnlyList<PublishingDocumentData> Documents);

/// <summary>
/// Builds the chapter and document data required to render the Publishing page.
/// Encapsulates depth-first tree traversal, version history retrieval, and change classification.
/// </summary>
public interface IContentNavigationService
{
    /// <summary>
    /// Returns the ordered list of published leaf chapters and their document sections
    /// for the given project, enriched with version and classification metadata.
    /// </summary>
    Task<IReadOnlyList<PublishingChapterData>> BuildPublishingChapterDataAsync(
        Guid projectId, ProjectType projectType, CancellationToken ct = default);
}
