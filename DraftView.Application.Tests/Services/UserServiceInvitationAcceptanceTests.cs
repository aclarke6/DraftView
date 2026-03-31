using Microsoft.Extensions.Configuration;
using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

public class UserServiceInvitationAcceptanceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IInvitationRepository> _inviteRepo = new();
    private readonly Mock<IUserNotificationPreferencesRepository> _prefsRepo = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IConfiguration> _config = new();

    private UserService CreateSut() => new(
        _userRepo.Object,
        _inviteRepo.Object,
        _prefsRepo.Object,
        _emailSender.Object,
        _unitOfWork.Object,
        _config.Object);

    [Fact]
    public async Task AcceptInvitationAsync_ValidToken_PersistsEnteredDisplayName()
    {
        var user = User.Create("reader@example.com", "Pending", Role.BetaReader);
        var invitation = Invitation.CreateAlwaysOpen(user.Id);
        var sut = CreateSut();

        _inviteRepo.Setup(r => r.GetByTokenAsync(invitation.Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await sut.AcceptInvitationAsync(invitation.Token, "Reader Four", CancellationToken.None);

        Assert.Same(user, result);
        Assert.Equal("Reader Four", user.DisplayName);
        Assert.True(user.IsActive);
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptInvitationAsync_BlankDisplayName_ThrowsInvariantViolationException()
    {
        var user = User.Create("reader@example.com", "Pending", Role.BetaReader);
        var invitation = Invitation.CreateAlwaysOpen(user.Id);
        var sut = CreateSut();

        _inviteRepo.Setup(r => r.GetByTokenAsync(invitation.Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var ex = await Assert.ThrowsAsync<InvariantViolationException>(() =>
            sut.AcceptInvitationAsync(invitation.Token, "   ", CancellationToken.None));

        Assert.Equal("I-DISPLAYNAME", ex.InvariantCode);
    }
}
