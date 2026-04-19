using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
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

    /// <summary>
    /// Creates a section explicitly for tree management workflows.
    /// </summary>
    public Task<Section> CreateSectionAsync(
        Guid projectId,
        string title,
        NodeType nodeType,
        Guid? parentId,
        int? sortOrder,
        Guid authorId,
        CancellationToken ct = default)
        => CreateSectionCoreAsync(projectId, title, nodeType, parentId, sortOrder, ct);

    /// <summary>
    /// Moves a section to a new parent and/or sort order.
    /// </summary>
    public Task MoveSectionAsync(
        Guid sectionId,
        Guid? newParentId,
        int newSortOrder,
        Guid authorId,
        CancellationToken ct = default)
        => MoveSectionCoreAsync(sectionId, newParentId, newSortOrder, ct);

    /// <summary>
    /// Soft-deletes a section and descendants.
    /// </summary>
    public Task DeleteSectionAsync(
        Guid sectionId,
        Guid authorId,
        CancellationToken ct = default)
        => DeleteSectionCoreAsync(sectionId, ct);

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

    private async Task<Section> CreateSectionCoreAsync(
        Guid projectId,
        string title,
        NodeType nodeType,
        Guid? parentId,
        int? sortOrder,
        CancellationToken ct)
    {
        ValidateCreateSectionArguments(title, nodeType);

        var sections = await sectionRepository.GetByProjectIdAsync(projectId, ct);
        ValidateParent(projectId, parentId, sections);

        var resolvedSortOrder = sortOrder ?? ResolveDefaultSortOrder([.. sections.Where(s => !s.IsSoftDeleted)], parentId);
        var section = CreateTreeManagedSection(projectId, title, nodeType, parentId, resolvedSortOrder);

        await sectionRepository.AddAsync(section, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return section;
    }

    private async Task MoveSectionCoreAsync(
        Guid sectionId,
        Guid? newParentId,
        int newSortOrder,
        CancellationToken ct)
    {
        var section = await sectionRepository.GetByIdAsync(sectionId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), sectionId);

        await EnsureMoveDoesNotCreateCycleAsync(sectionId, newParentId, ct);

        section.UpdateParent(newParentId);
        section.UpdateSortOrder(newSortOrder);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task DeleteSectionCoreAsync(Guid sectionId, CancellationToken ct)
    {
        var section = await sectionRepository.GetByIdAsync(sectionId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), sectionId);

        var descendants = await sectionRepository.GetAllDescendantsAsync(sectionId, ct);
        foreach (var current in descendants.Append(section))
        {
            if (current.IsPublished)
                current.Unpublish();

            current.SoftDelete();
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private static void ValidateCreateSectionArguments(string title, NodeType nodeType)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new InvariantViolationException("I-TREE-TITLE", "Section title must not be empty.");

        if (nodeType is not NodeType.Document and not NodeType.Folder)
            throw new InvariantViolationException("I-TREE-NODETYPE", "Section node type must be Folder or Document.");
    }

    private static void ValidateParent(Guid projectId, Guid? parentId, IReadOnlyList<Section> sections)
    {
        if (!parentId.HasValue)
            return;

        var parent = sections.FirstOrDefault(s => s.Id == parentId.Value && !s.IsSoftDeleted);
        if (parent is null || parent.ProjectId != projectId)
            throw new InvariantViolationException("I-TREE-PARENT", "Parent section was not found in this project.");
    }

    private static Section CreateTreeManagedSection(
        Guid projectId,
        string title,
        NodeType nodeType,
        Guid? parentId,
        int sortOrder)
    {
        return nodeType == NodeType.Document
            ? Section.CreateDocumentForUpload(projectId, title, parentId, sortOrder)
            : Section.CreateFolderForTree(projectId, title, parentId, sortOrder);
    }

    private async Task EnsureMoveDoesNotCreateCycleAsync(Guid sectionId, Guid? newParentId, CancellationToken ct)
    {
        var currentParentId = newParentId;
        while (currentParentId.HasValue)
        {
            if (currentParentId.Value == sectionId)
                throw new InvariantViolationException(
                    "I-TREE-CIRCULAR",
                    "Cannot move a section to one of its own descendants.");

            var parent = await sectionRepository.GetByIdAsync(currentParentId.Value, ct);
            currentParentId = parent?.ParentId;
        }
    }
}
