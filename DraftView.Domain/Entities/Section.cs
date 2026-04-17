using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

public sealed class Section
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string ScrivenerUuid { get; private set; } = default!;
    public Guid? ParentId { get; private set; }
    public string Title { get; private set; } = default!;
    public int SortOrder { get; private set; }
    public NodeType NodeType { get; private set; }
    public string? ScrivenerStatus { get; private set; }
    public string? HtmlContent { get; private set; }
    public string? ContentHash { get; private set; }
    public bool IsPublished { get; private set; }
    public bool ContentChangedSincePublish { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime? UnpublishedAt { get; private set; }
    public bool IsSoftDeleted { get; private set; }
    public DateTime? SoftDeletedAt { get; private set; }

    private Section() { }

    public static Section CreateFolder(
        Guid projectId, string scrivenerUuid, string title,
        Guid? parentId, int sortOrder)
    {
        ValidateCommon(scrivenerUuid, title);
        return new Section
        {
            Id             = Guid.NewGuid(),
            ProjectId      = projectId,
            ScrivenerUuid  = scrivenerUuid.Trim(),
            Title          = title.Trim(),
            ParentId       = parentId,
            SortOrder      = sortOrder,
            NodeType       = NodeType.Folder,
            IsPublished    = false,
            IsSoftDeleted  = false,
            ContentChangedSincePublish = false
        };
    }

    public static Section CreateDocument(
        Guid projectId, string scrivenerUuid, string title,
        Guid? parentId, int sortOrder, string? htmlContent,
        string? contentHash, string? scrivenerStatus)
    {
        ValidateCommon(scrivenerUuid, title);
        return new Section
        {
            Id             = Guid.NewGuid(),
            ProjectId      = projectId,
            ScrivenerUuid  = scrivenerUuid.Trim(),
            Title          = title.Trim(),
            ParentId       = parentId,
            SortOrder      = sortOrder,
            NodeType       = NodeType.Document,
            HtmlContent    = htmlContent,
            ContentHash    = contentHash,
            ScrivenerStatus = scrivenerStatus,
            IsPublished    = false,
            IsSoftDeleted  = false,
            ContentChangedSincePublish = false
        };
    }

    /// <summary>
    /// Creates a document section for manual upload workflows.
    /// These sections do not have a ScrivenerUuid.
    /// </summary>
    public static Section CreateDocumentForUpload(
        Guid projectId, string title, Guid? parentId, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new InvariantViolationException("I-SEC-TITLE",
                "Section title must not be null or whitespace.");

        return new Section
        {
            Id             = Guid.NewGuid(),
            ProjectId      = projectId,
            ScrivenerUuid  = null!,
            Title          = title.Trim(),
            ParentId       = parentId,
            SortOrder      = sortOrder,
            NodeType       = NodeType.Document,
            HtmlContent    = null,
            ContentHash    = null,
            ScrivenerStatus = null,
            IsPublished    = false,
            IsSoftDeleted  = false,
            ContentChangedSincePublish = false
        };
    }

    // ---------------------------------------------------------------------------
    // Publication
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Publishes a scene as part of a chapter publication.
    /// This is the only permitted way to publish a Document section.
    /// Call only from PublicationService.PublishChapterAsync.
    /// </summary>
    public void PublishAsPartOfChapter(string contentHash)
    {
        if (NodeType == NodeType.Folder)
            throw new InvariantViolationException("I-04",
                "Only Document sections may be published.");

        if (IsSoftDeleted)
            throw new InvariantViolationException("I-18",
                "A soft-deleted section may not be published.");

        IsPublished                = true;
        PublishedAt                = DateTime.UtcNow;
        ContentHash                = contentHash;
        ContentChangedSincePublish = false;
    }

    /// <summary>
    /// Marks a Folder section as a published container (e.g. a published chapter).
    /// </summary>
    public void MarkAsPublishedContainer()
    {
        if (NodeType != NodeType.Folder)
            throw new InvariantViolationException("I-CONTAINER",
                "MarkAsPublishedContainer is only valid on Folder sections.");

        if (IsSoftDeleted)
            throw new InvariantViolationException("I-18",
                "A soft-deleted section may not be published.");

        IsPublished = true;
        PublishedAt = DateTime.UtcNow;
    }

    public void UnmarkAsPublishedContainer()
    {
        if (NodeType != NodeType.Folder) return;
        IsPublished   = false;
        UnpublishedAt = DateTime.UtcNow;
    }

    public void Unpublish()
    {
        if (!IsPublished) return;
        IsPublished   = false;
        UnpublishedAt = DateTime.UtcNow;
    }

    public void MarkContentChanged()
    {
        ContentChangedSincePublish = true;
    }

    public void UpdateContent(string? htmlContent, string? contentHash)
    {
        HtmlContent = htmlContent;
        ContentHash = contentHash;
    }

    public void UpdateSortOrder(int sortOrder) => SortOrder = sortOrder;

    public void UpdateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new InvariantViolationException("I-SEC-TITLE",
                "Section title must not be null or whitespace.");
        Title = title.Trim();
    }

    public void UpdateScrivenerStatus(string? status) => ScrivenerStatus = status;

    public void SoftDelete()
    {
        if (IsSoftDeleted) return;
        if (IsPublished) Unpublish();
        IsSoftDeleted = true;
        SoftDeletedAt = DateTime.UtcNow;
    }

    private static void ValidateCommon(string scrivenerUuid, string title)
    {
        if (string.IsNullOrWhiteSpace(scrivenerUuid))
            throw new InvariantViolationException("I-SEC-UUID",
                "Section ScrivenerUuid must not be null or whitespace.");

        if (string.IsNullOrWhiteSpace(title))
            throw new InvariantViolationException("I-SEC-TITLE",
                "Section title must not be null or whitespace.");
    }
}
