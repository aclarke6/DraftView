using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for ReaderMessageService.
/// Covers: successful send, author role enforcement, entity-not-found cases,
/// email dispatch with correct Reply-To, sent-log persistence, GetSentByAuthor.
/// Excludes: SMTP delivery, web controller binding.
/// </summary>
public class ReaderMessageServiceTests
{
    private readonly Mock<IUserRepository>           _userRepo     = new();
    private readonly Mock<IReaderMessageRepository>  _messageRepo  = new();
    private readonly Mock<IEmailSender>              _emailSender  = new();
    private readonly Mock<IUnitOfWork>               _unitOfWork   = new();

    private ReaderMessageService CreateSut() => new(
        _userRepo.Object,
        _messageRepo.Object,
        _emailSender.Object,
        _unitOfWork.Object);

    private static User MakeAuthor()
    {
        var u = User.Create("author@example.com", "Author Name", Role.Author);
        u.Activate();
        return u;
    }

    private static User MakeReader()
    {
        var u = User.Create("reader@example.com", "Reader Name", Role.BetaReader);
        u.Activate();
        return u;
    }

    // -----------------------------------------------------------------------
    // SendAsync — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_ValidRequest_PersistsMessageAndSaves()
    {
        var author    = MakeAuthor();
        var recipient = MakeReader();
        var sut       = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(author.Id,    default)).ReturnsAsync(author);
        _userRepo.Setup(r => r.GetByIdAsync(recipient.Id, default)).ReturnsAsync(recipient);
        ReaderMessage? saved = null;
        _messageRepo.Setup(r => r.AddAsync(It.IsAny<ReaderMessage>(), default))
            .Callback<ReaderMessage, CancellationToken>((m, _) => saved = m);

        await sut.SendAsync(author.Id, recipient.Id, "Hello", "Great work!", default);

        Assert.NotNull(saved);
        Assert.Equal(author.Id,    saved!.AuthorId);
        Assert.Equal(recipient.Id, saved.RecipientId);
        Assert.Equal("Hello",      saved.Subject);
        Assert.Equal("Great work!", saved.Body);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ValidRequest_SendsEmailToRecipientWithReplyToAuthor()
    {
        var author    = MakeAuthor();
        var recipient = MakeReader();
        var sut       = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(author.Id,    default)).ReturnsAsync(author);
        _userRepo.Setup(r => r.GetByIdAsync(recipient.Id, default)).ReturnsAsync(recipient);

        await sut.SendAsync(author.Id, recipient.Id, "My subject", "My message", default);

        _emailSender.Verify(s => s.SendAsync(
            recipient.Email,
            recipient.DisplayName,
            "My subject",
            It.Is<string>(b => b.Contains("My message") && b.Contains(author.DisplayName)),
            default,
            author.Email),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_ValidRequest_EmailBodyContainsAuthorSignOff()
    {
        var author    = MakeAuthor();
        var recipient = MakeReader();
        var sut       = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(author.Id,    default)).ReturnsAsync(author);
        _userRepo.Setup(r => r.GetByIdAsync(recipient.Id, default)).ReturnsAsync(recipient);

        string? capturedBody = null;
        _emailSender.Setup(s => s.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
            .Callback<string, string, string, string, CancellationToken, string?>(
                (_, _, _, body, _, _) => capturedBody = body);

        await sut.SendAsync(author.Id, recipient.Id, "Subject", "Body text", default);

        Assert.NotNull(capturedBody);
        Assert.Contains("Author Name", capturedBody);
    }

    // -----------------------------------------------------------------------
    // SendAsync — authorisation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_NonAuthorActingUser_ThrowsUnauthorised()
    {
        var reader    = MakeReader();
        var recipient = MakeReader();
        var sut       = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(reader.Id,    default)).ReturnsAsync(reader);
        _userRepo.Setup(r => r.GetByIdAsync(recipient.Id, default)).ReturnsAsync(recipient);

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.SendAsync(reader.Id, recipient.Id, "Subj", "Body", default));
    }

    // -----------------------------------------------------------------------
    // SendAsync — not found
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_AuthorNotFound_ThrowsEntityNotFound()
    {
        var sut = CreateSut();
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.SendAsync(Guid.NewGuid(), Guid.NewGuid(), "S", "B", default));
    }

    [Fact]
    public async Task SendAsync_RecipientNotFound_ThrowsEntityNotFound()
    {
        var author = MakeAuthor();
        var sut    = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(author.Id,    default)).ReturnsAsync(author);
        _userRepo.Setup(r => r.GetByIdAsync(It.Is<Guid>(id => id != author.Id), default))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.SendAsync(author.Id, Guid.NewGuid(), "S", "B", default));
    }

    // -----------------------------------------------------------------------
    // GetSentByAuthorAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSentByAuthorAsync_ReturnsRepositoryResult()
    {
        var authorId = Guid.NewGuid();
        var sut      = CreateSut();
        var expected = new List<ReaderMessage>
        {
            ReaderMessage.Create(authorId, Guid.NewGuid(), "S1", "B1", DateTime.UtcNow),
            ReaderMessage.Create(authorId, Guid.NewGuid(), "S2", "B2", DateTime.UtcNow)
        };

        _messageRepo.Setup(r => r.GetByAuthorIdAsync(authorId, default))
            .ReturnsAsync(expected);

        var result = await sut.GetSentByAuthorAsync(authorId);

        Assert.Equal(2, result.Count);
    }
}
