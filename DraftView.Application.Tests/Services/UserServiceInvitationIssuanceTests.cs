using Microsoft.Extensions.Configuration;
using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

public class UserServiceInvitationIssuanceTests
{
    private readonly Mock<IUserRepository> UserRepo = new();
    private readonly Mock<IInvitationRepository> InviteRepo = new();
    private readonly Mock<IUserPreferencesRepository> PrefsRepo = new();
    private readonly Mock<IEmailSender> EmailSender = new();
    private readonly Mock<IUnitOfWork> UnitOfWork = new();
    private readonly Mock<IConfiguration> Config = new();
    private readonly Mock<IReaderAccessRepository> ReaderAccessRepo = new();
    private readonly Mock<IAuthorizationFacade> AuthFacade = new();
    private readonly Mock<IAuthorNotificationRepository> NotifRepo = new();

    private readonly User Author;

    public UserServiceInvitationIssuanceTests()
    {
        Author = User.Create("author@example.com", "Author", Role.Author);

        UserRepo.Setup(r => r.GetByIdAsync(Author.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Author);
        UserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        InviteRepo.Setup(r => r.GetPendingByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Config.Setup(c => c["App:BaseUrl"]).Returns("https://app.draftview.co.uk");
        AuthFacade.Setup(f => f.IsAuthor()).Returns(true);
    }

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

    [Fact]
    public async Task IssueInvitationAsync_AlwaysOpen_EmailBodyStatesInvitationDoesNotExpire()
    {
        var sut = CreateSut();

        await sut.IssueInvitationAsync(
            "reader@example.com", "Reader One", ExpiryPolicy.AlwaysOpen, null, Author.Id);

        EmailSender.Verify(e => e.SendAsync(
            "reader@example.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("does not expire")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueInvitationAsync_WithExpiry_EmailBodyStatesExpiryDate()
    {
        var expiresAt = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var sut = CreateSut();

        await sut.IssueInvitationAsync(
            "reader@example.com", "Reader One", ExpiryPolicy.ExpiresAt, expiresAt, Author.Id);

        EmailSender.Verify(e => e.SendAsync(
            "reader@example.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("30 June 2026")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueInvitationAsync_AlwaysOpen_RecipientNameIsDisplayName()
    {
        var sut = CreateSut();

        await sut.IssueInvitationAsync(
            "becca.dunlop@example.com", "Becca Dunlop", ExpiryPolicy.AlwaysOpen, null, Author.Id);

        EmailSender.Verify(e => e.SendAsync(
            "becca.dunlop@example.com",
            "Becca Dunlop",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueInvitationAsync_WithExpiry_RecipientNameIsDisplayName()
    {
        var expiresAt = DateTime.UtcNow.AddDays(14);
        var sut = CreateSut();

        await sut.IssueInvitationAsync(
            "hilary.royston@example.com", "Hilary Royston", ExpiryPolicy.ExpiresAt, expiresAt, Author.Id);

        EmailSender.Verify(e => e.SendAsync(
            "hilary.royston@example.com",
            "Hilary Royston",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueInvitationAsync_UsesConfiguredAbsoluteBaseUrlInInviteLink()
    {
        var sut = CreateSut();

        await sut.IssueInvitationAsync(
            "reader@example.com", "Reader One", ExpiryPolicy.AlwaysOpen, null, Author.Id);

        EmailSender.Verify(e => e.SendAsync(
            "reader@example.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("https://app.draftview.co.uk/Account/AcceptInvitation?token=")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueInvitationAsync_WhenAppBaseUrlMissing_ThrowsInsteadOfFallingBackToLocalhost()
    {
        Config.Setup(c => c["App:BaseUrl"]).Returns((string?)null);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.IssueInvitationAsync("reader@example.com", "Reader One", ExpiryPolicy.AlwaysOpen, null, Author.Id));

        Assert.Contains("App:BaseUrl", ex.Message);
        EmailSender.Verify(e => e.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IssueInvitationAsync_WhenAppBaseUrlInvalid_ThrowsInsteadOfSendingBrokenLink()
    {
        Config.Setup(c => c["App:BaseUrl"]).Returns("not-a-url");
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.IssueInvitationAsync("reader@example.com", "Reader One", ExpiryPolicy.AlwaysOpen, null, Author.Id));

        Assert.Contains("valid absolute URL", ex.Message);
        EmailSender.Verify(e => e.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
