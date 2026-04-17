using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Manages the Section tree structure for author-managed projects.
/// GetOrCreateForUploadAsync is the only permitted creation path for
/// sections without a ScrivenerUuid.
/// </summary>
public interface ISectionTreeService
{
    /// <summary>
    /// Finds an existing Document section matching title + parentId within the project,
    /// or creates one. Created sections have ScrivenerUuid = null and NodeType = Document.
    /// SortOrder defaults to end of sibling list when not supplied.
    /// This is the ONLY place in the solution where a Section with ScrivenerUuid = null
    /// may be created.
    /// </summary>
    Task<Section> GetOrCreateForUploadAsync(
        Guid projectId,
        string title,
        Guid? parentId,
        int? sortOrder,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the full section hierarchy for a project as a tree of SectionTreeNodes.
    /// Soft-deleted sections are excluded. Ordered by SortOrder within each level.
    /// </summary>
    Task<IReadOnlyList<SectionTreeNode>> GetTreeAsync(
        Guid projectId,
        CancellationToken ct = default);
}
