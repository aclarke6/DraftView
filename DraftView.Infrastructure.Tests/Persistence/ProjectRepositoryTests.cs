using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Persistence.Repositories;

namespace DraftView.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests ProjectRepository persistence contracts and Project EF model configuration.
/// Excludes sync orchestration behaviour, which belongs in application service tests.
/// </summary>
public class ProjectRepositoryTests : IDisposable
{
    private static readonly Guid ValidAuthorId = Guid.NewGuid();

    private readonly DraftViewDbContext _db;
    private readonly ProjectRepository _sut;

    public ProjectRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db  = new DraftViewDbContext(options);
        _sut = new ProjectRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private static Project MakeProject(string name, string uuid) =>
        Project.Create(name, "/Apps/Scrivener/Test.scriv", ValidAuthorId, uuid);

    // ---------------------------------------------------------------------------
    // AddAsync - happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_NewProject_AddsSuccessfully()
    {
        var project = MakeProject("Book 1", "UUID-001");

        await _sut.AddAsync(project);
        await _db.SaveChangesAsync();

        var found = await _sut.GetBySyncRootIdAsync("UUID-001");
        Assert.NotNull(found);
        Assert.Equal("Book 1", found!.Name);
    }

    [Fact]
    public async Task AddAsync_ProjectWithNullUuid_AddsSuccessfully()
    {
        var project = Project.Create("No UUID", "/Apps/Scrivener/Test.scriv", ValidAuthorId);

        await _sut.AddAsync(project);
        await _db.SaveChangesAsync();

        var all = await _sut.GetAllAsync();
        Assert.Single(all);
    }

    [Fact]
    public void ModelConfiguration_WebhookSyncControlFields_AreNullableWithBoundedOutcome()
    {
        var projectType = _db.Model.FindEntityType(typeof(Project));

        Assert.NotNull(projectType);
        Assert.True(projectType!.FindProperty(nameof(Project.SyncRequestedUtc))!.IsNullable);
        Assert.True(projectType.FindProperty(nameof(Project.LastWebhookUtc))!.IsNullable);
        Assert.True(projectType.FindProperty(nameof(Project.HeldUntilUtc))!.IsNullable);
        Assert.True(projectType.FindProperty(nameof(Project.LastSuccessfulSyncUtc))!.IsNullable);
        Assert.True(projectType.FindProperty(nameof(Project.LastSyncAttemptUtc))!.IsNullable);
        Assert.True(projectType.FindProperty(nameof(Project.SyncLeaseId))!.IsNullable);
        Assert.True(projectType.FindProperty(nameof(Project.SyncLeaseExpiresUtc))!.IsNullable);

        var outcome = projectType.FindProperty(nameof(Project.LastBackgroundSyncOutcome));
        Assert.NotNull(outcome);
        Assert.True(outcome!.IsNullable);
        Assert.Equal(500, outcome.GetMaxLength());
    }

    [Fact]
    public async Task AddAsync_TwoProjectsWithNullUuid_BothAddSuccessfully()
    {
        var p1 = Project.Create("No UUID 1", "/Apps/Scrivener/Test.scriv", ValidAuthorId);
        var p2 = Project.Create("No UUID 2", "/Apps/Scrivener/Test2.scriv", ValidAuthorId);

        await _sut.AddAsync(p1);
        await _db.SaveChangesAsync();
        await _sut.AddAsync(p2);
        await _db.SaveChangesAsync();

        var all = await _sut.GetAllAsync();
        Assert.Equal(2, all.Count);
    }

    // ---------------------------------------------------------------------------
    // AddAsync - duplicate guard
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_DuplicateUuid_ThrowsDuplicateProjectException()
    {
        var p1 = MakeProject("Book 1", "UUID-DUPE");
        await _sut.AddAsync(p1);
        await _db.SaveChangesAsync();

        var p2 = MakeProject("Book 1 Again", "UUID-DUPE");

        await Assert.ThrowsAsync<DuplicateProjectException>(
            () => _sut.AddAsync(p2));
    }

    [Fact]
    public async Task AddAsync_DuplicateUuid_ExceptionContainsUuid()
    {
        var p1 = MakeProject("Book 1", "UUID-CHECK");
        await _sut.AddAsync(p1);
        await _db.SaveChangesAsync();

        var p2 = MakeProject("Book 1 Copy", "UUID-CHECK");

        var ex = await Assert.ThrowsAsync<DuplicateProjectException>(
            () => _sut.AddAsync(p2));

        Assert.Equal("UUID-CHECK", ex.SyncRootId);
    }

    [Fact]
    public async Task AddAsync_DuplicateUuid_DoesNotPersistDuplicate()
    {
        var p1 = MakeProject("Book 1", "UUID-NOPERSIST");
        await _sut.AddAsync(p1);
        await _db.SaveChangesAsync();

        var p2 = MakeProject("Book 1 Copy", "UUID-NOPERSIST");
        try { await _sut.AddAsync(p2); } catch (DuplicateProjectException) { }

        var all = await _sut.GetAllAsync();
        Assert.Single(all);
    }

    // ---------------------------------------------------------------------------
    // GetBySyncRootIdAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetBySyncRootIdAsync_ExistingUuid_ReturnsProject()
    {
        var project = MakeProject("Book 1", "UUID-GET");
        await _sut.AddAsync(project);
        await _db.SaveChangesAsync();

        var found = await _sut.GetBySyncRootIdAsync("UUID-GET");

        Assert.NotNull(found);
        Assert.Equal("Book 1", found!.Name);
    }

    [Fact]
    public async Task GetBySyncRootIdAsync_UnknownUuid_ReturnsNull()
    {
        var found = await _sut.GetBySyncRootIdAsync("UUID-MISSING");
        Assert.Null(found);
    }

    [Fact]
    public async Task GetBySyncRootIdAsync_SoftDeletedProject_ReturnsNull()
    {
        var project = MakeProject("Book 1", "UUID-DELETED");
        await _sut.AddAsync(project);
        await _db.SaveChangesAsync();

        project.SoftDelete();
        await _db.SaveChangesAsync();

        var found = await _sut.GetBySyncRootIdAsync("UUID-DELETED");
        Assert.Null(found);
    }
}
