using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Persistence.Repositories;

namespace DraftView.Infrastructure.Tests.Persistence;

public class ScrivenerProjectRepositoryTests : IDisposable
{
    private readonly DraftViewDbContext _db;
    private readonly ScrivenerProjectRepository _sut;

    public ScrivenerProjectRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db  = new DraftViewDbContext(options);
        _sut = new ScrivenerProjectRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private static ScrivenerProject MakeProject(string name, string uuid) =>
        ScrivenerProject.Create(name, "/Apps/Scrivener/Test.scriv", uuid);

    // ---------------------------------------------------------------------------
    // AddAsync - happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_NewProject_AddsSuccessfully()
    {
        var project = MakeProject("Book 1", "UUID-001");

        await _sut.AddAsync(project);
        await _db.SaveChangesAsync();

        var found = await _sut.GetByScrivenerRootUuidAsync("UUID-001");
        Assert.NotNull(found);
        Assert.Equal("Book 1", found!.Name);
    }

    [Fact]
    public async Task AddAsync_ProjectWithNullUuid_AddsSuccessfully()
    {
        var project = ScrivenerProject.Create("No UUID", "/Apps/Scrivener/Test.scriv");

        await _sut.AddAsync(project);
        await _db.SaveChangesAsync();

        var all = await _sut.GetAllAsync();
        Assert.Single(all);
    }

    [Fact]
    public async Task AddAsync_TwoProjectsWithNullUuid_BothAddSuccessfully()
    {
        var p1 = ScrivenerProject.Create("No UUID 1", "/Apps/Scrivener/Test.scriv");
        var p2 = ScrivenerProject.Create("No UUID 2", "/Apps/Scrivener/Test2.scriv");

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

        Assert.Equal("UUID-CHECK", ex.ScrivenerRootUuid);
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
    // GetByScrivenerRootUuidAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetByScrivenerRootUuidAsync_ExistingUuid_ReturnsProject()
    {
        var project = MakeProject("Book 1", "UUID-GET");
        await _sut.AddAsync(project);
        await _db.SaveChangesAsync();

        var found = await _sut.GetByScrivenerRootUuidAsync("UUID-GET");

        Assert.NotNull(found);
        Assert.Equal("Book 1", found!.Name);
    }

    [Fact]
    public async Task GetByScrivenerRootUuidAsync_UnknownUuid_ReturnsNull()
    {
        var found = await _sut.GetByScrivenerRootUuidAsync("UUID-MISSING");
        Assert.Null(found);
    }

    [Fact]
    public async Task GetByScrivenerRootUuidAsync_SoftDeletedProject_ReturnsNull()
    {
        var project = MakeProject("Book 1", "UUID-DELETED");
        await _sut.AddAsync(project);
        await _db.SaveChangesAsync();

        project.SoftDelete();
        await _db.SaveChangesAsync();

        var found = await _sut.GetByScrivenerRootUuidAsync("UUID-DELETED");
        Assert.Null(found);
    }
}
