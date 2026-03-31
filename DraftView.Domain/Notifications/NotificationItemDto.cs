namespace DraftView.Domain.Notifications;

public enum NotificationEventType
{
    NewComment,
    ReplyToAuthor,
    ReaderJoined,
    SyncCompleted
}

public sealed class NotificationItemDto
{
    public NotificationEventType EventType { get; init; }
    public DateTime OccurredAt { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Detail { get; init; }
    public string? LinkUrl { get; init; }

    public static NotificationItemDto NewComment(
        string readerName, string sectionTitle,
        string bodySnippet, Guid sectionId, DateTime occurredAt) =>
        new()
        {
            EventType  = NotificationEventType.NewComment,
            OccurredAt = occurredAt,
            Title      = $"{readerName} commented on \"{sectionTitle}\"",
            Detail     = Truncate(bodySnippet),
            LinkUrl    = $"/Author/Section/{sectionId}"
        };

    public static NotificationItemDto ReplyToAuthor(
        string readerName, string sectionTitle,
        string bodySnippet, Guid sectionId, DateTime occurredAt) =>
        new()
        {
            EventType  = NotificationEventType.ReplyToAuthor,
            OccurredAt = occurredAt,
            Title      = $"{readerName} replied to your comment on \"{sectionTitle}\"",
            Detail     = Truncate(bodySnippet),
            LinkUrl    = $"/Author/Section/{sectionId}"
        };

    public static NotificationItemDto ReaderJoined(
        string readerName, DateTime occurredAt) =>
        new()
        {
            EventType  = NotificationEventType.ReaderJoined,
            OccurredAt = occurredAt,
            Title      = $"{readerName} accepted their invitation",
            LinkUrl    = "/Author/Readers"
        };

    public static NotificationItemDto SyncCompleted(
        string projectName, DateTime occurredAt) =>
        new()
        {
            EventType  = NotificationEventType.SyncCompleted,
            OccurredAt = occurredAt,
            Title      = $"Sync completed for {projectName}"
        };

    private static string Truncate(string body, int max = 80)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        var t = body.Trim();
        return t.Length <= max ? t : t[..max].TrimEnd() + "\u2026";
    }
}
