namespace DraftView.Application.Services;

/// <summary>
/// Scrivener statuses that allow a scene to be shown to readers (First Draft or above).
/// </summary>
internal static class PublishableStatus
{
    private static readonly IReadOnlySet<string> Statuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "First Draft", "Revised Draft", "Final Draft", "Done"
        };

    /// <summary>
    /// Returns true when the status is explicitly one of the publishable statuses.
    /// A null or blank status returns false.
    /// </summary>
    public static bool Contains(string? status) =>
        !string.IsNullOrWhiteSpace(status) && Statuses.Contains(status);
}
