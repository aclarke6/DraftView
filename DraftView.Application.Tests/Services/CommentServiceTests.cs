using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Application.Tests.Services;

public class CommentServiceTests
{
    private readonly Mock<ICommentRepository>            _commentRepo      = new();
    private readonly Mock<ISectionRepository>            _sectionRepo      = new();
    private readonly Mock<IUserRepository>               _userRepo         = new();
    private readonly Mock<IUnitOfWork>                   _unitOfWork       = new();
    private readonly Mock<IAuthorNotificationRepository> _notificationRepo = new();
    private readonly Mock<ISectionVersionRepository>     _versionRepo      = new();

    private CommentService CreateSut() => new(
        _commentRepo.Object,
        _sectionRepo.Object,
        _userRepo.Object,
        _unitOfWork.Object,
        _notificationRepo.Object,
        _versionRepo.Object);

    private static Section MakePublishedSection()
    {
        var s = Section.CreateDocument(Guid.NewGuid(), Guid.NewGuid().ToString(),
            "Scene 1", null, 0, "<p>Content</p>", "hash", "First Draft");
        s.PublishAsPartOfChapter("hash");
        return s;
    }

    private static User MakeBetaReader() =>
        User.Create("reader@example.com", "Reader", Role.BetaReader);

    private static User MakeAuthor() =>
        User.Create("author@example.com", "Author", Role.Author);

    // ---------------------------------------------------------------------------
    // CreateRootComment
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateRootCommentAsync_ValidRequest_AddsComment()
    {
        var section = MakePublishedSection();
        var reader  = MakeBetaReader();
        reader.Activate();
        var sut = CreateSut();

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);

        Comment? added = null;
        _commentRepo.Setup(r => r.AddAsync(It.IsAny<Comment>(), default))
            .Callback<Comment, CancellationToken>((c, _) => added = c);

        var result = await sut.CreateRootCommentAsync(
            section.Id, reader.Id, "Great scene!", Visibility.Public);

        Assert.NotNull(added);
        Assert.Equal("Great scene!", result.Body);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateRootCommentAsync_SectionNotFound_ThrowsEntityNotFoundException()
    {
        var sut       = CreateSut();
        var missingId = Guid.NewGuid();

        _sectionRepo.Setup(r => r.GetByIdAsync(missingId, default))
            .ReturnsAsync((Section?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.CreateRootCommentAsync(missingId, Guid.NewGuid(), "body", Visibility.Public));
    }

    [Fact]
    public async Task CreateRootCommentAsync_UnpublishedSection_BetaReader_ThrowsUnauthorised()
    {
        var section = Section.CreateDocument(Guid.NewGuid(), Guid.NewGuid().ToString(),
            "Scene 1", null, 0, "<p>x</p>", "h", "First Draft");
        var reader = MakeBetaReader();
        reader.Activate();
        var sut = CreateSut();

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.CreateRootCommentAsync(section.Id, reader.Id, "body", Visibility.Public));
    }

    // ---------------------------------------------------------------------------
    // CreateReply
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateReplyAsync_ValidRequest_AddsReply()
    {
        var section = MakePublishedSection();
        var reader  = MakeBetaReader();
        reader.Activate();
        var parent  = Comment.CreateRoot(section.Id, reader.Id, "Original.", Visibility.Public);
        var sut     = CreateSut();

        _commentRepo.Setup(r => r.GetByIdAsync(parent.Id, default)).ReturnsAsync(parent);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);

        Comment? added = null;
        _commentRepo.Setup(r => r.AddAsync(It.IsAny<Comment>(), default))
            .Callback<Comment, CancellationToken>((c, _) => added = c);

        var result = await sut.CreateReplyAsync(parent.Id, reader.Id, "My reply.", Visibility.Public);

        Assert.NotNull(added);
        Assert.Equal(parent.Id, result.ParentCommentId);
    }

    [Fact]
    public async Task CreateReplyAsync_DeletedParent_ThrowsInvariantViolationException()
    {
        var section = MakePublishedSection();
        var reader  = MakeBetaReader();
        reader.Activate();
        var parent  = Comment.CreateRoot(section.Id, reader.Id, "Original.", Visibility.Public);
        parent.SoftDelete();
        var sut = CreateSut();

        _commentRepo.Setup(r => r.GetByIdAsync(parent.Id, default)).ReturnsAsync(parent);
        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.CreateReplyAsync(parent.Id, reader.Id, "reply", Visibility.Public));
    }

    //// ---------------------------------------------------------------------------
    //// EditComment
    //// ---------------------------------------------------------------------------

    //[Fact]
    //public async Task EditCommentAsync_Owner_UpdatesBody()
    //{
    //    var section = MakePublishedSection();
    //    var reader  = MakeBetaReader();
    //    reader.Activate();
    //    var comment = Comment.CreateRoot(section.Id, reader.Id, "Original.", Visibility.Public);
    //    var sut     = CreateSut();

    //    _commentRepo.Setup(r => r.GetByIdAsync(comment.Id, default)).ReturnsAsync(comment);
    //    UserRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);

    //    await sut.EditCommentAsync(comment.Id, reader.Id, "Updated.");

    //    Assert.Equal("Updated.", comment.Body);
    //}

    [Fact]
    public async Task EditCommentAsync_NonOwner_ThrowsUnauthorised()
    {
        var section  = MakePublishedSection();
        var reader   = MakeBetaReader();
        var other    = MakeBetaReader();
        reader.Activate();
        other.Activate();
        var comment = Comment.CreateRoot(section.Id, reader.Id, "Original.", Visibility.Public);
        var sut     = CreateSut();

        _commentRepo.Setup(r => r.GetByIdAsync(comment.Id, default)).ReturnsAsync(comment);
        _userRepo.Setup(r => r.GetByIdAsync(other.Id, default)).ReturnsAsync(other);

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.EditCommentAsync(comment.Id, other.Id, "Hacked."));
    }

    // ---------------------------------------------------------------------------
    // GetThreadsForSection
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetThreadsForSectionAsync_FiltersPrivateComments()
    {
        var section  = MakePublishedSection();
        var reader1  = MakeBetaReader();
        var reader2  = MakeBetaReader();
        reader1.Activate();
        reader2.Activate();

        var publicComment  = Comment.CreateRoot(section.Id, reader1.Id, "Public.", Visibility.Public);
        var privateComment = Comment.CreateRoot(section.Id, reader1.Id, "Private.", Visibility.Private);

        var sut = CreateSut();

        _commentRepo.Setup(r => r.GetAllBySectionIdAsync(section.Id, default))
            .ReturnsAsync(new List<Comment> { publicComment, privateComment });
        _commentRepo.Setup(r => r.GetRepliesByParentIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(new List<Comment>());
        _userRepo.Setup(r => r.GetByIdAsync(reader2.Id, default)).ReturnsAsync(reader2);

        var result = await sut.GetThreadsForSectionAsync(section.Id, reader2.Id);

        Assert.Single(result);
        Assert.Equal("Public.", result[0].Body);
    }

    // ---------------------------------------------------------------------------
    // Notifications — CreateRootComment
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateRootCommentAsync_WritesNewCommentNotification_WhenReaderComments()
    {
        var section = MakePublishedSection();
        var reader  = MakeBetaReader();
        reader.Activate();
        var author  = MakeAuthor();
        var sut     = CreateSut();

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);
        _userRepo.Setup(r => r.GetAuthorAsync(default)).ReturnsAsync(author);
        _commentRepo.Setup(r => r.AddAsync(It.IsAny<Comment>(), default)).Returns(Task.CompletedTask);

        await sut.CreateRootCommentAsync(section.Id, reader.Id, "Nice chapter!", Visibility.Public);

        _notificationRepo.Verify(
            r => r.AddAsync(It.Is<AuthorNotification>(n =>
                n.AuthorId == author.Id &&
                n.Title.Contains(reader.DisplayName) &&
                n.Title.Contains(section.Title)),
                default),
            Times.Once);
    }

    [Fact]
    public async Task CreateRootCommentAsync_DoesNotWriteNotification_WhenAuthorComments()
    {
        var section = MakePublishedSection();
        var author  = MakeAuthor();
        var sut     = CreateSut();

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _userRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        _commentRepo.Setup(r => r.AddAsync(It.IsAny<Comment>(), default)).Returns(Task.CompletedTask);

        await sut.CreateRootCommentAsync(section.Id, author.Id, "My note.", Visibility.Private);

        _notificationRepo.Verify(
            r => r.AddAsync(It.IsAny<AuthorNotification>(), default),
            Times.Never);
    }

    // ---------------------------------------------------------------------------
    // Notifications — CreateReply
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateReplyAsync_WritesReplyToAuthorNotification_WhenReaderRepliesToAuthorComment()
    {
        var section = MakePublishedSection();
        var author  = MakeAuthor();
        var reader  = MakeBetaReader();
        reader.Activate();
        var authorComment = Comment.CreateRoot(section.Id, author.Id, "My thoughts.", Visibility.Public,
            isReaderComment: false);
        var sut = CreateSut();

        _commentRepo.Setup(r => r.GetByIdAsync(authorComment.Id, default)).ReturnsAsync(authorComment);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);
        _userRepo.Setup(r => r.GetAuthorAsync(default)).ReturnsAsync(author);
        _commentRepo.Setup(r => r.AddAsync(It.IsAny<Comment>(), default)).Returns(Task.CompletedTask);

        await sut.CreateReplyAsync(authorComment.Id, reader.Id, "Agreed!", Visibility.Public);

        _notificationRepo.Verify(
            r => r.AddAsync(It.Is<AuthorNotification>(n =>
                n.AuthorId == author.Id &&
                n.Title.Contains(reader.DisplayName)),
                default),
            Times.Once);
    }

    [Fact]
    public async Task CreateReplyAsync_DoesNotWriteNotification_WhenReplyIsNotToAuthorComment()
    {
        var section       = MakePublishedSection();
        var reader1       = MakeBetaReader();
        var reader2       = MakeBetaReader();
        var author        = MakeAuthor();
        reader1.Activate();
        reader2.Activate();
        var readerComment = Comment.CreateRoot(section.Id, reader1.Id, "Good stuff.", Visibility.Public);
        var sut = CreateSut();

        _commentRepo.Setup(r => r.GetByIdAsync(readerComment.Id, default)).ReturnsAsync(readerComment);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _userRepo.Setup(r => r.GetByIdAsync(reader2.Id, default)).ReturnsAsync(reader2);
        _userRepo.Setup(r => r.GetAuthorAsync(default)).ReturnsAsync(author);
        _commentRepo.Setup(r => r.AddAsync(It.IsAny<Comment>(), default)).Returns(Task.CompletedTask);

        await sut.CreateReplyAsync(readerComment.Id, reader2.Id, "Me too!", Visibility.Public);

        _notificationRepo.Verify(
            r => r.AddAsync(It.IsAny<AuthorNotification>(), default),
            Times.Never);
    }

    // ---------------------------------------------------------------------------
    // SectionVersionId anchoring
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateRootCommentAsync_SetsCurrentSectionVersionId_WhenVersionExists()
    {
        var section = MakePublishedSection();
        var reader  = MakeBetaReader();
        reader.Activate();
        var sut = CreateSut();

        var version = SectionVersion.Create(section, Guid.NewGuid(), 1);
        var versionRepo = new Mock<ISectionVersionRepository>();
        versionRepo.Setup(r => r.GetLatestAsync(section.Id, default))
            .ReturnsAsync(version);

        var sutWithVersion = new CommentService(
            _commentRepo.Object,
            _sectionRepo.Object,
            _userRepo.Object,
            _unitOfWork.Object,
            _notificationRepo.Object,
            versionRepo.Object);

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);

        Comment? added = null;
        _commentRepo.Setup(r => r.AddAsync(It.IsAny<Comment>(), default))
            .Callback<Comment, CancellationToken>((c, _) => added = c);

        await sutWithVersion.CreateRootCommentAsync(section.Id, reader.Id, "Great!", Visibility.Public);

        Assert.NotNull(added);
        Assert.Equal(version.Id, added!.SectionVersionId);
    }

    [Fact]
    public async Task CreateRootCommentAsync_SetsNullSectionVersionId_WhenNoVersionExists()
    {
        var section = MakePublishedSection();
        var reader  = MakeBetaReader();
        reader.Activate();
        var sut = CreateSut();

        var versionRepo = new Mock<ISectionVersionRepository>();
        versionRepo.Setup(r => r.GetLatestAsync(section.Id, default))
            .ReturnsAsync((SectionVersion?)null);

        var sutWithVersion = new CommentService(
            _commentRepo.Object,
            _sectionRepo.Object,
            _userRepo.Object,
            _unitOfWork.Object,
            _notificationRepo.Object,
            versionRepo.Object);

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);

        Comment? added = null;
        _commentRepo.Setup(r => r.AddAsync(It.IsAny<Comment>(), default))
            .Callback<Comment, CancellationToken>((c, _) => added = c);

        await sutWithVersion.CreateRootCommentAsync(section.Id, reader.Id, "Great!", Visibility.Public);

        Assert.NotNull(added);
        Assert.Null(added!.SectionVersionId);
    }
}
