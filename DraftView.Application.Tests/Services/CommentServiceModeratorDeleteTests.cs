using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Application.Tests.Services;

public class CommentServiceModeratorDeleteTests
{
    [Fact]
    public async Task ModerateDeleteCommentAsync_ModeratorDeletingLeafRootComment_SoftDeletesTarget()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var moderator = MakeAuthor();
        owner.Activate();
        moderator.Activate();

        var target = Comment.CreateRoot(section.Id, owner.Id, "Root.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        userRepo
            .Setup(r => r.GetByIdAsync(moderator.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moderator);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        // Act
        await sut.ModerateDeleteCommentAsync(target.Id, moderator.Id);

        // Assert
        Assert.True(target.IsSoftDeleted);
        Assert.NotNull(target.SoftDeletedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ModerateDeleteCommentAsync_ModeratorDeletingLeafReply_SoftDeletesTarget()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var moderator = MakeAuthor();
        owner.Activate();
        moderator.Activate();

        var root = Comment.CreateRoot(section.Id, owner.Id, "Root.", Visibility.Public);
        var target = Comment.CreateReply(section.Id, owner.Id, root.Id, Visibility.Public, "Reply.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        userRepo
            .Setup(r => r.GetByIdAsync(moderator.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moderator);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        // Act
        await sut.ModerateDeleteCommentAsync(target.Id, moderator.Id);

        // Assert
        Assert.True(target.IsSoftDeleted);
        Assert.NotNull(target.SoftDeletedAt);
        Assert.False(root.IsSoftDeleted);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ModerateDeleteCommentAsync_NonModeratorDeletingComment_ThrowsUnauthorisedOperationException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var nonModerator = MakeBetaReader();
        owner.Activate();
        nonModerator.Activate();

        var target = Comment.CreateRoot(section.Id, owner.Id, "Root.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        userRepo
            .Setup(r => r.GetByIdAsync(nonModerator.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(nonModerator);

        // Act
        var act = () => sut.ModerateDeleteCommentAsync(target.Id, nonModerator.Id);

        // Assert
        await Assert.ThrowsAsync<UnauthorisedOperationException>(act);
        Assert.False(target.IsSoftDeleted);
        Assert.Null(target.SoftDeletedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ModerateDeleteCommentAsync_ModeratorDeletingRootCommentWithDirectReplies_SoftDeletesTargetAndReplies()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var moderator = MakeAuthor();
        owner.Activate();
        moderator.Activate();

        var target = Comment.CreateRoot(section.Id, owner.Id, "Root.", Visibility.Public);
        var childA = Comment.CreateReply(section.Id, owner.Id, target.Id, Visibility.Public, "Child A.", Visibility.Public);
        var childB = Comment.CreateReply(section.Id, owner.Id, target.Id, Visibility.Public, "Child B.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        userRepo
            .Setup(r => r.GetByIdAsync(moderator.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moderator);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { childA, childB });

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(childA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(childB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        // Act
        await sut.ModerateDeleteCommentAsync(target.Id, moderator.Id);

        // Assert
        Assert.True(target.IsSoftDeleted);
        Assert.True(childA.IsSoftDeleted);
        Assert.True(childB.IsSoftDeleted);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ModerateDeleteCommentAsync_ModeratorDeletingReplyWithNestedReplies_SoftDeletesEntireSubtree()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var moderator = MakeAuthor();
        owner.Activate();
        moderator.Activate();

        var root = Comment.CreateRoot(section.Id, owner.Id, "Root.", Visibility.Public);
        var target = Comment.CreateReply(section.Id, owner.Id, root.Id, Visibility.Public, "Target.", Visibility.Public);
        var child = Comment.CreateReply(section.Id, owner.Id, target.Id, Visibility.Public, "Child.", Visibility.Public);
        var grandchild = Comment.CreateReply(section.Id, owner.Id, child.Id, Visibility.Public, "Grandchild.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        userRepo
            .Setup(r => r.GetByIdAsync(moderator.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moderator);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { child });

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(child.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { grandchild });

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(grandchild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        // Act
        await sut.ModerateDeleteCommentAsync(target.Id, moderator.Id);

        // Assert
        Assert.False(root.IsSoftDeleted);
        Assert.True(target.IsSoftDeleted);
        Assert.True(child.IsSoftDeleted);
        Assert.True(grandchild.IsSoftDeleted);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ModerateDeleteCommentAsync_TargetCommentNotFound_ThrowsEntityNotFoundException()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var moderator = MakeAuthor();
        moderator.Activate();
        var missingCommentId = Guid.NewGuid();

        commentRepo
            .Setup(r => r.GetByIdAsync(missingCommentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Comment?) null);

        // Act
        var act = () => sut.ModerateDeleteCommentAsync(missingCommentId, moderator.Id);

        // Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(act);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ModerateDeleteCommentAsync_ActingUserNotFound_ThrowsEntityNotFoundException()
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

        var target = Comment.CreateRoot(section.Id, owner.Id, "Root.", Visibility.Public);
        var missingUserId = Guid.NewGuid();

        commentRepo
            .Setup(r => r.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        userRepo
            .Setup(r => r.GetByIdAsync(missingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?) null);

        // Act
        var act = () => sut.ModerateDeleteCommentAsync(target.Id, missingUserId);

        // Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(act);
        Assert.False(target.IsSoftDeleted);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ModerateDeleteCommentAsync_ModeratorDeletingCommentWithMixedDeletedAndActiveDescendants_SoftDeletesEntireSubtree()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var moderator = MakeAuthor();
        owner.Activate();
        moderator.Activate();

        var target = Comment.CreateRoot(section.Id, owner.Id, "Root.", Visibility.Public);
        var activeChild = Comment.CreateReply(section.Id, owner.Id, target.Id, Visibility.Public, "Active child.", Visibility.Public);
        var deletedChild = Comment.CreateReply(section.Id, owner.Id, target.Id, Visibility.Public, "Deleted child.", Visibility.Public);
        deletedChild.SoftDelete();
        var grandchild = Comment.CreateReply(section.Id, owner.Id, activeChild.Id, Visibility.Public, "Grandchild.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        userRepo
            .Setup(r => r.GetByIdAsync(moderator.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moderator);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { activeChild, deletedChild });

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(activeChild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { grandchild });

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(deletedChild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(grandchild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        var firstDeletedTimestamp = deletedChild.SoftDeletedAt;

        // Act
        await sut.ModerateDeleteCommentAsync(target.Id, moderator.Id);

        // Assert
        Assert.True(target.IsSoftDeleted);
        Assert.True(activeChild.IsSoftDeleted);
        Assert.True(deletedChild.IsSoftDeleted);
        Assert.Equal(firstDeletedTimestamp, deletedChild.SoftDeletedAt);
        Assert.True(grandchild.IsSoftDeleted);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ModerateDeleteCommentAsync_ModeratorDeletingAlreadySoftDeletedLeaf_CompletesWithoutError()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var moderator = MakeAuthor();
        owner.Activate();
        moderator.Activate();

        var target = Comment.CreateRoot(section.Id, owner.Id, "Root.", Visibility.Public);
        target.SoftDelete();
        var firstDeletedTimestamp = target.SoftDeletedAt;

        commentRepo
            .Setup(r => r.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        userRepo
            .Setup(r => r.GetByIdAsync(moderator.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moderator);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        // Act
        await sut.ModerateDeleteCommentAsync(target.Id, moderator.Id);

        // Assert
        Assert.True(target.IsSoftDeleted);
        Assert.Equal(firstDeletedTimestamp, target.SoftDeletedAt);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ModerateDeleteCommentAsync_ModeratorDeletingAlreadySoftDeletedParent_SoftDeletesRemainingActiveDescendants()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var moderator = MakeAuthor();
        owner.Activate();
        moderator.Activate();

        var target = Comment.CreateRoot(section.Id, owner.Id, "Root.", Visibility.Public);
        target.SoftDelete();
        var child = Comment.CreateReply(section.Id, owner.Id, target.Id, Visibility.Public, "Child.", Visibility.Public);
        var grandchild = Comment.CreateReply(section.Id, owner.Id, child.Id, Visibility.Public, "Grandchild.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        userRepo
            .Setup(r => r.GetByIdAsync(moderator.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moderator);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { child });

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(child.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { grandchild });

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(grandchild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        // Act
        await sut.ModerateDeleteCommentAsync(target.Id, moderator.Id);

        // Assert
        Assert.True(target.IsSoftDeleted);
        Assert.True(child.IsSoftDeleted);
        Assert.True(grandchild.IsSoftDeleted);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ModerateDeleteCommentAsync_ModeratorDeletingSubtree_DoesNotDeleteSiblingBranches()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var moderator = MakeAuthor();
        owner.Activate();
        moderator.Activate();

        var root = Comment.CreateRoot(section.Id, owner.Id, "Root.", Visibility.Public);

        var target = Comment.CreateReply(section.Id, owner.Id, root.Id, Visibility.Public, "Target.", Visibility.Public);
        var targetChild = Comment.CreateReply(section.Id, owner.Id, target.Id, Visibility.Public, "Target child.", Visibility.Public);

        var sibling = Comment.CreateReply(section.Id, owner.Id, root.Id, Visibility.Public, "Sibling.", Visibility.Public);
        var siblingChild = Comment.CreateReply(section.Id, owner.Id, sibling.Id, Visibility.Public, "Sibling child.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        userRepo
            .Setup(r => r.GetByIdAsync(moderator.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moderator);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { targetChild });

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(targetChild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        // Act
        await sut.ModerateDeleteCommentAsync(target.Id, moderator.Id);

        // Assert
        Assert.True(target.IsSoftDeleted);
        Assert.True(targetChild.IsSoftDeleted);

        Assert.False(root.IsSoftDeleted);
        Assert.False(sibling.IsSoftDeleted);
        Assert.False(siblingChild.IsSoftDeleted);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ModerateDeleteCommentAsync_ModeratorDeletingDeepReplyTree_SoftDeletesAllDescendantsRecursively()
    {
        // Arrange
        var commentRepo = new Mock<ICommentRepository>();
        var sectionRepo = new Mock<ISectionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CommentService(commentRepo.Object, sectionRepo.Object, userRepo.Object, unitOfWork.Object, new Mock<IAuthorNotificationRepository>().Object, new Mock<ISectionVersionRepository>().Object);

        var section = MakePublishedSection();
        var owner = MakeBetaReader();
        var moderator = MakeAuthor();
        owner.Activate();
        moderator.Activate();

        var level1 = Comment.CreateRoot(section.Id, owner.Id, "Level 1.", Visibility.Public);
        var level2 = Comment.CreateReply(section.Id, owner.Id, level1.Id, Visibility.Public, "Level 2.", Visibility.Public);
        var level3 = Comment.CreateReply(section.Id, owner.Id, level2.Id, Visibility.Public, "Level 3.", Visibility.Public);
        var level4 = Comment.CreateReply(section.Id, owner.Id, level3.Id, Visibility.Public, "Level 4.", Visibility.Public);
        var level5 = Comment.CreateReply(section.Id, owner.Id, level4.Id, Visibility.Public, "Level 5.", Visibility.Public);

        commentRepo
            .Setup(r => r.GetByIdAsync(level1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(level1);

        userRepo
            .Setup(r => r.GetByIdAsync(moderator.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(moderator);

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(level1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { level2 });

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(level2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { level3 });

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(level3.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { level4 });

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(level4.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { level5 });

        commentRepo
            .Setup(r => r.GetRepliesByParentIdAsync(level5.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        // Act
        await sut.ModerateDeleteCommentAsync(level1.Id, moderator.Id);

        // Assert
        Assert.True(level1.IsSoftDeleted);
        Assert.True(level2.IsSoftDeleted);
        Assert.True(level3.IsSoftDeleted);
        Assert.True(level4.IsSoftDeleted);
        Assert.True(level5.IsSoftDeleted);

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

        section.PublishAsPartOfChapter("hash");
        return section;
    }

    private static User MakeBetaReader() =>
        User.Create("reader@example.com", "Reader", Role.BetaReader);

    private static User MakeAuthor() =>
        User.Create("author@example.com", "Author", Role.Author);
}

