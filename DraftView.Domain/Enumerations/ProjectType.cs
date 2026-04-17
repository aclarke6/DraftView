namespace DraftView.Domain.Enumerations;

/// <summary>
/// Determines the ingestion source for a project.
/// ScrivenerDropbox projects sync via Dropbox. Manual projects receive
/// content only via author-initiated file import.
/// </summary>
public enum ProjectType
{
    ScrivenerDropbox = 0,
    Manual = 1
}
