using Moq;
using DraftReader.Application.Services;
using DraftReader.Domain.Entities;
using DraftReader.Domain.Enumerations;
using DraftReader.Domain.Exceptions;
using DraftReader.Domain.Interfaces.Repositories;

namespace DraftReader.Application.Tests.Services;

public class CommentServiceEditTests
{
    [Fact]
    public async Task EditCommentAsync_OwnerEditingOwnRootComment_UpdatesBody()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(
            commentRepo.Object,
            sectionRepo.Object,
            userRepo.Object,
            unitOfWork.Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        owner.Activate();

        var comment = Comment.CreateRoot(section.Id, owner.Id, "Original.", Visibility.Public);
        var originalEditedAt = comment.EditedAt;

        commentRepo
            .Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        userRepo
            .Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

        // Act
        await sut.EditCommentAsync(comment.Id, owner.Id, "Updated.");

        // Assert
        Assert.Equal("Updated.", comment.Body);
        Assert.NotNull(comment.EditedAt);
        Assert.NotEqual(originalEditedAt, comment.EditedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EditCommentAsync_OwnerEditingOwnReply_UpdatesBody()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(
            commentRepo.Object,
            sectionRepo.Object,
            userRepo.Object,
            unitOfWork.Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        owner.Activate();

        var parent = Comment.CreateRoot(section.Id, owner.Id, "Parent.", Visibility.Public);
        var reply = Comment.CreateReply(section.Id, owner.Id, parent.Id, Visibility.Public, "Original reply.", Visibility.Public);
        var originalEditedAt = reply.EditedAt;

        commentRepo
            .Setup(r => r.GetByIdAsync(reply.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reply);

        userRepo
            .Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

        // Act
        await sut.EditCommentAsync(reply.Id, owner.Id, "Updated reply.");

        // Assert
        Assert.Equal("Updated reply.", reply.Body);
        Assert.NotNull(reply.EditedAt);
        Assert.NotEqual(originalEditedAt, reply.EditedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EditCommentAsync_NonOwnerEditingRootComment_ThrowsUnauthorisedOperationException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(
            commentRepo.Object,
            sectionRepo.Object,
            userRepo.Object,
            unitOfWork.Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var otherUser = MakeBetaReader();
        owner.Activate();
        otherUser.Activate();

        var comment = Comment.CreateRoot(section.Id, owner.Id, "Original.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        userRepo
            .Setup(r => r.GetByIdAsync(otherUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUser);

        // Act
        var act = () => sut.EditCommentAsync(comment.Id, otherUser.Id, "Hacked.");

        // Assert
        await Assert.ThrowsAsync<UnauthorisedOperationException>(act);
        Assert.Equal("Original.", comment.Body);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditCommentAsync_NonOwnerEditingReply_ThrowsUnauthorisedOperationException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(
            commentRepo.Object,
            sectionRepo.Object,
            userRepo.Object,
            unitOfWork.Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var otherUser = MakeBetaReader();
        owner.Activate();
        otherUser.Activate();

        var parent = Comment.CreateRoot(section.Id, owner.Id, "Parent.", Visibility.Public);
        var reply = Comment.CreateReply(section.Id, owner.Id, parent.Id, Visibility.Public, "Original reply.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(reply.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reply);

        userRepo
            .Setup(r => r.GetByIdAsync(otherUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUser);

        // Act
        var act = () => sut.EditCommentAsync(reply.Id, otherUser.Id, "Hacked reply.");

        // Assert
        await Assert.ThrowsAsync<UnauthorisedOperationException>(act);
        Assert.Equal("Original reply.", reply.Body);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditCommentAsync_AuthorRoleEditingAnotherUsersComment_ThrowsUnauthorisedOperationException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(
            commentRepo.Object,
            sectionRepo.Object,
            userRepo.Object,
            unitOfWork.Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var platformAuthor = MakeAuthor();
        owner.Activate();
        platformAuthor.Activate();

        var comment = Comment.CreateRoot(section.Id, owner.Id, "Original.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        userRepo
            .Setup(r => r.GetByIdAsync(platformAuthor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(platformAuthor);

        // Act
        var act = () => sut.EditCommentAsync(comment.Id, platformAuthor.Id, "Author edit.");

        // Assert
        await Assert.ThrowsAsync<UnauthorisedOperationException>(act);
        Assert.Equal("Original.", comment.Body);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditCommentAsync_CommentNotFound_ThrowsEntityNotFoundException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(
            commentRepo.Object,
            sectionRepo.Object,
            userRepo.Object,
            unitOfWork.Object);

        var user = MakeBetaReader();
        user.Activate();
        var missingCommentId = Guid.NewGuid();

        commentRepo
            .Setup(r => r.GetByIdAsync(missingCommentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Comment?) null);

        // Act
        var act = () => sut.EditCommentAsync(missingCommentId, user.Id, "Updated.");

        // Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(act);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditCommentAsync_ActingUserNotFound_ThrowsEntityNotFoundException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(
            commentRepo.Object,
            sectionRepo.Object,
            userRepo.Object,
            unitOfWork.Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        owner.Activate();

        var comment = Comment.CreateRoot(section.Id, owner.Id, "Original.", Visibility.Public);
        var missingUserId = Guid.NewGuid();

        commentRepo
            .Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        userRepo
            .Setup(r => r.GetByIdAsync(missingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?) null);

        // Act
        var act = () => sut.EditCommentAsync(comment.Id, missingUserId, "Updated.");

        // Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(act);
        Assert.Equal("Original.", comment.Body);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditCommentAsync_SoftDeletedComment_ThrowsInvariantViolationException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(
            commentRepo.Object,
            sectionRepo.Object,
            userRepo.Object,
            unitOfWork.Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        owner.Activate();

        var comment = Comment.CreateRoot(section.Id, owner.Id, "Original.", Visibility.Public);
        comment.SoftDelete();

        commentRepo
            .Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        userRepo
            .Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

        // Act
        var act = () => sut.EditCommentAsync(comment.Id, owner.Id, "Updated.");

        // Assert
        await Assert.ThrowsAsync<InvariantViolationException>(act);
        Assert.Equal("Original.", comment.Body);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EditCommentAsync_InvalidBody_ThrowsInvariantViolationException(string? invalidBody)
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(
            commentRepo.Object,
            sectionRepo.Object,
            userRepo.Object,
            unitOfWork.Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        owner.Activate();

        var comment = Comment.CreateRoot(section.Id, owner.Id, "Original.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        userRepo
            .Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

        // Act
#pragma warning disable CS8604
        var act = () => sut.EditCommentAsync(comment.Id, owner.Id, invalidBody);
#pragma warning restore CS8604

        // Assert
        await Assert.ThrowsAsync<InvariantViolationException>(act);
        Assert.Equal("Original.", comment.Body);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditCommentAsync_OwnerEditingCommentWithTrimmedBody_StoresTrimmedBody()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(
            commentRepo.Object,
            sectionRepo.Object,
            userRepo.Object,
            unitOfWork.Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        owner.Activate();

        var comment = Comment.CreateRoot(section.Id, owner.Id, "Original.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        userRepo
            .Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

        // Act
        await sut.EditCommentAsync(comment.Id, owner.Id, "  Updated with padding.  ");

        // Assert
        Assert.Equal("Updated with padding.", comment.Body);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Section MakePublishedSection()
    {
        var section = Section.CreateDocument(
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            "Scene 1",
            null,
            0,
            "<p>Content</p>",
            "hash",
            "First Draft");

        section.Publish("hash");
        return section;
    }

    private static User MakeBetaReader() =>
        User.Create("reader@example.com", "Reader", Role.BetaReader);

    private static User MakeAuthor() =>
        User.Create("author@example.com", "Author", Role.Author);
}