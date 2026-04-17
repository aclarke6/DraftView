using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Contracts;

/// <summary>
/// Lightweight tree node used for rendering the section hierarchy
/// in upload parent dropdowns and the future tree builder UI.
/// </summary>
public sealed class SectionTreeNode
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid? ParentId { get; init; }
    public string Title { get; init; } = default!;
    public int SortOrder { get; init; }
    public NodeType NodeType { get; init; }
    public IReadOnlyList<SectionTreeNode> Children { get; init; }
        = Array.Empty<SectionTreeNode>();
}
