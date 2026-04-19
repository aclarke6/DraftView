using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;

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

    /// <summary>
    /// Creates a new Document or Folder section with an explicit title, parent, and sort order.
    /// Called from the tree builder UI. Section will have ScrivenerUuid = null.
    /// </summary>
    Task<Section> CreateSectionAsync(
        Guid projectId,
        string title,
        NodeType nodeType,
        Guid? parentId,
        int? sortOrder,
        Guid authorId,
        CancellationToken ct = default);

    /// <summary>
    /// Moves a section to a new parent and/or sort order.
    /// Validates that the move does not create a circular reference.
    /// </summary>
    Task MoveSectionAsync(
        Guid sectionId,
        Guid? newParentId,
        int newSortOrder,
        Guid authorId,
        CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a section and all its descendants.
    /// Published sections are unpublished before soft-deletion.
    /// </summary>
    Task DeleteSectionAsync(
        Guid sectionId,
        Guid authorId,
        CancellationToken ct = default);
}
