using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for ReaderManagementService.GetReaderSummaryAsync.
/// Covers: soft-deleted exclusion, status derivation (Active/Invited/Inactive),
/// display name defaulting, alphabetical ordering.
/// Excludes: EF Core persistence, invitation issuance, reader deactivation.
/// </summary>
public class ReaderManagementServiceTests
{
    private readonly Mock<IUserRepository>       _userRepo       = new();
    private readonly Mock<IInvitationRepository> _invitationRepo = new();

    private ReaderManagementService CreateSut() => new(
        _userRepo.Object,
        _invitationRepo.Object);

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
    public async Task GetReaderSummaryAsync_BlankDisplayName_DefaultsToPendingReader()
    {
        var reader = User.Create("reader@example.test", string.Empty, Role.BetaReader);

        _userRepo.Setup(r => r.GetAllBetaReadersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([reader]);
        SetupNoPendingInvitations(reader.Id);

        var result = await CreateSut().GetReaderSummaryAsync();

        Assert.Equal("Pending reader", result[0].DisplayName);
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
}
