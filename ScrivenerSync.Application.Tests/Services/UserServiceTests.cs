using Microsoft.Extensions.Configuration;
using Moq;
using ScrivenerSync.Application.Services;
using ScrivenerSync.Domain.Entities;
using ScrivenerSync.Domain.Enumerations;
using ScrivenerSync.Domain.Exceptions;
using ScrivenerSync.Domain.Interfaces.Repositories;
using ScrivenerSync.Domain.Interfaces.Services;

namespace ScrivenerSync.Application.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository>                       _userRepo    = new();
    private readonly Mock<IInvitationRepository>                 _inviteRepo  = new();
    private readonly Mock<IUserNotificationPreferencesRepository> _prefsRepo   = new();
    private readonly Mock<IEmailSender>                          _emailSender = new();
    private readonly Mock<IUnitOfWork>                           _unitOfWork  = new();
    private readonly Mock<IConfiguration>                        _config      = new();

    private UserService CreateSut() => new(
        _userRepo.Object,
        _inviteRepo.Object,
        _prefsRepo.Object,
        _emailSender.Object,
        _unitOfWork.Object,
        _config.Object);

    private static User MakeAuthor()
    {
        var u = User.Create("author@example.com", "Author", Role.Author);
        u.Activate();
        return u;
    }

    // ---------------------------------------------------------------------------
    // IssueInvitation
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task IssueInvitationAsync_ValidRequest_CreatesUserAndInvitation()
    {
        var author = MakeAuthor();
        var sut    = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        _userRepo.Setup(r => r.EmailExistsAsync("reader@example.com", default)).ReturnsAsync(false);

        User? addedUser         = null;
        Invitation? addedInvite = null;

        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), default))
            .Callback<User, CancellationToken>((u, _) => addedUser = u);
        _inviteRepo.Setup(r => r.AddAsync(It.IsAny<Invitation>(), default))
            .Callback<Invitation, CancellationToken>((i, _) => addedInvite = i);
        _prefsRepo.Setup(r => r.AddAsync(It.IsAny<UserNotificationPreferences>(), default));

        await sut.IssueInvitationAsync("reader@example.com", ExpiryPolicy.AlwaysOpen, null, author.Id);

        Assert.NotNull(addedUser);
        Assert.NotNull(addedInvite);
        Assert.Equal("reader@example.com", addedUser!.Email);
        _emailSender.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task IssueInvitationAsync_NonAuthor_ThrowsUnauthorised()
    {
        var reader = User.Create("reader@example.com", "Reader", Role.BetaReader);
        reader.Activate();
        var sut = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.IssueInvitationAsync("other@example.com", ExpiryPolicy.AlwaysOpen, null, reader.Id));
    }

    [Fact]
    public async Task IssueInvitationAsync_EmailAlreadyExists_ThrowsInvariantViolation()
    {
        var author = MakeAuthor();
        var sut    = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        _userRepo.Setup(r => r.EmailExistsAsync("existing@example.com", default)).ReturnsAsync(true);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.IssueInvitationAsync("existing@example.com", ExpiryPolicy.AlwaysOpen, null, author.Id));
    }

    // ---------------------------------------------------------------------------
    // AcceptInvitation
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AcceptInvitationAsync_ValidToken_ActivatesUser()
    {
        var user       = User.Create("reader@example.com", "Reader", Role.BetaReader);
        var invitation = Invitation.CreateAlwaysOpen(user.Id);
        var sut        = CreateSut();

        _inviteRepo.Setup(r => r.GetByTokenAsync(invitation.Token, default)).ReturnsAsync(invitation);
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        await sut.AcceptInvitationAsync(invitation.Token, "Reader Name", "hashedpassword");

        Assert.True(user.IsActive);
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
    }

    [Fact]
    public async Task AcceptInvitationAsync_InvalidToken_ThrowsEntityNotFoundException()
    {
        var sut = CreateSut();

        _inviteRepo.Setup(r => r.GetByTokenAsync("badtoken", default))
            .ReturnsAsync((Invitation?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.AcceptInvitationAsync("badtoken", "Name", "hash"));
    }

    [Fact]
    public async Task AcceptInvitationAsync_CancelledInvitation_ThrowsInvariantViolation()
    {
        var user       = User.Create("reader@example.com", "Reader", Role.BetaReader);
        var invitation = Invitation.CreateAlwaysOpen(user.Id);
        invitation.Cancel();
        var sut = CreateSut();

        _inviteRepo.Setup(r => r.GetByTokenAsync(invitation.Token, default)).ReturnsAsync(invitation);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.AcceptInvitationAsync(invitation.Token, "Name", "hash"));
    }

    // ---------------------------------------------------------------------------
    // DeactivateUser
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeactivateUserAsync_Author_DeactivatesReader()
    {
        var author = MakeAuthor();
        var reader = User.Create("reader@example.com", "Reader", Role.BetaReader);
        reader.Activate();
        var sut = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);

        await sut.DeactivateUserAsync(reader.Id, author.Id);

        Assert.False(reader.IsActive);
    }

    // ---------------------------------------------------------------------------
    // SoftDeleteUser
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SoftDeleteUserAsync_Author_SoftDeletesReader()
    {
        var author = MakeAuthor();
        var reader = User.Create("reader@example.com", "Reader", Role.BetaReader);
        reader.Activate();
        var sut = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);

        await sut.SoftDeleteUserAsync(reader.Id, author.Id);

        Assert.True(reader.IsSoftDeleted);
    }
}
