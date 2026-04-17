using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

public sealed class Project
{
    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    public Guid Id { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Name { get; private set; } = default!;
    public string DropboxPath { get; private set; } = default!;

    /// <summary>
    /// UUID of the Scrivener binder node that is the root of this project.
    /// For Book-split vaults this is the Book folder UUID.
    /// For single-project vaults this is the Manuscript (DraftFolder) UUID.
    /// </summary>
    public string? SyncRootId { get; private set; } = default!;
    public ProjectType ProjectType { get; private set; }
    public bool IsReaderActive { get; private set; }
    public DateTime? ReaderActivatedAt { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }
    public SyncStatus SyncStatus { get; private set; }
    public string? SyncErrorMessage { get; private set; }
    public bool IsSoftDeleted { get; private set; }
    public DateTime? SoftDeletedAt { get; private set; }

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    private Project() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    public static Project Create(
        string name,
        string dropboxPath,
        Guid authorId,
        string? scrivenerRootUuid = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvariantViolationException("I-PROJ-NAME",
                "Project name must not be null or whitespace.");

        if (string.IsNullOrWhiteSpace(dropboxPath))
            throw new InvariantViolationException("I-PROJ-PATH",
                "Project Dropbox path must not be null or whitespace.");

        if (authorId == Guid.Empty)
            throw new InvariantViolationException("I-PROJ-AUTHOR",
                "Project must be associated with a valid author.");

        return new Project {
            Id                = Guid.NewGuid(),
            AuthorId          = authorId,
            Name              = name.Trim(),
            DropboxPath       = dropboxPath.Trim(),
            SyncRootId = scrivenerRootUuid?.Trim(),
            ProjectType       = ProjectType.ScrivenerDropbox,
            IsReaderActive    = false,
            SyncStatus        = SyncStatus.Stale,
            IsSoftDeleted     = false
        };
    }

    /// <summary>
    /// Creates a manual project that receives content only via author-initiated file import.
    /// Manual projects do not sync with Dropbox.
    /// </summary>
    public static Project CreateManual(string name, Guid authorId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvariantViolationException("I-PROJ-NAME",
                "Project name must not be null or whitespace.");

        if (authorId == Guid.Empty)
            throw new InvariantViolationException("I-PROJ-AUTHOR",
                "Project must be associated with a valid author.");

        return new Project {
            Id                = Guid.NewGuid(),
            AuthorId          = authorId,
            Name              = name.Trim(),
            DropboxPath       = string.Empty,
            SyncRootId        = null,
            ProjectType       = ProjectType.Manual,
            IsReaderActive    = false,
            SyncStatus        = SyncStatus.Stale,
            IsSoftDeleted     = false
        };
    }

    // ---------------------------------------------------------------------------
    // Behaviour
    // ---------------------------------------------------------------------------

    public void ActivateForReaders()
    {
        if (IsSoftDeleted)
            throw new InvariantViolationException("I-PROJ-DELETED",
                "A soft-deleted project cannot be activated for readers.");

        IsReaderActive    = true;
        ReaderActivatedAt = DateTime.UtcNow;
    }

    public void DeactivateForReaders()
    {
        IsReaderActive = false;
    }

    public void UpdateSyncStatus(SyncStatus status, DateTime syncedAt, string? errorMessage)
    {
        if (status == SyncStatus.Error && string.IsNullOrWhiteSpace(errorMessage))
            throw new InvariantViolationException("I-SYNC-ERR",
                "A sync error message is required when status is Error.");

        SyncStatus       = status;
        LastSyncedAt     = syncedAt;
        SyncErrorMessage = status == SyncStatus.Error ? errorMessage : null;
    }

    public void MarkSyncing()
    {
        SyncStatus       = SyncStatus.Syncing;
        SyncErrorMessage = null;
    }

    public void SoftDelete()
    {
        if (IsSoftDeleted)
            return;

        IsReaderActive = false;
        IsSoftDeleted  = true;
        SoftDeletedAt  = DateTime.UtcNow;
    }

    public void Restore(string? updatedName = null)
    {
        IsSoftDeleted = false;
        SoftDeletedAt = null;
        SyncStatus    = SyncStatus.Stale;
        if (updatedName is not null)
            Name = updatedName.Trim();
    }
}
