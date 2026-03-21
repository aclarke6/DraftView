using ScrivenerSync.Domain.Entities;

namespace ScrivenerSync.Web.Models;

public class ReadingViewModel
{
    public Section Section { get; set; } = default!;
    public IReadOnlyList<Comment> Comments { get; set; } = new List<Comment>();
    public IReadOnlyList<Section> TableOfContents { get; set; } = new List<Section>();
}

public class AddCommentViewModel
{
    public Guid SectionId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public Guid? ParentCommentId { get; set; }
}
