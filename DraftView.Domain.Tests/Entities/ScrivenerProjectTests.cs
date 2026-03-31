using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class ScrivenerProjectTests
{
    // ---------------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidData_ReturnsProject()
    {
        var project = ScrivenerProject.Create("My Novel", "/dropbox/MyNovel.scriv");

        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal("My Novel", project.Name);
        Assert.Equal("/dropbox/MyNovel.scriv", project.DropboxPath);
        Assert.False(project.IsReaderActive);
        Assert.False(project.IsSoftDeleted);
        Assert.Null(project.ReaderActivatedAt);
        Assert.Null(project.LastSyncedAt);
        Assert.Null(project.SyncErrorMessage);
        Assert.Null(project.SoftDeletedAt);
        Assert.Equal(SyncStatus.Stale, project.SyncStatus);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsInvariantViolationException(string? name)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => ScrivenerProject.Create(name, "/dropbox/MyNovel.scriv"));
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
            () => ScrivenerProject.Create("My Novel", path));
#pragma warning restore CS8604

        Assert.Equal("I-PROJ-PATH", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // ActivateForReaders
    // ---------------------------------------------------------------------------

    [Fact]
    public void ActivateForReaders_SetsIsReaderActiveAndRecordsTimestamp()
    {
        var project = ScrivenerProject.Create("My Novel", "/dropbox/MyNovel.scriv");
        var before = DateTime.UtcNow;

        project.ActivateForReaders();

        Assert.True(project.IsReaderActive);
        Assert.NotNull(project.ReaderActivatedAt);
        Assert.True(project.ReaderActivatedAt >= before);
    }

    [Fact]
    public void ActivateForReaders_WhenSoftDeleted_ThrowsInvariantViolationException()
    {
        var project = ScrivenerProject.Create("My Novel", "/dropbox/MyNovel.scriv");
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
        var project = ScrivenerProject.Create("My Novel", "/dropbox/MyNovel.scriv");
        project.ActivateForReaders();

        project.DeactivateForReaders();

        Assert.False(project.IsReaderActive);
    }

    [Fact]
    public void DeactivateForReaders_WhenAlreadyInactive_DoesNotThrow()
    {
        var project = ScrivenerProject.Create("My Novel", "/dropbox/MyNovel.scriv");

        var ex = Record.Exception(() => project.DeactivateForReaders());

        Assert.Null(ex);
    }

    // ---------------------------------------------------------------------------
    // UpdateSyncStatus
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateSyncStatus_ToHealthy_ClearsSyncErrorMessage()
    {
        var project = ScrivenerProject.Create("My Novel", "/dropbox/MyNovel.scriv");
        var syncTime = DateTime.UtcNow;

        project.UpdateSyncStatus(SyncStatus.Healthy, syncTime, null);

        Assert.Equal(SyncStatus.Healthy, project.SyncStatus);
        Assert.Null(project.SyncErrorMessage);
        Assert.Equal(syncTime, project.LastSyncedAt);
    }

    [Fact]
    public void UpdateSyncStatus_ToStale_ClearsSyncErrorMessage()
    {
        var project = ScrivenerProject.Create("My Novel", "/dropbox/MyNovel.scriv");

        project.UpdateSyncStatus(SyncStatus.Stale, DateTime.UtcNow, null);

        Assert.Equal(SyncStatus.Stale, project.SyncStatus);
        Assert.Null(project.SyncErrorMessage);
    }

    [Fact]
    public void UpdateSyncStatus_ToError_WithMessage_SetsSyncErrorMessage()
    {
        var project = ScrivenerProject.Create("My Novel", "/dropbox/MyNovel.scriv");

        project.UpdateSyncStatus(SyncStatus.Error, DateTime.UtcNow, "File not found.");

        Assert.Equal(SyncStatus.Error, project.SyncStatus);
        Assert.Equal("File not found.", project.SyncErrorMessage);
    }

    [Fact]
    public void UpdateSyncStatus_ToError_WithoutMessage_ThrowsInvariantViolationException()
    {
        var project = ScrivenerProject.Create("My Novel", "/dropbox/MyNovel.scriv");

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
        var project = ScrivenerProject.Create("My Novel", "/dropbox/MyNovel.scriv");
        var before = DateTime.UtcNow;

        project.SoftDelete();

        Assert.True(project.IsSoftDeleted);
        Assert.NotNull(project.SoftDeletedAt);
        Assert.True(project.SoftDeletedAt >= before);
    }

    [Fact]
    public void SoftDelete_DeactivatesReadersWhenActive()
    {
        var project = ScrivenerProject.Create("My Novel", "/dropbox/MyNovel.scriv");
        project.ActivateForReaders();

        project.SoftDelete();

        Assert.False(project.IsReaderActive);
        Assert.True(project.IsSoftDeleted);
    }

    [Fact]
    public void SoftDelete_WhenAlreadyDeleted_DoesNotChangeSoftDeletedAt()
    {
        var project = ScrivenerProject.Create("My Novel", "/dropbox/MyNovel.scriv");
        project.SoftDelete();
        var firstDeletion = project.SoftDeletedAt;

        project.SoftDelete();

        Assert.Equal(firstDeletion, project.SoftDeletedAt);
    }
}
