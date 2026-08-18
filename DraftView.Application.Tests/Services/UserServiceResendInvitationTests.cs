using Microsoft.Extensions.Configuration;
using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

public class UserServiceResendInvitationTests
{
    private readonly Mock<IUserRepository>              _userRepo       = new();
    private readonly Mock<IInvitationRepository>        _inviteRepo     = new();
    private readonly Mock<IUserPreferencesRepository>   _prefsRepo      = new();
    private readonly Mock<IEmailSender>                 _emailSender    = new();
    private readonly Mock<IUnitOfWork>                  _unitOfWork     = new();
    private readonly Mock<IConfiguration>               _config         = new();
    private readonly Mock<IReaderAccessRepository>      _readerAccess   = new();
    private readonly Mock<IAuthorizationFacade>         _authFacade     = new();
    private readonly Mock<IAuthorNotificationRepository> _notifRepo     = new();

    public UserServiceResendInvitationTests()
    {
        _config.Setup(c => c["App:BaseUrl"]).Returns("https://app.draftview.co.uk");
        _authFacade.Setup(f => f.IsAuthor()).Returns(true);
        _authFacade.Setup(f => f.IsSystemSupport()).Returns(false);
        _inviteRepo.Setup(r => r.GetPendingByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _prefsRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreferences?)null);
    }

    private UserService CreateSut() => new(
        _userRepo.Object,
        _inviteRepo.Object,
        _prefsRepo.Object,
        _emailSender.Object,
        _unitOfWork.Object,
        _config.Object,
        _readerAccess.Object,
        _authFacade.Object,
        _notifRepo.Object);

    [Fact]
    public async Task ResendInvitationAsync_ReaderNotFound_ThrowsInvariantViolation()
    {
        var userId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateSut().ResendInvitationAsync(userId, Guid.NewGuid()));

        Assert.Equal("U-RESEND-NOT-FOUND", ex.InvariantCode);
    }

    [Fact]
    public async Task ResendInvitationAsync_ActiveReader_DeactivatesBeforeReissuing()
    {
        var authorId = Guid.NewGuid();
        var reader   = User.Create("r@example.test", "Alice", Role.BetaReader);
        reader.Activate();

        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reader);
        _userRepo.Setup(r => r.GetByEmailAsync(reader.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _prefsRepo.Setup(r => r.GetByUserIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserPreferences.CreateForBetaReader(reader.Id));

        await CreateSut().ResendInvitationAsync(reader.Id, authorId);

        Assert.False(reader.IsActive);
        _emailSender.Verify(e => e.SendAsync(
            reader.Email,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendInvitationAsync_WithPendingInvitation_UsesExistingPolicy()
    {
        var authorId  = Guid.NewGuid();
        var reader    = User.Create("r@example.test", "Alice", Role.BetaReader);
        var expiresAt = DateTime.UtcNow.AddDays(14);
        var pending   = Invitation.CreateWithExpiry(reader.Id, expiresAt);

        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reader);
        _userRepo.Setup(r => r.GetByEmailAsync(reader.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _inviteRepo.Setup(r => r.GetPendingByUserIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pending]);

        await CreateSut().ResendInvitationAsync(reader.Id, authorId);

        _emailSender.Verify(e => e.SendAsync(
            reader.Email,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains(expiresAt.ToString("d MMMM yyyy"))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendInvitationAsync_NoPendingInvitation_DefaultsToAlwaysOpen()
    {
        var authorId = Guid.NewGuid();
        var reader   = User.Create("r@example.test", "Alice", Role.BetaReader);

        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reader);
        _userRepo.Setup(r => r.GetByEmailAsync(reader.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _inviteRepo.Setup(r => r.GetPendingByUserIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateSut().ResendInvitationAsync(reader.Id, authorId);

        _emailSender.Verify(e => e.SendAsync(
            reader.Email,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("does not expire")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
