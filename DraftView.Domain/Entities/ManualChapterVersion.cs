using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

/// <summary>
/// Immutable snapshot of a ManualChapter's content at a point in time.
/// Records are never modified after creation; hard-delete is the only removal path.
/// </summary>
public sealed class ManualChapterVersion
{
    public Guid Id { get; private set; }
    public Guid ManualChapterId { get; private set; }
    public Guid AuthorId { get; private set; }
    public int VersionNumber { get; private set; }
    public string RawContent { get; private set; } = default!;
    public SnapshotReason SnapshotReason { get; private set; }
    public DateTime SnapshotAt { get; private set; }

    private ManualChapterVersion() { }

    /// <summary>
    /// Creates a new immutable ManualChapterVersion, enforcing all domain invariants.
    /// </summary>
    public static ManualChapterVersion Create(
        Guid manualChapterId,
        Guid authorId,
        int versionNumber,
        string rawContent,
        SnapshotReason snapshotReason,
        DateTime snapshotAt)
    {
        if (rawContent is null)
            throw new InvariantViolationException("I-MCV-01", "RawContent must not be null.");
        if (versionNumber <= 0)
            throw new InvariantViolationException("I-MCV-02", "VersionNumber must be greater than zero.");

        return new ManualChapterVersion
        {
            Id               = Guid.NewGuid(),
            ManualChapterId  = manualChapterId,
            AuthorId         = authorId,
            VersionNumber    = versionNumber,
            RawContent       = rawContent,
            SnapshotReason   = snapshotReason,
            SnapshotAt       = snapshotAt
        };
    }
}
