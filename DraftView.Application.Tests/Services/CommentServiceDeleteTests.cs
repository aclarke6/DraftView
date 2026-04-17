using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Application.Tests.Services;

public class CommentServiceDeleteTests
{
    [Fact]
    public async Task SoftDeleteCommentAsync_OwnerDeletingRootComment_WhenNoChildren_SoftDeletes()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

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

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        // Act
        await sut.SoftDeleteCommentAsync(comment.Id, owner.Id);

        // Assert
        Assert.True(comment.IsSoftDeleted);
        Assert.NotNull(comment.SoftDeletedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SoftDeleteCommentAsync_OwnerDeletingReply_WhenNoChildren_SoftDeletes()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        owner.Activate();

        var parent = Comment.CreateRoot(section.Id, owner.Id, "Parent.", Visibility.Public);
        var reply = Comment.CreateReply(section.Id, owner.Id, parent.Id, Visibility.Public, "Reply.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(reply.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reply);

        userRepo
            .Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(reply.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        // Act
        await sut.SoftDeleteCommentAsync(reply.Id, owner.Id);

        // Assert
        Assert.True(reply.IsSoftDeleted);
        Assert.NotNull(reply.SoftDeletedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SoftDeleteCommentAsync_NonOwnerDeletingRootComment_ThrowsUnauthorisedOperationException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

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
        var act = () => sut.SoftDeleteCommentAsync(comment.Id, otherUser.Id);

        // Assert
        await Assert.ThrowsAsync<UnauthorisedOperationException>(act);
        Assert.False(comment.IsSoftDeleted);
        Assert.Null(comment.SoftDeletedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteCommentAsync_NonOwnerDeletingReply_ThrowsUnauthorisedOperationException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var otherUser = MakeBetaReader();
        owner.Activate();
        otherUser.Activate();

        var parent = Comment.CreateRoot(section.Id, owner.Id, "Parent.", Visibility.Public);
        var reply = Comment.CreateReply(section.Id, owner.Id, parent.Id, Visibility.Public, "Reply.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(reply.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reply);

        userRepo
            .Setup(r => r.GetByIdAsync(otherUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUser);

        // Act
        var act = () => sut.SoftDeleteCommentAsync(reply.Id, otherUser.Id);

        // Assert
        await Assert.ThrowsAsync<UnauthorisedOperationException>(act);
        Assert.False(reply.IsSoftDeleted);
        Assert.Null(reply.SoftDeletedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteCommentAsync_AuthorRoleDeletingAnotherUsersComment_ThrowsUnauthorisedOperationException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

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
        var act = () => sut.SoftDeleteCommentAsync(comment.Id, platformAuthor.Id);

        // Assert
        await Assert.ThrowsAsync<UnauthorisedOperationException>(act);
        Assert.False(comment.IsSoftDeleted);
        Assert.Null(comment.SoftDeletedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteCommentAsync_OwnerDeletingRootComment_WhenChildrenExist_ThrowsInvariantViolationException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        owner.Activate();

        var comment = Comment.CreateRoot(section.Id, owner.Id, "Original.", Visibility.Public);
        var child = Comment.CreateReply(section.Id, owner.Id, comment.Id, Visibility.Public, "Child.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        userRepo
            .Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { child });

        // Act
        var act = () => sut.SoftDeleteCommentAsync(comment.Id, owner.Id);

        // Assert
        await Assert.ThrowsAsync<InvariantViolationException>(act);
        Assert.False(comment.IsSoftDeleted);
        Assert.Null(comment.SoftDeletedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteCommentAsync_OwnerDeletingReply_WhenChildrenExist_ThrowsInvariantViolationException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        owner.Activate();

        var root = Comment.CreateRoot(section.Id, owner.Id, "Root.", Visibility.Public);
        var reply = Comment.CreateReply(section.Id, owner.Id, root.Id, Visibility.Public, "Reply.", Visibility.Public);
        var childReply = Comment.CreateReply(section.Id, owner.Id, reply.Id, Visibility.Public, "Child reply.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(reply.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reply);

        userRepo
            .Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(reply.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { childReply });

        // Act
        var act = () => sut.SoftDeleteCommentAsync(reply.Id, owner.Id);

        // Assert
        await Assert.ThrowsAsync<InvariantViolationException>(act);
        Assert.False(reply.IsSoftDeleted);
        Assert.Null(reply.SoftDeletedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteCommentAsync_CommentNotFound_ThrowsEntityNotFoundException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var user = MakeBetaReader();
        user.Activate();
        var missingCommentId = Guid.NewGuid();

        commentRepo
            .Setup(r => r.GetByIdAsync(missingCommentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Comment?) null);

        // Act
        var act = () => sut.SoftDeleteCommentAsync(missingCommentId, user.Id);

        // Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(act);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteCommentAsync_ActingUserNotFound_ThrowsEntityNotFoundException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

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
        var act = () => sut.SoftDeleteCommentAsync(comment.Id, missingUserId);

        // Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(act);
        Assert.False(comment.IsSoftDeleted);
        Assert.Null(comment.SoftDeletedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteCommentAsync_WhenCommentAlreadySoftDeleted_RemainsSoftDeleted()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        owner.Activate();

        var comment = Comment.CreateRoot(section.Id, owner.Id, "Original.", Visibility.Public);
        comment.SoftDelete();
        var firstDeletedAt = comment.SoftDeletedAt;

        commentRepo
            .Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        userRepo
            .Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        // Act
        await sut.SoftDeleteCommentAsync(comment.Id, owner.Id);

        // Assert
        Assert.True(comment.IsSoftDeleted);
        Assert.Equal(firstDeletedAt, comment.SoftDeletedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SoftDeleteCommentAsync_WhenOnlyChildIsSoftDeleted_StillThrowsInvariantViolationException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        owner.Activate();

        var comment = Comment.CreateRoot(section.Id, owner.Id, "Original.", Visibility.Public);
        var child = Comment.CreateReply(section.Id, owner.Id, comment.Id, Visibility.Public, "Child.", Visibility.Public);
        child.SoftDelete();

        commentRepo
            .Setup(r => r.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        userRepo
            .Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { child });

        // Act
        var act = () => sut.SoftDeleteCommentAsync(comment.Id, owner.Id);

        // Assert
        await Assert.ThrowsAsync<InvariantViolationException>(act);
        Assert.False(comment.IsSoftDeleted);
        Assert.Null(comment.SoftDeletedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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

        section.PublishAsPartOfChapter("hash");
        return section;
    }

    private static User MakeBetaReader() =>
        User.Create("reader@example.com", "Reader", Role.BetaReader);

    private static User MakeAuthor() =>
        User.Create("author@example.com", "Author", Role.Author);
}

