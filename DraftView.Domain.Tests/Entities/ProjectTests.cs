using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

/// <summary>
/// Tests Project creation, reader activation, sync status, soft-delete behaviour,
/// manual project creation, and webhook sync control fields. Excludes persistence mapping.
/// </summary>
public class ProjectTests
{
    private static readonly Guid ValidAuthorId = Guid.NewGuid();

    // ---------------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidData_ReturnsProject()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);

        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal("My Novel", project.Name);
        Assert.Equal("/dropbox/MyNovel.scriv", project.DropboxPath);
        Assert.Equal(ValidAuthorId, project.AuthorId);
        Assert.False(project.IsReaderActive);
        Assert.False(project.IsSoftDeleted);
        Assert.Null(project.ReaderActivatedAt);
        Assert.Null(project.LastSyncedAt);
        Assert.Null(project.SyncErrorMessage);
        Assert.Null(project.SoftDeletedAt);
        Assert.Equal(SyncStatus.Stale, project.SyncStatus);
    }

    [Fact]
    public void Create_InitializesWebhookSyncControlFieldsAsNull()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);

        Assert.Null(project.SyncRequestedUtc);
        Assert.Null(project.LastWebhookUtc);
        Assert.Null(project.HeldUntilUtc);
        Assert.Null(project.LastSuccessfulSyncUtc);
        Assert.Null(project.LastSyncAttemptUtc);
        Assert.Null(project.SyncLeaseId);
        Assert.Null(project.SyncLeaseExpiresUtc);
        Assert.Null(project.LastBackgroundSyncOutcome);
    }

    [Fact]
    public void WebhookSyncControlFields_CanBeUpdatedByApplicationServices()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);
        var leaseId = Guid.NewGuid();
        var requestedAt = DateTime.UtcNow.AddMinutes(-5);
        var webhookAt = DateTime.UtcNow.AddMinutes(-4);
        var heldUntil = DateTime.UtcNow.AddMinutes(5);
        var successfulAt = DateTime.UtcNow.AddMinutes(-3);
        var attemptedAt = DateTime.UtcNow.AddMinutes(-2);
        var leaseExpiresAt = DateTime.UtcNow.AddMinutes(10);

        project.SyncRequestedUtc = requestedAt;
        project.LastWebhookUtc = webhookAt;
        project.HeldUntilUtc = heldUntil;
        project.LastSuccessfulSyncUtc = successfulAt;
        project.LastSyncAttemptUtc = attemptedAt;
        project.SyncLeaseId = leaseId;
        project.SyncLeaseExpiresUtc = leaseExpiresAt;
        project.LastBackgroundSyncOutcome = "Completed";

        Assert.Equal(requestedAt, project.SyncRequestedUtc);
        Assert.Equal(webhookAt, project.LastWebhookUtc);
        Assert.Equal(heldUntil, project.HeldUntilUtc);
        Assert.Equal(successfulAt, project.LastSuccessfulSyncUtc);
        Assert.Equal(attemptedAt, project.LastSyncAttemptUtc);
        Assert.Equal(leaseId, project.SyncLeaseId);
        Assert.Equal(leaseExpiresAt, project.SyncLeaseExpiresUtc);
        Assert.Equal("Completed", project.LastBackgroundSyncOutcome);
    }

    [Fact]
    public void Create_WithEmptyAuthorId_ThrowsInvariantViolationException()
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => Project.Create("My Novel", "/dropbox/MyNovel.scriv", Guid.Empty));

        Assert.Equal("I-PROJ-AUTHOR", ex.InvariantCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsInvariantViolationException(string? name)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => Project.Create(name, "/dropbox/MyNovel.scriv", ValidAuthorId));
#pragma warning restore CS8604

        Assert.Equal("I-PROJ-NAME", ex.InvariantCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidDropboxPath_ThrowsInvariantViolationException(string? path)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => Project.Create("My Novel", path, ValidAuthorId));
#pragma warning restore CS8604

        Assert.Equal("I-PROJ-PATH", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // ActivateForReaders
    // ---------------------------------------------------------------------------

    [Fact]
    public void ActivateForReaders_SetsIsReaderActiveAndRecordsTimestamp()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);
        var before = DateTime.UtcNow;

        project.ActivateForReaders();

        Assert.True(project.IsReaderActive);
        Assert.NotNull(project.ReaderActivatedAt);
        Assert.True(project.ReaderActivatedAt >= before);
    }

    [Fact]
    public void ActivateForReaders_WhenSoftDeleted_ThrowsInvariantViolationException()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);
        project.SoftDelete();

        var ex = Assert.Throws<InvariantViolationException>(
            () => project.ActivateForReaders());

        Assert.Equal("I-PROJ-DELETED", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // DeactivateForReaders
    // ---------------------------------------------------------------------------

    [Fact]
    public void DeactivateForReaders_SetsIsReaderActiveFalse()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);
        project.ActivateForReaders();

        project.DeactivateForReaders();

        Assert.False(project.IsReaderActive);
    }

    [Fact]
    public void DeactivateForReaders_WhenAlreadyInactive_DoesNotThrow()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);

        var ex = Record.Exception(() => project.DeactivateForReaders());

        Assert.Null(ex);
    }

    // ---------------------------------------------------------------------------
    // UpdateSyncStatus
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateSyncStatus_ToHealthy_ClearsSyncErrorMessage()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);
        var syncTime = DateTime.UtcNow;

        project.UpdateSyncStatus(SyncStatus.Healthy, syncTime, null);

        Assert.Equal(SyncStatus.Healthy, project.SyncStatus);
        Assert.Null(project.SyncErrorMessage);
        Assert.Equal(syncTime, project.LastSyncedAt);
    }

    [Fact]
    public void UpdateSyncStatus_ToStale_ClearsSyncErrorMessage()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);

        project.UpdateSyncStatus(SyncStatus.Stale, DateTime.UtcNow, null);

        Assert.Equal(SyncStatus.Stale, project.SyncStatus);
        Assert.Null(project.SyncErrorMessage);
    }

    [Fact]
    public void UpdateSyncStatus_ToError_WithMessage_SetsSyncErrorMessage()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);

        project.UpdateSyncStatus(SyncStatus.Error, DateTime.UtcNow, "File not found.");

        Assert.Equal(SyncStatus.Error, project.SyncStatus);
        Assert.Equal("File not found.", project.SyncErrorMessage);
    }

    [Fact]
    public void UpdateSyncStatus_ToError_WithoutMessage_ThrowsInvariantViolationException()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);

        var ex = Assert.Throws<InvariantViolationException>(
            () => project.UpdateSyncStatus(SyncStatus.Error, DateTime.UtcNow, null));

        Assert.Equal("I-SYNC-ERR", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // SoftDelete
    // ---------------------------------------------------------------------------

    [Fact]
    public void SoftDelete_SetsFlagsAndRecordsTimestamp()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);
        var before = DateTime.UtcNow;

        project.SoftDelete();

        Assert.True(project.IsSoftDeleted);
        Assert.NotNull(project.SoftDeletedAt);
        Assert.True(project.SoftDeletedAt >= before);
    }

    [Fact]
    public void SoftDelete_DeactivatesReadersWhenActive()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);
        project.ActivateForReaders();

        project.SoftDelete();

        Assert.False(project.IsReaderActive);
        Assert.True(project.IsSoftDeleted);
    }

    [Fact]
    public void SoftDelete_WhenAlreadyDeleted_DoesNotChangeSoftDeletedAt()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);
        project.SoftDelete();
        var firstDeletion = project.SoftDeletedAt;

        project.SoftDelete();

        Assert.Equal(firstDeletion, project.SoftDeletedAt);
    }

    // ---------------------------------------------------------------------------
    // CreateManual
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateManual_WithValidData_ReturnsProject()
    {
        var project = Project.CreateManual("My Manual Novel", ValidAuthorId);

        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal("My Manual Novel", project.Name);
        Assert.Equal(ValidAuthorId, project.AuthorId);
        Assert.False(project.IsReaderActive);
        Assert.False(project.IsSoftDeleted);
    }

    [Fact]
    public void CreateManual_SetsProjectTypeToManual()
    {
        var project = Project.CreateManual("My Manual Novel", ValidAuthorId);

        Assert.Equal(ProjectType.Manual, project.ProjectType);
    }

    [Fact]
    public void CreateManual_HasNullSyncRootId()
    {
        var project = Project.CreateManual("My Manual Novel", ValidAuthorId);

        Assert.Null(project.SyncRootId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateManual_WithNullName_ThrowsInvariantViolation(string? name)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => Project.CreateManual(name, ValidAuthorId));
#pragma warning restore CS8604

        Assert.Equal("I-PROJ-NAME", ex.InvariantCode);
    }

    [Fact]
    public void CreateManual_WithEmptyGuidAuthorId_ThrowsInvariantViolation()
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => Project.CreateManual("My Manual Novel", Guid.Empty));

        Assert.Equal("I-PROJ-AUTHOR", ex.InvariantCode);
    }

    [Fact]
    public void Create_ExistingFactory_SetsProjectTypeToScrivenerDropbox()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);

        Assert.Equal(ProjectType.ScrivenerDropbox, project.ProjectType);
    }

    [Fact]
    public void UpdateDropboxCursor_SetsCursor()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);

        project.UpdateDropboxCursor("cursor-001");

        Assert.Equal("cursor-001", project.DropboxCursor);
    }

    [Fact]
    public void UpdateDropboxCursor_WithEmptyString_ThrowsInvariantViolation()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);

        var ex = Assert.Throws<InvariantViolationException>(() => project.UpdateDropboxCursor(string.Empty));

        Assert.Equal("I-SYNC-CURSOR-EMPTY", ex.InvariantCode);
    }

    [Fact]
    public void UpdateDropboxCursor_WithWhitespace_ThrowsInvariantViolation()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);

        var ex = Assert.Throws<InvariantViolationException>(() => project.UpdateDropboxCursor("   "));

        Assert.Equal("I-SYNC-CURSOR-EMPTY", ex.InvariantCode);
    }

    [Fact]
    public void UpdateDropboxCursor_OverwritesPreviousCursor()
    {
        var project = Project.Create("My Novel", "/dropbox/MyNovel.scriv", ValidAuthorId);
        project.UpdateDropboxCursor("cursor-001");

        project.UpdateDropboxCursor("cursor-002");

        Assert.Equal("cursor-002", project.DropboxCursor);
    }
}
