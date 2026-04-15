using Microsoft.Extensions.Configuration;
using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository>                       UserRepo    = new();
    private readonly Mock<IInvitationRepository>                 InviteRepo  = new();
    private readonly Mock<IUserPreferencesRepository> PrefsRepo   = new();
    private readonly Mock<IEmailSender>                          EmailSender = new();
    private readonly Mock<IUnitOfWork>                           UnitOfWork  = new();
    private readonly Mock<IConfiguration>                        Config      = new();
    private readonly Mock<IReaderAccessRepository>               ReaderAccessRepo = new();
    private readonly Mock<IAuthorizationFacade>                  AuthFacade       = new();
    private readonly Mock<IAuthorNotificationRepository>         NotifRepo        = new();

    private UserService CreateSut() => new(
        UserRepo.Object,
        InviteRepo.Object,
        PrefsRepo.Object,
        EmailSender.Object,
        UnitOfWork.Object,
        Config.Object,
        ReaderAccessRepo.Object,
        AuthFacade.Object,
        NotifRepo.Object);

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

        UserRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        UserRepo.Setup(r => r.GetByEmailAsync("reader@example.com", default)).ReturnsAsync((User?)null);
        InviteRepo.Setup(r => r.GetPendingByUserIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync([]);

        User? addedUser         = null;
        Invitation? addedInvite = null;

        UserRepo.Setup(r => r.AddAsync(It.IsAny<User>(), default))
            .Callback<User, CancellationToken>((u, _) => addedUser = u);
        InviteRepo.Setup(r => r.AddAsync(It.IsAny<Invitation>(), default))
            .Callback<Invitation, CancellationToken>((i, _) => addedInvite = i);
        PrefsRepo.Setup(r => r.AddAsync(It.IsAny<UserPreferences>(), default));
        AuthFacade.Setup(f => f.IsAuthor()).Returns(true);

        await sut.IssueInvitationAsync("reader@example.com", ExpiryPolicy.AlwaysOpen, null, author.Id);

        Assert.NotNull(addedUser);
        Assert.NotNull(addedInvite);
        Assert.Equal("reader@example.com", addedUser!.Email);
        EmailSender.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task IssueInvitationAsync_NonAuthor_ThrowsUnauthorised()
    {
        var reader = User.Create("reader@example.com", "Reader", Role.BetaReader);
        reader.Activate();
        var sut = CreateSut();

        UserRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);
        AuthFacade.Setup(f => f.IsAuthor()).Returns(false);

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.IssueInvitationAsync("other@example.com", ExpiryPolicy.AlwaysOpen, null, reader.Id));
    }

    [Fact]
    public async Task IssueInvitationAsync_EmailAlreadyExists_ThrowsInvariantViolation()
    {
        var author = MakeAuthor();
        var sut    = CreateSut();

        var existingUser = User.Create("existing@example.com", "Existing User", Role.BetaReader);
        existingUser.Activate();

        UserRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        UserRepo.Setup(r => r.GetByEmailAsync("existing@example.com", default)).ReturnsAsync(existingUser);
        AuthFacade.Setup(f => f.IsAuthor()).Returns(true);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.IssueInvitationAsync("existing@example.com", ExpiryPolicy.AlwaysOpen, null, author.Id));
    }

    [Fact]
    public async Task IssueInvitationAsync_ExistingPendingInvitee_CancelsOlderPendingInvitesAndIssuesFreshToken()
    {
        var author = MakeAuthor();
        var existingUser = User.Create("reader@example.com", "Pending", Role.BetaReader);
        var olderPending = Invitation.CreateAlwaysOpen(existingUser.Id);
        var newerPending = Invitation.CreateAlwaysOpen(existingUser.Id);
        var sut = CreateSut();

        UserRepo.Setup(r => r.GetByEmailAsync("reader@example.com", default)).ReturnsAsync(existingUser);
        InviteRepo.Setup(r => r.GetPendingByUserIdAsync(existingUser.Id, default))
            .ReturnsAsync([olderPending, newerPending]);
        AuthFacade.Setup(f => f.IsAuthor()).Returns(true);

        var freshInvitation = await sut.IssueInvitationAsync("reader@example.com", ExpiryPolicy.AlwaysOpen, null, author.Id);

        Assert.Equal(InvitationStatus.Cancelled, olderPending.Status);
        Assert.Equal(InvitationStatus.Cancelled, newerPending.Status);
        Assert.Equal(existingUser.Id, freshInvitation.UserId);
        UserRepo.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Never);
        PrefsRepo.Verify(r => r.AddAsync(It.IsAny<UserPreferences>(), default), Times.Never);
        InviteRepo.Verify(r => r.AddAsync(It.Is<Invitation>(i => i.Id == freshInvitation.Id), default), Times.Once);
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

        InviteRepo.Setup(r => r.GetByTokenAsync(invitation.Token, default)).ReturnsAsync(invitation);
        UserRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        await sut.AcceptInvitationAsync(invitation.Token, "Reader Name");

        Assert.True(user.IsActive);
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
    }

    [Fact]
    public async Task AcceptInvitationAsync_InvalidToken_ThrowsEntityNotFoundException()
    {
        var sut = CreateSut();

        InviteRepo.Setup(r => r.GetByTokenAsync("badtoken", default))
            .ReturnsAsync((Invitation?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.AcceptInvitationAsync("badtoken", "Name"));
    }

    [Fact]
    public async Task AcceptInvitationAsync_CancelledInvitation_ThrowsInvariantViolation()
    {
        var user       = User.Create("reader@example.com", "Reader", Role.BetaReader);
        var invitation = Invitation.CreateAlwaysOpen(user.Id);
        invitation.Cancel();
        var sut = CreateSut();

        InviteRepo.Setup(r => r.GetByTokenAsync(invitation.Token, default)).ReturnsAsync(invitation);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.AcceptInvitationAsync(invitation.Token, "Name"));
    }

    // ---------------------------------------------------------------------------
    // DeactivateUser
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeactivateUserAsync_Author_DeactivatesReader()
    {
        var author = MakeAuthor();
        var reader = User.Create("reader@example.com", "Reader", Role.BetaReader);
        AuthFacade.Setup(f => f.IsAuthor()).Returns(true);
        reader.Activate();
        var sut = CreateSut();

        UserRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        UserRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);

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
        AuthFacade.Setup(f => f.IsAuthor()).Returns(true);
        var sut = CreateSut();

        UserRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        UserRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);

        await sut.SoftDeleteUserAsync(reader.Id, author.Id);

        Assert.True(reader.IsSoftDeleted);
    }

    [Fact]
    public async Task IssueInvitationAsync_WithExpiresAtPolicy_AndNullExpiry_ThrowsInvariantViolation()
    {
        var author = User.Create("author@example.com", "Author", Role.Author);
        author.Activate();
        var sut = CreateSut();

        UserRepo.Setup(r => r.GetByIdAsync(author.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        UserRepo.Setup(r => r.GetByEmailAsync("reader@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        AuthFacade.Setup(f => f.IsAuthor()).Returns(true);

        var ex = await Assert.ThrowsAsync<InvariantViolationException>(() =>
            sut.IssueInvitationAsync(
                "reader@example.com",
                ExpiryPolicy.ExpiresAt,
                null,
                author.Id,
                CancellationToken.None));

        Assert.Equal("I-INVITE-EXPIRY-REQUIRED", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // DeactivateUserAsync -- revokes ReaderAccess
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeactivateUserAsync_RevokesAllReaderAccess()
    {
        var author = MakeAuthor();
        var reader = User.Create("reader@test.com", "Reader", Role.BetaReader);
        AuthFacade.Setup(f => f.IsAuthor()).Returns(true);
        reader.Activate();

        UserRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        UserRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);

        var sut = CreateSut();
        await sut.DeactivateUserAsync(reader.Id, author.Id);

        ReaderAccessRepo.Verify(
            r => r.RevokeAllForReaderAsync(reader.Id, author.Id, default),
            Times.Once);
    }

    [Fact]
    public async Task UpdateDisplayThemeAsync_ValidUser_UpdatesThemeAndSaves()
    {
        var user = User.Create("reader@example.com", "Reader", Role.BetaReader);
        var prefs = UserPreferences.CreateForBetaReader(user.Id);
        var sut = CreateSut();

        PrefsRepo.Setup(r => r.GetByUserIdAsync(user.Id, default))
            .ReturnsAsync(prefs);

        await sut.UpdateDisplayThemeAsync(user.Id, DisplayTheme.Dark);

        Assert.Equal(DisplayTheme.Dark, prefs.DisplayTheme);
        UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task UpdateDisplayThemeAsync_MissingPreferences_ThrowsEntityNotFoundException()
    {
        var userId = Guid.NewGuid();
        var sut = CreateSut();

        PrefsRepo.Setup(r => r.GetByUserIdAsync(userId, default))
            .ReturnsAsync((UserPreferences?) null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.UpdateDisplayThemeAsync(userId, DisplayTheme.Dark));
    }

    // ---------------------------------------------------------------------------
    // UpdateProseFontPreferencesAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateProseFontPreferencesAsync_ValidUser_UpdatesPreferencesAndSaves()
    {
        var user = User.Create("reader@example.com", "Reader", Role.BetaReader);
        var prefs = UserPreferences.CreateForBetaReader(user.Id);
        var sut = CreateSut();

        PrefsRepo.Setup(r => r.GetByUserIdAsync(user.Id, default))
            .ReturnsAsync(prefs);

        await sut.UpdateProseFontPreferencesAsync(
            user.Id,
            ProseFont.Classic,
            ProseFontSize.Large,
            CancellationToken.None);

        Assert.Equal(ProseFont.Classic, prefs.ProseFont);
        Assert.Equal(ProseFontSize.Large, prefs.ProseFontSize);
        UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task UpdateProseFontPreferencesAsync_MissingPreferences_ThrowsEntityNotFoundException()
    {
        var userId = Guid.NewGuid();
        var sut = CreateSut();

        PrefsRepo.Setup(r => r.GetByUserIdAsync(userId, default))
            .ReturnsAsync((UserPreferences?) null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            sut.UpdateProseFontPreferencesAsync(
                userId,
                ProseFont.SystemSerif,
                ProseFontSize.Medium,
                CancellationToken.None));
    }
}



