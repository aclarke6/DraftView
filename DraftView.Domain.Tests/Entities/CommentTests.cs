using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class CommentTests
{
    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid UserId    = Guid.NewGuid();

    // ---------------------------------------------------------------------------
    // CreateRoot
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateRoot_WithValidPublicData_ReturnsComment()
    {
        var before = DateTime.UtcNow;

        var comment = Comment.CreateRoot(SectionId, UserId, "Great scene!", Visibility.Public);

        Assert.NotEqual(Guid.Empty, comment.Id);
        Assert.Equal(SectionId, comment.SectionId);
        Assert.Equal(UserId, comment.AuthorId);
        Assert.Null(comment.ParentCommentId);
        Assert.Equal("Great scene!", comment.Body);
        Assert.Equal(Visibility.Public, comment.Visibility);
        Assert.False(comment.IsSoftDeleted);
        Assert.Null(comment.EditedAt);
        Assert.Null(comment.SoftDeletedAt);
        Assert.True(comment.CreatedAt >= before);
    }

    [Fact]
    public void CreateRoot_WithPrivateVisibility_ReturnsPrivateComment()
    {
        var comment = Comment.CreateRoot(SectionId, UserId, "Private thought.", Visibility.Private);

        Assert.Equal(Visibility.Private, comment.Visibility);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRoot_WithInvalidBody_ThrowsInvariantViolationException(string? body)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => Comment.CreateRoot(SectionId, UserId, body, Visibility.Public));
#pragma warning restore CS8604

        Assert.Equal("I-07", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // CreateReply
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateReply_WithPublicParent_ReturnsReplyWithRequestedVisibility()
    {
        var parent = Comment.CreateRoot(SectionId, UserId, "Original.", Visibility.Public);

        var reply = Comment.CreateReply(SectionId, UserId, parent.Id, Visibility.Public, "Reply.", Visibility.Public);

        Assert.Equal(parent.Id, reply.ParentCommentId);
        Assert.Equal(SectionId, reply.SectionId);
        Assert.Equal(Visibility.Public, reply.Visibility);
    }

    [Fact]
    public void CreateReply_WithPrivateParent_ForcesPrivateVisibility()
    {
        var parent = Comment.CreateRoot(SectionId, UserId, "Private.", Visibility.Private);

        var reply = Comment.CreateReply(SectionId, UserId, parent.Id, Visibility.Private, "Reply.", Visibility.Public);

        Assert.Equal(Visibility.Private, reply.Visibility);
    }

    [Fact]
    public void CreateReply_WithPrivateParent_ForcesPrivateEvenWhenPublicRequested()
    {
        var parent = Comment.CreateRoot(SectionId, UserId, "Private.", Visibility.Private);

        var reply = Comment.CreateReply(SectionId, UserId, parent.Id, Visibility.Private, "My reply.", Visibility.Public);

        Assert.Equal(Visibility.Private, reply.Visibility);
        Assert.Equal("I-03 enforced", "I-03 enforced"); // documents intent
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateReply_WithInvalidBody_ThrowsInvariantViolationException(string? body)
    {
        var parent = Comment.CreateRoot(SectionId, UserId, "Original.", Visibility.Public);

#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => Comment.CreateReply(SectionId, UserId, parent.Id, Visibility.Public, body, Visibility.Public));
#pragma warning restore CS8604

        Assert.Equal("I-07", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // Edit
    // ---------------------------------------------------------------------------

    [Fact]
    public void Edit_WithValidBody_UpdatesBodyAndSetsEditedAt()
    {
        var comment = Comment.CreateRoot(SectionId, UserId, "Original.", Visibility.Public);
        var before = DateTime.UtcNow;

        comment.Edit("Updated body.");

        Assert.Equal("Updated body.", comment.Body);
        Assert.NotNull(comment.EditedAt);
        Assert.True(comment.EditedAt >= before);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Edit_WithInvalidBody_ThrowsInvariantViolationException(string? body)
    {
        var comment = Comment.CreateRoot(SectionId, UserId, "Original.", Visibility.Public);

#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => comment.Edit(body));
#pragma warning restore CS8604

        Assert.Equal("I-07", ex.InvariantCode);
    }

    [Fact]
    public void Edit_WhenSoftDeleted_ThrowsInvariantViolationException()
    {
        var comment = Comment.CreateRoot(SectionId, UserId, "Original.", Visibility.Public);
        comment.SoftDelete();

        var ex = Assert.Throws<InvariantViolationException>(
            () => comment.Edit("Updated."));

        Assert.Equal("I-EDIT-DELETED", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // SoftDelete
    // ---------------------------------------------------------------------------

    [Fact]
    public void SoftDelete_SetsFlagsAndRecordsTimestamp()
    {
        var comment = Comment.CreateRoot(SectionId, UserId, "Original.", Visibility.Public);
        var before = DateTime.UtcNow;

        comment.SoftDelete();

        Assert.True(comment.IsSoftDeleted);
        Assert.NotNull(comment.SoftDeletedAt);
        Assert.True(comment.SoftDeletedAt >= before);
    }

    [Fact]
    public void SoftDelete_WhenAlreadyDeleted_DoesNotChangeSoftDeletedAt()
    {
        var comment = Comment.CreateRoot(SectionId, UserId, "Original.", Visibility.Public);
        comment.SoftDelete();
        var firstDeletion = comment.SoftDeletedAt;

        comment.SoftDelete();

        Assert.Equal(firstDeletion, comment.SoftDeletedAt);
    }

    // ---------------------------------------------------------------------------
    // IsVisibleTo
    // ---------------------------------------------------------------------------

    [Fact]
    public void IsVisibleTo_PublicComment_IsVisibleToAnyUser()
    {
        var comment = Comment.CreateRoot(SectionId, UserId, "Public.", Visibility.Public);
        var otherUserId = Guid.NewGuid();

        Assert.True(comment.IsVisibleTo(otherUserId, Role.BetaReader));
    }

    [Fact]
    public void IsVisibleTo_PrivateComment_IsVisibleToAuthorRole()
    {
        var comment = Comment.CreateRoot(SectionId, UserId, "Private.", Visibility.Private);
        var authorId = Guid.NewGuid();

        Assert.True(comment.IsVisibleTo(authorId, Role.Author));
    }

    [Fact]
    public void IsVisibleTo_PrivateComment_IsVisibleToCommentAuthor()
    {
        var comment = Comment.CreateRoot(SectionId, UserId, "Private.", Visibility.Private);

        Assert.True(comment.IsVisibleTo(UserId, Role.BetaReader));
    }

    [Fact]
    public void IsVisibleTo_PrivateComment_IsNotVisibleToOtherBetaReader()
    {
        var comment = Comment.CreateRoot(SectionId, UserId, "Private.", Visibility.Private);
        var otherUserId = Guid.NewGuid();

        Assert.False(comment.IsVisibleTo(otherUserId, Role.BetaReader));
    }

    // Add these test methods to DraftView.Domain.Tests\Entities\CommentTests.cs
    // ---------------------------------------------------------------------------
    // Status â€” initial state
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateRoot_ReaderComment_HasStatusNew()
    {
        var comment = Comment.CreateRoot(Guid.NewGuid(), Guid.NewGuid(), "Body", Visibility.Public, isReaderComment: true);
        Assert.Equal(CommentStatus.New, comment.Status);
    }

    [Fact]
    public void CreateRoot_AuthorComment_HasStatusAuthorReply()
    {
        var comment = Comment.CreateRoot(Guid.NewGuid(), Guid.NewGuid(), "Body", Visibility.Public, isReaderComment: false);
        Assert.Equal(CommentStatus.AuthorReply, comment.Status);
    }

    [Fact]
    public void CreateReply_AlwaysHasStatusAuthorReply()
    {
        var reply = Comment.CreateReply(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Visibility.Public, "Reply body", Visibility.Public);
        Assert.Equal(CommentStatus.AuthorReply, reply.Status);
    }

    // ---------------------------------------------------------------------------
    // Status â€” SetStatus
    // ---------------------------------------------------------------------------

    [Fact]
    public void SetStatus_OnReaderComment_UpdatesStatus()
    {
        var comment = Comment.CreateRoot(Guid.NewGuid(), Guid.NewGuid(), "Body", Visibility.Public, isReaderComment: true);
        comment.SetStatus(CommentStatus.Todo);
        Assert.Equal(CommentStatus.Todo, comment.Status);
    }

    [Fact]
    public void SetStatus_OnAuthorReply_ThrowsInvariantViolation()
    {
        var reply = Comment.CreateReply(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Visibility.Public, "Reply body", Visibility.Public);

        var ex = Assert.Throws<InvariantViolationException>(() => reply.SetStatus(CommentStatus.Done));
        Assert.Equal("I-COMMENT-STATUS", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // Status â€” MarkDoneByReply
    // ---------------------------------------------------------------------------

    [Fact]
    public void MarkDoneByReply_WhenNew_SetsDone()
    {
        var comment = Comment.CreateRoot(Guid.NewGuid(), Guid.NewGuid(), "Body", Visibility.Public, isReaderComment: true);
        Assert.Equal(CommentStatus.New, comment.Status);

        comment.MarkDoneByReply();

        Assert.Equal(CommentStatus.Done, comment.Status);
    }

    [Fact]
    public void MarkDoneByReply_WhenAlreadyTodo_DoesNotOverride()
    {
        var comment = Comment.CreateRoot(Guid.NewGuid(), Guid.NewGuid(), "Body", Visibility.Public, isReaderComment: true);
        comment.SetStatus(CommentStatus.Todo);

        comment.MarkDoneByReply();

        // MarkDoneByReply only overrides New â€” an explicit status is preserved
        Assert.Equal(CommentStatus.Todo, comment.Status);
    }

}

