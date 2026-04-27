namespace DraftView.Domain.Contracts;

/// <summary>
/// Original context data for a passage anchor.
/// </summary>
public sealed class OriginalContextDto
{
    public Guid PassageAnchorId { get; init; }
    public Guid SectionId { get; init; }

    public Guid? OriginalSectionVersionId { get; init; }
    public bool IsLegacyFallback { get; init; }

    public string OriginalSelectedText { get; init; } = string.Empty;
    public string NormalizedSelectedText { get; init; } = string.Empty;
    public string PrefixContext { get; init; } = string.Empty;
    public string SuffixContext { get; init; } = string.Empty;

    public int StartOffset { get; init; }
    public int EndOffset { get; init; }

    public string OriginalHtmlContent { get; init; } = string.Empty;

    public string? OriginalVersionLabel { get; init; }
    public int? OriginalVersionNumber { get; init; }
    public DateTime? OriginalVersionCreatedAtUtc { get; init; }
}
