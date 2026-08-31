using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

/// <summary>
/// Stores the HTML content a reader last read for a given scene.
/// Used to compute change state (Trivial/Polish/Revision/Rewrite/New) at page view time.
/// One record per (SectionId, UserId) pair — upserted when read state becomes true.
/// </summary>
public sealed class ReaderSnapshot
{
    public Guid Id { get; private set; }
    public Guid SectionId { get; private set; }
    public Guid UserId { get; private set; }
    public string HtmlContent { get; private set; } = default!;
    public DateTime SnapshotAt { get; private set; }

    private ReaderSnapshot() { }

    /// <summary>
    /// Creates a new snapshot of what a reader last read.
    /// </summary>
    public static ReaderSnapshot Create(Guid sectionId, Guid userId, string htmlContent)
    {
        if (sectionId == Guid.Empty)
            throw new InvariantViolationException("I-SNAP-SECTION", "SectionId must not be empty.");

        if (userId == Guid.Empty)
            throw new InvariantViolationException("I-SNAP-USER", "UserId must not be empty.");

        if (string.IsNullOrWhiteSpace(htmlContent))
            throw new InvariantViolationException("I-SNAP-HTML", "HtmlContent must not be null or empty.");

        return new ReaderSnapshot
        {
            Id          = Guid.NewGuid(),
            SectionId   = sectionId,
            UserId      = userId,
            HtmlContent = htmlContent,
            SnapshotAt  = DateTime.UtcNow
        };
    }
}
