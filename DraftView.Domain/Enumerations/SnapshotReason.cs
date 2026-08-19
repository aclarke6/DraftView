namespace DraftView.Domain.Enumerations;

/// <summary>
/// Records why a <see cref="DraftView.Domain.Entities.ManualChapterVersion"/> snapshot was created.
/// </summary>
public enum SnapshotReason
{
    FileUpload  = 0,
    Replace     = 1,
    PasteUpload = 2,
    InlineEdit  = 3
}
