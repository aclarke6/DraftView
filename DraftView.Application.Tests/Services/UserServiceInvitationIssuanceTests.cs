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
    private readonly Mock<IReaderAccessRepository>       ReaderAccessRepo = new();
    private readonly Mock<IAuthorizationFacade>          AuthFacade       = new();
    private readonly Mock<IAuthorNotificationRepository> NotifRepo        = new();

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

    // -------------------------------------------------------------------------
    // 1B - Expiry information in email body
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IssueInvitationAsync_AlwaysOpen_EmailBodyStatesInvitationDoesNotExpire()
    {
        var sut = CreateSut();

        await sut.IssueInvitationAsync(
            "reader@example.com", ExpiryPolicy.AlwaysOpen, null, Author.Id);

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
            "reader@example.com", ExpiryPolicy.ExpiresAt, expiresAt, Author.Id);

        EmailSender.Verify(e => e.SendAsync(
            "reader@example.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("30 June 2026")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // 1D - Recipient name is email local part, not generic "Invited Reader"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IssueInvitationAsync_AlwaysOpen_RecipientNameIsEmailLocalPart()
    {
        var sut = CreateSut();

        await sut.IssueInvitationAsync(
            "becca.dunlop@example.com", ExpiryPolicy.AlwaysOpen, null, Author.Id);

        EmailSender.Verify(e => e.SendAsync(
            "becca.dunlop@example.com",
            "becca.dunlop",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueInvitationAsync_WithExpiry_RecipientNameIsEmailLocalPart()
    {
        var expiresAt = DateTime.UtcNow.AddDays(14);
        var sut = CreateSut();

        await sut.IssueInvitationAsync(
            "hilary.royston@example.com", ExpiryPolicy.ExpiresAt, expiresAt, Author.Id);

        EmailSender.Verify(e => e.SendAsync(
            "hilary.royston@example.com",
            "hilary.royston",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
