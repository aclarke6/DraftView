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
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IInvitationRepository> _inviteRepo = new();
    private readonly Mock<IUserNotificationPreferencesRepository> _prefsRepo = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IConfiguration> _config = new();

    private readonly User _author;

    public UserServiceInvitationIssuanceTests()
    {
        _author = User.Create("author@example.com", "Author", Role.Author);

        _userRepo.Setup(r => r.GetByIdAsync(_author.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_author);
        _userRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _config.Setup(c => c["App:BaseUrl"]).Returns("https://app.draftview.co.uk");
    }

    private UserService CreateSut() => new(
        _userRepo.Object,
        _inviteRepo.Object,
        _prefsRepo.Object,
        _emailSender.Object,
        _unitOfWork.Object,
        _config.Object);

    // -------------------------------------------------------------------------
    // 1B - Expiry information in email body
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IssueInvitationAsync_AlwaysOpen_EmailBodyStatesInvitationDoesNotExpire()
    {
        var sut = CreateSut();

        await sut.IssueInvitationAsync(
            "reader@example.com", ExpiryPolicy.AlwaysOpen, null, _author.Id);

        _emailSender.Verify(e => e.SendAsync(
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
            "reader@example.com", ExpiryPolicy.ExpiresAt, expiresAt, _author.Id);

        _emailSender.Verify(e => e.SendAsync(
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
            "becca.dunlop@example.com", ExpiryPolicy.AlwaysOpen, null, _author.Id);

        _emailSender.Verify(e => e.SendAsync(
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
            "hilary.royston@example.com", ExpiryPolicy.ExpiresAt, expiresAt, _author.Id);

        _emailSender.Verify(e => e.SendAsync(
            "hilary.royston@example.com",
            "hilary.royston",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
