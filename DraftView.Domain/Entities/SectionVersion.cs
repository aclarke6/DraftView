using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

/// <summary>
/// An immutable snapshot of a Section's prose content at the moment of a
/// Republish action. Readers always see the latest SectionVersion.
/// HtmlContent and ContentHash are immutable after creation.
/// </summary>
public sealed class SectionVersion
{
    public Guid Id { get; private set; }
    public Guid SectionId { get; private set; }
    public Guid AuthorId { get; private set; }
    public int VersionNumber { get; private set; }
    public int MajorVersion { get; private set; }
    public int MinorVersion { get; private set; }

    /// <summary>
    /// Scrivener status label captured at publish time (e.g. "First Draft", "Revised Draft").
    /// Null for manual-upload sections or pre-feature backfilled records.
    /// Used by VersioningService to detect status changes and determine major vs minor bumps.
    /// </summary>
    public string? ScrivenerStatus { get; private set; }

    public string HtmlContent { get; private set; } = default!;
    public string ContentHash { get; private set; } = default!;
    public ChangeClassification? ChangeClassification { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Human-readable version label shown to readers, e.g. "Version 1.03 — Polish".
    /// Omits the classification label when none has been set (first publish).
    /// </summary>
    public string VersionLabel => ChangeClassification.HasValue
        ? $"Version {MajorVersion}.{MinorVersion:D2} — {ChangeClassification}"
        : $"Version {MajorVersion}.{MinorVersion:D2}";

    private SectionVersion() { }

    /// <summary>
    /// Creates a new immutable snapshot of a Document section's prose content.
    /// This is the only valid creation path for SectionVersion entities.
    /// </summary>
    /// <param name="section">The Section to snapshot. Must be a Document with non-empty HtmlContent.</param>
    /// <param name="authorId">The ID of the author creating this version.</param>
    /// <param name="versionNumber">Sequential version number (1-based) for ordering and ReadEvent comparison.</param>
    /// <param name="majorVersion">Semantic major version — increments when ScrivenerStatus changes.</param>
    /// <param name="minorVersion">Semantic minor version — increments when status is unchanged.</param>
    /// <returns>A new SectionVersion entity.</returns>
    /// <exception cref="InvariantViolationException">Thrown when invariants are violated.</exception>
    public static SectionVersion Create(
        Section section, Guid authorId, int versionNumber, int majorVersion, int minorVersion)
    {
        if (section.NodeType != NodeType.Document)
            throw new InvariantViolationException("I-VER-FOLDER",
                "Only Document sections can be versioned. Folder nodes cannot have versions.");

        if (section.IsSoftDeleted)
            throw new InvariantViolationException("I-VER-DELETED",
                "Cannot create a version of a soft-deleted section.");

        if (string.IsNullOrEmpty(section.HtmlContent))
            throw new InvariantViolationException("I-VER-CONTENT",
                "Cannot create a version when HtmlContent is null or empty.");

        if (authorId == Guid.Empty)
            throw new InvariantViolationException("I-VER-AUTHOR",
                "AuthorId must not be empty.");

        if (versionNumber < 1)
            throw new InvariantViolationException("I-VER-NUMBER",
                "Version number must be 1 or greater.");

        if (majorVersion < 1)
            throw new InvariantViolationException("I-VER-MAJOR",
                "Major version must be 1 or greater.");

        if (minorVersion < 0)
            throw new InvariantViolationException("I-VER-MINOR",
                "Minor version must be 0 or greater.");

        return new SectionVersion
        {
            Id = Guid.NewGuid(),
            SectionId = section.Id,
            AuthorId = authorId,
            VersionNumber = versionNumber,
            MajorVersion = majorVersion,
            MinorVersion = minorVersion,
            ScrivenerStatus = section.ScrivenerStatus,
            HtmlContent = section.HtmlContent,
            ContentHash = section.ContentHash ?? string.Empty,
            ChangeClassification = null,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Sets the change classification for this version.
    /// Called by the application layer after diff-based heuristic classification.
    /// Can only be set once — classification is immutable after first assignment.
    /// </summary>
    /// <param name="classification">The classification to assign.</param>
    /// <exception cref="InvariantViolationException">Thrown when classification has already been set.</exception>
    public void SetChangeClassification(ChangeClassification classification)
    {
        if (ChangeClassification.HasValue)
            throw new InvariantViolationException("I-VER-CLASS",
                "ChangeClassification has already been set and cannot be changed.");

        ChangeClassification = classification;
    }

}
