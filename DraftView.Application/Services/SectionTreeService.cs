using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Manages upload-created sections and builds section hierarchy trees.
/// </summary>
public class SectionTreeService(
    ISectionRepository sectionRepository,
    IUnitOfWork unitOfWork) : ISectionTreeService
{
    /// <summary>
    /// Finds an existing upload section or creates a new document section for manual import.
    /// </summary>
    public Task<Section> GetOrCreateForUploadAsync(
        Guid projectId,
        string title,
        Guid? parentId,
        int? sortOrder,
        CancellationToken ct = default)
        => GetOrCreateForUploadCoreAsync(projectId, title, parentId, sortOrder, ct);

    /// <summary>
    /// Builds the full section hierarchy for a project.
    /// </summary>
    public Task<IReadOnlyList<SectionTreeNode>> GetTreeAsync(
        Guid projectId,
        CancellationToken ct = default)
        => GetTreeCoreAsync(projectId, ct);

    private async Task<Section> GetOrCreateForUploadCoreAsync(
        Guid projectId,
        string title,
        Guid? parentId,
        int? sortOrder,
        CancellationToken ct)
    {
        var sections = (await sectionRepository.GetByProjectIdAsync(projectId, ct))
            .Where(s => !s.IsSoftDeleted)
            .ToList();

        var existing = sections.FirstOrDefault(s =>
            s.NodeType == NodeType.Document &&
            s.ParentId == parentId &&
            string.Equals(s.Title.Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
            return existing;

        var resolvedSortOrder = sortOrder ?? ResolveDefaultSortOrder(sections, parentId);
        var section = Section.CreateDocumentForUpload(projectId, title, parentId, resolvedSortOrder);

        await sectionRepository.AddAsync(section, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return section;
    }

    private async Task<IReadOnlyList<SectionTreeNode>> GetTreeCoreAsync(
        Guid projectId,
        CancellationToken ct)
    {
        var sections = (await sectionRepository.GetByProjectIdAsync(projectId, ct))
            .Where(s => !s.IsSoftDeleted)
            .ToList();

        var lookup = sections
            .GroupBy(s => s.ParentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SortOrder).ToList());

        return BuildTree(lookup, Guid.Empty);
    }

    private static int ResolveDefaultSortOrder(
        IReadOnlyList<Section> sections,
        Guid? parentId)
    {
        var siblings = sections.Where(s => s.ParentId == parentId).ToList();
        return siblings.Count == 0 ? 1 : siblings.Max(s => s.SortOrder) + 1;
    }

    private static IReadOnlyList<SectionTreeNode> BuildTree(
        IReadOnlyDictionary<Guid, List<Section>> lookup,
        Guid parentId)
    {
        if (!lookup.TryGetValue(parentId, out var children))
            return Array.Empty<SectionTreeNode>();

        return children.Select(child => new SectionTreeNode
        {
            Id         = child.Id,
            ProjectId  = child.ProjectId,
            ParentId   = child.ParentId,
            Title      = child.Title,
            SortOrder  = child.SortOrder,
            NodeType   = child.NodeType,
            Children   = BuildTree(lookup, child.Id)
        }).ToList();
    }
}
