using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Notifications;

namespace DraftView.Domain.Tests.Entities;

public class AuthorNotificationTests
{
    private static readonly Guid ValidAuthorId = Guid.NewGuid();
    private static readonly DateTime ValidOccurredAt =
        new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_SetsAllProperties()
    {
        var n = AuthorNotification.Create(
            ValidAuthorId,
            NotificationEventType.NewComment,
            "Alice commented on \"Chapter 1\"",
            "Great opening!",
            "/Author/Section/abc",
            ValidOccurredAt);

        Assert.NotEqual(Guid.Empty, n.Id);
        Assert.Equal(ValidAuthorId, n.AuthorId);
        Assert.Equal(NotificationEventType.NewComment, n.EventType);
        Assert.Equal("Alice commented on \"Chapter 1\"", n.Title);
        Assert.Equal("Great opening!", n.Detail);
        Assert.Equal("/Author/Section/abc", n.LinkUrl);
        Assert.Equal(ValidOccurredAt, n.OccurredAt);
    }

    [Fact]
    public void Create_Throws_WhenAuthorIdIsEmpty()
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            AuthorNotification.Create(
                Guid.Empty,
                NotificationEventType.NewComment,
                "Title",
                null,
                null,
                ValidOccurredAt));

        Assert.Equal("I-NOTIF-01", ex.InvariantCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenTitleIsNullOrWhitespace(string? title)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(() =>
            AuthorNotification.Create(
                ValidAuthorId,
                NotificationEventType.NewComment,
                title,
                null,
                null,
                ValidOccurredAt));
#pragma warning restore CS8604

        Assert.Equal("I-NOTIF-02", ex.InvariantCode);
    }

    [Fact]
    public void Create_AllowsNullDetailAndLinkUrl()
    {
        var n = AuthorNotification.Create(
            ValidAuthorId,
            NotificationEventType.SyncCompleted,
            "Sync completed",
            null,
            null,
            ValidOccurredAt);

        Assert.Null(n.Detail);
        Assert.Null(n.LinkUrl);
    }

    [Fact]
    public void Create_SetsOccurredAt()
    {
        var at = new DateTime(2026, 3, 15, 9, 30, 0, DateTimeKind.Utc);

        var n = AuthorNotification.Create(
            ValidAuthorId,
            NotificationEventType.ReaderJoined,
            "Bob joined",
            null,
            null,
            at);

        Assert.Equal(at, n.OccurredAt);
    }
}
