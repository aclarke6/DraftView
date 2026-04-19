namespace DraftView.Domain.Interfaces.Services;

public enum DropboxEntryType
{
    Added,
    Modified,
    Deleted
}

public sealed record DropboxChangedEntry(
    string Path,
    DropboxEntryType EntryType,
    string? ContentHash);
