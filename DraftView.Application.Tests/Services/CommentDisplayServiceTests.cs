using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for CommentDisplayService.GetCommentDisplayDataAsync.
/// Covers: soft-delete filtering, author display name resolution, unknown-author fallback,
/// parent/child HasChildren and CanDelete logic, passage anchor resolution and override
/// permission, and passage anchor lookup failures being swallowed.
/// Excludes: EF Core persistence, UI rendering.
/// </summary>
public class CommentDisplayServiceTests
{
    private readonly Mock<IUserRepository>         _userRepo            = new();
    private readonly Mock<IPassageAnchorService>   _passageAnchorService = new();

    private CommentDisplayService CreateSut() => new(
        _userRepo.Object,
        _passageAnchorService.Object);

    [Fact]
    public async Task GetCommentDisplayDataAsync_FiltersSoftDeletedComments()
    {
        var authorId = Guid.NewGuid();
        var visible = Comment.CreateRoot(Guid.NewGuid(), authorId, "Hello", Visibility.Public);
        var deleted = Comment.CreateRoot(Guid.NewGuid(), authorId, "Bye", Visibility.Public);
        deleted.SoftDelete();

        _userRepo.Setup(r => r.GetByIdAsync(authorId, default))
            .ReturnsAsync(User.Create("a@example.com", "Alice", Role.BetaReader));

        var result = await CreateSut().GetCommentDisplayDataAsync(
            new List<Comment> { visible, deleted }, authorId, Guid.NewGuid(), false);

        Assert.Single(result);
        Assert.Equal(visible.Id, result[0].Comment.Id);
    }

    [Fact]
    public async Task GetCommentDisplayDataAsync_ResolvesAuthorDisplayName()
    {
        var authorId = Guid.NewGuid();
        var comment = Comment.CreateRoot(Guid.NewGuid(), authorId, "Hello", Visibility.Public);
        var user = User.Create("a@example.com", "Alice", Role.BetaReader);

        _userRepo.Setup(r => r.GetByIdAsync(authorId, default)).ReturnsAsync(user);

        var result = await CreateSut().GetCommentDisplayDataAsync(
            new List<Comment> { comment }, authorId, Guid.NewGuid(), false);

        Assert.Equal("Alice", result[0].AuthorDisplayName);
    }

    [Fact]
    public async Task GetCommentDisplayDataAsync_UnknownAuthor_UsesUnknownName()
    {
        var authorId = Guid.NewGuid();
        var comment = Comment.CreateRoot(Guid.NewGuid(), authorId, "Hello", Visibility.Public);

        _userRepo.Setup(r => r.GetByIdAsync(authorId, default)).ReturnsAsync((User?)null);

        var result = await CreateSut().GetCommentDisplayDataAsync(
            new List<Comment> { comment }, authorId, Guid.NewGuid(), false);

        Assert.Equal("Unknown", result[0].AuthorDisplayName);
    }

    [Fact]
    public async Task GetCommentDisplayDataAsync_CommentWithChildren_HasChildrenTrueAndCannotDelete()
    {
        var authorId = Guid.NewGuid();
        var parent = Comment.CreateRoot(Guid.NewGuid(), authorId, "Parent", Visibility.Public);
        var child  = Comment.CreateReply(parent.SectionId, authorId, parent.Id, Visibility.Public, "Child", Visibility.Public);

        _userRepo.Setup(r => r.GetByIdAsync(authorId, default))
            .ReturnsAsync(User.Create("a@example.com", "Alice", Role.BetaReader));

        var result = await CreateSut().GetCommentDisplayDataAsync(
            new List<Comment> { parent, child }, authorId, Guid.NewGuid(), false);

        var parentDisplay = result.Single(r => r.Comment.Id == parent.Id);
        Assert.True(parentDisplay.HasChildren);
        Assert.False(parentDisplay.CanDelete);
    }

    [Fact]
    public async Task GetCommentDisplayDataAsync_CommentWithoutChildren_AuthoredByCurrentUser_CanDelete()
    {
        var currentUserId = Guid.NewGuid();
        var comment = Comment.CreateRoot(Guid.NewGuid(), currentUserId, "Hi", Visibility.Public);

        _userRepo.Setup(r => r.GetByIdAsync(currentUserId, default))
            .ReturnsAsync(User.Create("a@example.com", "Alice", Role.BetaReader));

        var result = await CreateSut().GetCommentDisplayDataAsync(
            new List<Comment> { comment }, currentUserId, Guid.NewGuid(), false);

        Assert.True(result[0].CanDelete);
        Assert.True(result[0].CanEdit);
    }

    [Fact]
    public async Task GetCommentDisplayDataAsync_ResolvesPassageAnchorAndOverridePermission()
    {
        var currentUserId = Guid.NewGuid();
        var projectAuthorId = Guid.NewGuid();
        var anchorId = Guid.NewGuid();
        var comment = Comment.CreateRoot(
            Guid.NewGuid(), currentUserId, "Hi", Visibility.Public,
            passageAnchorId: anchorId);

        var anchorDto = new PassageAnchorDto(
            anchorId, Guid.NewGuid(), PassageAnchorPurpose.Comment,
            currentUserId, DateTime.UtcNow, PassageAnchorStatus.Exact, null,
            new PassageAnchorSnapshotDto("text", "text", "hash", "pre", "post", 0, 4, "content-hash", null),
            null);

        _userRepo.Setup(r => r.GetByIdAsync(currentUserId, default))
            .ReturnsAsync(User.Create("a@example.com", "Alice", Role.BetaReader));
        _passageAnchorService.Setup(s => s.GetByIdAsync(anchorId, currentUserId, default))
            .ReturnsAsync(anchorDto);

        var result = await CreateSut().GetCommentDisplayDataAsync(
            new List<Comment> { comment }, currentUserId, projectAuthorId, false);

        Assert.NotNull(result[0].PassageAnchor);
        Assert.True(result[0].CanOverridePassageAnchor);
    }

    [Fact]
    public async Task GetCommentDisplayDataAsync_PassageAnchorLookupThrows_ResultIsNullNotThrown()
    {
        var currentUserId = Guid.NewGuid();
        var anchorId = Guid.NewGuid();
        var comment = Comment.CreateRoot(
            Guid.NewGuid(), currentUserId, "Hi", Visibility.Public,
            passageAnchorId: anchorId);

        _userRepo.Setup(r => r.GetByIdAsync(currentUserId, default))
            .ReturnsAsync(User.Create("a@example.com", "Alice", Role.BetaReader));
        _passageAnchorService.Setup(s => s.GetByIdAsync(anchorId, currentUserId, default))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await CreateSut().GetCommentDisplayDataAsync(
            new List<Comment> { comment }, currentUserId, Guid.NewGuid(), false);

        Assert.Null(result[0].PassageAnchor);
    }
}
