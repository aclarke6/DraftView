namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Generates a one-line AI summary describing changes between versions.
/// The summary names characters, locations, and events from the prose.
/// Written in the author's voice as a note to beta readers.
/// Returns null on any failure — callers must handle null gracefully.
/// AI failure must never block publishing.
/// </summary>
public interface IAiSummaryService
{
    /// <summary>
    /// Generates a one-line summary for a section version.
    /// </summary>
    /// <param name="previousHtml">The previous version's HTML content. Null for first versions.</param>
    /// <param name="currentHtml">The current working state HTML content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A one-line summary string, or null if generation failed or was skipped.</returns>
    Task<string?> GenerateSummaryAsync(
        string? previousHtml,
        string currentHtml,
        CancellationToken ct = default);
}
