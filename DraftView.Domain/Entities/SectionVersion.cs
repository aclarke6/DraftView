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
    public string HtmlContent { get; private set; } = default!;
    public string ContentHash { get; private set; } = default!;
    public ChangeClassification? ChangeClassification { get; private set; }
    public string? AiSummary { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SectionVersion() { }

    /// <summary>
    /// Creates a new immutable snapshot of a Document section's prose content.
    /// This is the only valid creation path for SectionVersion entities.
    /// </summary>
    /// <param name="section">The Section to snapshot. Must be a Document with non-empty HtmlContent.</param>
    /// <param name="authorId">The ID of the author creating this version.</param>
    /// <param name="nextVersionNumber">The version number to assign (1-based, must be MAX + 1).</param>
    /// <returns>A new SectionVersion entity.</returns>
    /// <exception cref="InvariantViolationException">Thrown when invariants are violated.</exception>
    public static SectionVersion Create(Section section, Guid authorId, int nextVersionNumber)
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

        if (nextVersionNumber < 1)
            throw new InvariantViolationException("I-VER-NUMBER",
                "Version number must be 1 or greater.");

        return new SectionVersion
        {
            Id = Guid.NewGuid(),
            SectionId = section.Id,
            AuthorId = authorId,
            VersionNumber = nextVersionNumber,
            HtmlContent = section.HtmlContent,
            ContentHash = section.ContentHash ?? string.Empty,
            ChangeClassification = null,
            AiSummary = null,
            CreatedAt = DateTime.UtcNow
        };
    }
}
