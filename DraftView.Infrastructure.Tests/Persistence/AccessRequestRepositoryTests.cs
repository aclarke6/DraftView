using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Persistence.Repositories;

namespace DraftView.Infrastructure.Tests.Persistence;

public class AccessRequestRepositoryTests : IDisposable
{
    private static readonly Guid ReaderA   = Guid.NewGuid();
    private static readonly Guid ReaderB   = Guid.NewGuid();
    private static readonly Guid ProjectX  = Guid.NewGuid();
    private static readonly Guid ProjectY  = Guid.NewGuid();

    private readonly DraftViewDbContext _db;
    private readonly AccessRequestRepository _sut;

    public AccessRequestRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new DraftViewDbContext(options);
        _sut = new AccessRequestRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private static AccessRequest Pending(Guid readerId, Guid projectId, string? coverNote = null) =>
        AccessRequest.Create(readerId, projectId, coverNote, null);

    private static AccessRequest Declined(Guid readerId, Guid projectId)
    {
        var r = AccessRequest.Create(readerId, projectId, null, null);
        r.Decline();
        return r;
    }

    private static AccessRequest Approved(Guid readerId, Guid projectId)
    {
        var r = AccessRequest.Create(readerId, projectId, null, null);
        r.Approve();
        return r;
    }

    // -----------------------------------------------------------------------
    // AddAsync / GetByIdAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_ThenGetById_RoundTrips()
    {
        var req = Pending(ReaderA, ProjectX, "Hello!");
        await _sut.AddAsync(req);
        await _db.SaveChangesAsync();

        var found = await _sut.GetByIdAsync(req.Id);

        Assert.NotNull(found);
        Assert.Equal(ReaderA, found!.ReaderId);
        Assert.Equal(ProjectX, found.ProjectId);
        Assert.Equal("Hello!", found.CoverNote);
        Assert.Equal(AccessRequestStatus.Pending, found.Status);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        var found = await _sut.GetByIdAsync(Guid.NewGuid());
        Assert.Null(found);
    }

    // -----------------------------------------------------------------------
    // GetPendingByProjectIdAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetPendingByProjectIdAsync_ReturnsOnlyPendingForProject()
    {
        await _sut.AddAsync(Pending(ReaderA, ProjectX));
        await _sut.AddAsync(Pending(ReaderB, ProjectX));
        await _sut.AddAsync(Declined(ReaderA, ProjectY));  // different project
        await _db.SaveChangesAsync();

        var results = await _sut.GetPendingByProjectIdAsync(ProjectX);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(ProjectX, r.ProjectId));
        Assert.All(results, r => Assert.Equal(AccessRequestStatus.Pending, r.Status));
    }

    [Fact]
    public async Task GetPendingByProjectIdAsync_ExcludesDeclinedAndApproved()
    {
        await _sut.AddAsync(Pending(ReaderA, ProjectX));
        await _sut.AddAsync(Declined(ReaderB, ProjectX));
        await _sut.AddAsync(Approved(Guid.NewGuid(), ProjectX));
        await _db.SaveChangesAsync();

        var results = await _sut.GetPendingByProjectIdAsync(ProjectX);

        Assert.Single(results);
        Assert.Equal(ReaderA, results[0].ReaderId);
    }

    // -----------------------------------------------------------------------
    // GetPendingCountByProjectIdAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetPendingCountByProjectIdAsync_ReturnsCorrectCount()
    {
        await _sut.AddAsync(Pending(ReaderA, ProjectX));
        await _sut.AddAsync(Pending(ReaderB, ProjectX));
        await _sut.AddAsync(Declined(Guid.NewGuid(), ProjectX));
        await _db.SaveChangesAsync();

        var count = await _sut.GetPendingCountByProjectIdAsync(ProjectX);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetPendingCountByProjectIdAsync_WhenNone_ReturnsZero()
    {
        var count = await _sut.GetPendingCountByProjectIdAsync(Guid.NewGuid());
        Assert.Equal(0, count);
    }

    // -----------------------------------------------------------------------
    // GetVisibleByReaderIdAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetVisibleByReaderIdAsync_IncludesPending()
    {
        await _sut.AddAsync(Pending(ReaderA, ProjectX));
        await _db.SaveChangesAsync();

        var results = await _sut.GetVisibleByReaderIdAsync(ReaderA, DateTime.UtcNow);

        Assert.Single(results);
    }

    [Fact]
    public async Task GetVisibleByReaderIdAsync_IncludesDeclinedNotYetSeen()
    {
        await _sut.AddAsync(Declined(ReaderA, ProjectX));
        await _db.SaveChangesAsync();

        var results = await _sut.GetVisibleByReaderIdAsync(ReaderA, DateTime.UtcNow);

        Assert.Single(results);
    }

    [Fact]
    public async Task GetVisibleByReaderIdAsync_ExcludesApproved()
    {
        await _sut.AddAsync(Approved(ReaderA, ProjectX));
        await _db.SaveChangesAsync();

        var results = await _sut.GetVisibleByReaderIdAsync(ReaderA, DateTime.UtcNow);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetVisibleByReaderIdAsync_ExcludesDeclinedSeenYesterday()
    {
        var req = Declined(ReaderA, ProjectX);
        req.MarkSeenByReader();
        await _sut.AddAsync(req);
        await _db.SaveChangesAsync();

        var tomorrow = DateTime.UtcNow.AddDays(1);
        var results = await _sut.GetVisibleByReaderIdAsync(ReaderA, tomorrow);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetVisibleByReaderIdAsync_IncludesDeclinedSeenToday()
    {
        var req = Declined(ReaderA, ProjectX);
        req.MarkSeenByReader();
        await _sut.AddAsync(req);
        await _db.SaveChangesAsync();

        var today = DateTime.UtcNow;
        var results = await _sut.GetVisibleByReaderIdAsync(ReaderA, today);

        Assert.Single(results);
    }

    [Fact]
    public async Task GetVisibleByReaderIdAsync_ExcludesOtherReadersRequests()
    {
        await _sut.AddAsync(Pending(ReaderA, ProjectX));
        await _sut.AddAsync(Pending(ReaderB, ProjectX));
        await _db.SaveChangesAsync();

        var results = await _sut.GetVisibleByReaderIdAsync(ReaderA, DateTime.UtcNow);

        Assert.Single(results);
        Assert.Equal(ReaderA, results[0].ReaderId);
    }

    // -----------------------------------------------------------------------
    // SaveAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_PersistsStatusChange()
    {
        var req = Pending(ReaderA, ProjectX);
        await _sut.AddAsync(req);
        await _db.SaveChangesAsync();

        req.Approve();
        await _sut.SaveAsync(req);
        await _db.SaveChangesAsync();

        var found = await _sut.GetByIdAsync(req.Id);
        Assert.NotNull(found);
        Assert.Equal(AccessRequestStatus.Approved, found!.Status);
        Assert.NotNull(found.RespondedAt);
    }

    // -----------------------------------------------------------------------
    // BulkDeclineByProjectAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BulkDeclineByProjectAsync_DeclinesPendingRequests()
    {
        await _sut.AddAsync(Pending(ReaderA, ProjectX));
        await _sut.AddAsync(Pending(ReaderB, ProjectX));
        await _db.SaveChangesAsync();

        var respondedAt = DateTime.UtcNow;
        await _sut.BulkDeclineByProjectAsync(ProjectX, respondedAt);
        await _db.SaveChangesAsync();

        var pending = await _sut.GetPendingByProjectIdAsync(ProjectX);
        Assert.Empty(pending);

        var all = await _db.AccessRequests
            .Where(r => r.ProjectId == ProjectX)
            .ToListAsync();
        Assert.All(all, r => Assert.Equal(AccessRequestStatus.Declined, r.Status));
        Assert.All(all, r => Assert.NotNull(r.RespondedAt));
    }

    [Fact]
    public async Task BulkDeclineByProjectAsync_DoesNotAffectOtherProjects()
    {
        await _sut.AddAsync(Pending(ReaderA, ProjectX));
        await _sut.AddAsync(Pending(ReaderB, ProjectY));
        await _db.SaveChangesAsync();

        await _sut.BulkDeclineByProjectAsync(ProjectX, DateTime.UtcNow);
        await _db.SaveChangesAsync();

        var yPending = await _sut.GetPendingByProjectIdAsync(ProjectY);
        Assert.Single(yPending);
    }

    [Fact]
    public async Task BulkDeclineByProjectAsync_DoesNotChangeAlreadyDeclined()
    {
        var already = Declined(ReaderA, ProjectX);
        var originalRespondedAt = already.RespondedAt;
        await _sut.AddAsync(already);
        await _db.SaveChangesAsync();

        await _sut.BulkDeclineByProjectAsync(ProjectX, DateTime.UtcNow.AddSeconds(1));
        await _db.SaveChangesAsync();

        var found = await _sut.GetByIdAsync(already.Id);
        Assert.Equal(originalRespondedAt, found!.RespondedAt);
    }

    // -----------------------------------------------------------------------
    // MarkDeclinedAsSeenAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MarkDeclinedAsSeenAsync_SetsSeenByReaderAtForUnseenDeclined()
    {
        var req = Declined(ReaderA, ProjectX);
        await _sut.AddAsync(req);
        await _db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        await _sut.MarkDeclinedAsSeenAsync(ReaderA, now);
        await _db.SaveChangesAsync();

        var found = await _sut.GetByIdAsync(req.Id);
        Assert.NotNull(found!.SeenByReaderAt);
    }

    [Fact]
    public async Task MarkDeclinedAsSeenAsync_DoesNotOverwriteAlreadySeen()
    {
        var req = Declined(ReaderA, ProjectX);
        req.MarkSeenByReader();
        var original = req.SeenByReaderAt;
        await _sut.AddAsync(req);
        await _db.SaveChangesAsync();

        await _sut.MarkDeclinedAsSeenAsync(ReaderA, DateTime.UtcNow.AddSeconds(1));
        await _db.SaveChangesAsync();

        var found = await _sut.GetByIdAsync(req.Id);
        Assert.Equal(original, found!.SeenByReaderAt);
    }

    [Fact]
    public async Task MarkDeclinedAsSeenAsync_DoesNotAffectPendingRequests()
    {
        await _sut.AddAsync(Pending(ReaderA, ProjectX));
        await _db.SaveChangesAsync();

        await _sut.MarkDeclinedAsSeenAsync(ReaderA, DateTime.UtcNow);
        await _db.SaveChangesAsync();

        var results = await _sut.GetVisibleByReaderIdAsync(ReaderA, DateTime.UtcNow);
        Assert.Single(results);
        Assert.Null(results[0].SeenByReaderAt);
    }
}
