using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

/// <summary>
/// Represents a single chapter contributed to a manual-upload project.
/// One file or paste submission equals one chapter — no auto-splitting.
/// </summary>
public sealed class ManualChapter
{
    public Guid Id { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = default!;
    public int SortOrder { get; private set; }
    public string RawContent { get; private set; } = default!;
    public string? OriginalFileName { get; private set; }
    public DateTime UploadedAt { get; private set; }

    /// <summary>
    /// The Id of the published Folder Section created for this chapter.
    /// Null until the chapter has been published at least once.
    /// </summary>
    public Guid? LinkedSectionId { get; private set; }

    private ManualChapter() { }

    /// <summary>
    /// Creates a new ManualChapter, enforcing all domain invariants.
    /// </summary>
    public static ManualChapter Create(
        Guid authorId,
        Guid projectId,
        string title,
        int sortOrder,
        string rawContent,
        string? originalFileName,
        DateTime uploadedAt)
    {
        if (authorId == Guid.Empty)
            throw new InvariantViolationException("I-MC-04", "AuthorId must not be empty.");
        if (projectId == Guid.Empty)
            throw new InvariantViolationException("I-MC-05", "ProjectId must not be empty.");
        if (string.IsNullOrWhiteSpace(title))
            throw new InvariantViolationException("I-MC-01", "Title must not be null or whitespace.");
        if (sortOrder < 0)
            throw new InvariantViolationException("I-MC-02", "SortOrder must be zero or positive.");
        if (rawContent is null)
            throw new InvariantViolationException("I-MC-03", "RawContent must not be null.");

        return new ManualChapter
        {
            Id               = Guid.NewGuid(),
            AuthorId         = authorId,
            ProjectId        = projectId,
            Title            = title.Trim(),
            SortOrder        = sortOrder,
            RawContent       = rawContent,
            OriginalFileName = originalFileName,
            UploadedAt       = uploadedAt
        };
    }

    /// <summary>
    /// Replaces the chapter's stored content in-place. Called before creating a version snapshot.
    /// </summary>
    public void UpdateContent(string rawContent)
    {
        if (rawContent is null)
            throw new InvariantViolationException("I-MC-03", "RawContent must not be null.");
        RawContent = rawContent;
    }

    /// <summary>
    /// Changes the chapter title.
    /// </summary>
    public void UpdateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new InvariantViolationException("I-MC-01", "Title must not be null or whitespace.");
        Title = title.Trim();
    }

    /// <summary>
    /// Changes the sort order.
    /// </summary>
    public void UpdateSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
            throw new InvariantViolationException("I-MC-02", "SortOrder must be zero or positive.");
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Records the Folder Section created to publish this chapter's content to readers.
    /// </summary>
    public void SetLinkedSection(Guid sectionId)
    {
        if (sectionId == Guid.Empty)
            throw new InvariantViolationException("I-MC-06", "LinkedSectionId must not be empty.");
        LinkedSectionId = sectionId;
    }
}
