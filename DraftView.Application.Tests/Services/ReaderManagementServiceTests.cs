using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for ReaderManagementService: GetReaderSummaryAsync, GetReaderAccessDataAsync,
/// UpdateReaderAccessAsync, SoftDeleteReaderAsync.
/// Excludes: EF Core persistence, invitation issuance, reader deactivation.
/// </summary>
public class ReaderManagementServiceTests
{
    private readonly Mock<IUserRepository>        _userRepo            = new();
    private readonly Mock<IInvitationRepository>  _invitationRepo      = new();
    private readonly Mock<IProjectRepository>     _projectRepo         = new();
    private readonly Mock<IReaderAccessRepository> _readerAccessRepo   = new();
    private readonly Mock<IUserService>            _userService         = new();
    private readonly Mock<IUnitOfWork>             _unitOfWork          = new();

    private ReaderManagementService CreateSut() => new(
        _userRepo.Object,
        _invitationRepo.Object,
        _projectRepo.Object,
        _readerAccessRepo.Object,
        _userService.Object,
        _unitOfWork.Object);

    private void SetupNoPendingInvitations(Guid userId)
    {
        _invitationRepo.Setup(r => r.GetPendingByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public async Task GetReaderSummaryAsync_SoftDeletedReader_IsExcluded()
    {
        var reader = User.Create("reader@example.test", "Alice", Role.BetaReader);
        reader.SoftDelete();

        _userRepo.Setup(r => r.GetAllBetaReadersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([reader]);

        var result = await CreateSut().GetReaderSummaryAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetReaderSummaryAsync_ActiveReader_GetsActiveStatus()
    {
        var reader = User.Create("reader@example.test", "Alice", Role.BetaReader);
        reader.Activate();

        _userRepo.Setup(r => r.GetAllBetaReadersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([reader]);
        SetupNoPendingInvitations(reader.Id);

        var result = await CreateSut().GetReaderSummaryAsync();

        Assert.Equal(ReaderStatus.Active, Assert.Single(result).Status);
    }

    [Fact]
    public async Task GetReaderSummaryAsync_InactiveReaderWithPendingInvitation_GetsInvitedStatus()
    {
        var reader = User.Create("reader@example.test", "Alice", Role.BetaReader);
        var invitation = Invitation.CreateAlwaysOpen(reader.Id);

        _userRepo.Setup(r => r.GetAllBetaReadersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([reader]);
        _invitationRepo.Setup(r => r.GetPendingByUserIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([invitation]);

        var result = await CreateSut().GetReaderSummaryAsync();

        Assert.Equal(ReaderStatus.Invited, Assert.Single(result).Status);
        Assert.True(result[0].HasPendingInvitation);
    }

    [Fact]
    public async Task GetReaderSummaryAsync_InactiveReaderWithNoPendingInvitation_GetsInactiveStatus()
    {
        var reader = User.Create("reader@example.test", "Alice", Role.BetaReader);

        _userRepo.Setup(r => r.GetAllBetaReadersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([reader]);
        SetupNoPendingInvitations(reader.Id);

        var result = await CreateSut().GetReaderSummaryAsync();

        Assert.Equal(ReaderStatus.Inactive, Assert.Single(result).Status);
        Assert.False(result[0].HasPendingInvitation);
    }

    [Fact]
    public async Task GetReaderSummaryAsync_MultipleReaders_OrderedByDisplayName()
    {
        var charlie = User.Create("c@example.test", "Charlie", Role.BetaReader);
        var alice   = User.Create("a@example.test", "Alice",   Role.BetaReader);
        var bob     = User.Create("b@example.test", "Bob",     Role.BetaReader);

        _userRepo.Setup(r => r.GetAllBetaReadersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([charlie, alice, bob]);
        SetupNoPendingInvitations(charlie.Id);
        SetupNoPendingInvitations(alice.Id);
        SetupNoPendingInvitations(bob.Id);

        var result = await CreateSut().GetReaderSummaryAsync();

        Assert.Equal(["Alice", "Bob", "Charlie"], result.Select(r => r.DisplayName));
    }

    // -----------------------------------------------------------------------
    // GetReaderAccessDataAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetReaderAccessDataAsync_ReaderNotFound_ReturnsNull()
    {
        var readerId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(readerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateSut().GetReaderAccessDataAsync(readerId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetReaderAccessDataAsync_ActiveReader_ReturnsActiveStatus()
    {
        var reader  = User.Create("r@example.test", "Alice", Role.BetaReader);
        reader.Activate();
        var project = Project.Create("Book", "/path", Guid.NewGuid(), "root");

        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reader);
        _projectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([project]);
        _readerAccessRepo.Setup(r => r.GetByReaderIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _invitationRepo.Setup(r => r.GetByUserIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invitation?)null);

        var result = await CreateSut().GetReaderAccessDataAsync(reader.Id);

        Assert.NotNull(result);
        Assert.Equal(ReaderStatus.Active, result!.Status);
    }

    [Fact]
    public async Task GetReaderAccessDataAsync_PartitionsProjectsCorrectly()
    {
        var authorId  = Guid.NewGuid();
        var reader    = User.Create("r@example.test", "Alice", Role.BetaReader);
        var projectA  = Project.Create("A", "/a", authorId, "root-a");
        var projectB  = Project.Create("B", "/b", authorId, "root-b");
        var access    = ReaderAccess.Grant(reader.Id, authorId, projectA.Id);

        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reader);
        _projectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([projectA, projectB]);
        _readerAccessRepo.Setup(r => r.GetByReaderIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([access]);
        _invitationRepo.Setup(r => r.GetByUserIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invitation?)null);

        var result = await CreateSut().GetReaderAccessDataAsync(reader.Id);

        Assert.NotNull(result);
        Assert.Single(result!.ProjectsWithAccess);
        Assert.Equal(projectA.Id, result.ProjectsWithAccess[0].Id);
        Assert.Single(result.ProjectsWithoutAccess);
        Assert.Equal(projectB.Id, result.ProjectsWithoutAccess[0].Id);
    }

    // -----------------------------------------------------------------------
    // UpdateReaderAccessAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UpdateReaderAccessAsync_GrantsNewAccess_AndSaves()
    {
        var authorId  = Guid.NewGuid();
        var readerId  = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(readerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReaderAccess?)null);

        await CreateSut().UpdateReaderAccessAsync(readerId, [projectId], [], authorId);

        _readerAccessRepo.Verify(r => r.AddAsync(It.IsAny<ReaderAccess>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateReaderAccessAsync_RevokesExistingAccess_AndSaves()
    {
        var authorId  = Guid.NewGuid();
        var readerId  = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var access    = ReaderAccess.Grant(readerId, authorId, projectId);

        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(readerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(access);

        await CreateSut().UpdateReaderAccessAsync(readerId, [], [projectId], authorId);

        Assert.False(access.IsActive);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -----------------------------------------------------------------------
    // SoftDeleteReaderAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SoftDeleteReaderAsync_RevokesAllAccessThenSoftDeletesUser()
    {
        var authorId = Guid.NewGuid();
        var userId   = Guid.NewGuid();

        await CreateSut().SoftDeleteReaderAsync(userId, authorId);

        _readerAccessRepo.Verify(
            r => r.RevokeAllForReaderAsync(userId, authorId, It.IsAny<CancellationToken>()),
            Times.Once);
        _userService.Verify(
            s => s.SoftDeleteUserAsync(userId, authorId, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
