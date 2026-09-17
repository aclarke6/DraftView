using DraftView.Domain.Entities;
using DraftView.Domain.Notifications;

namespace DraftView.Domain.Interfaces.Services;

public interface IDashboardService
{
    Task<IReadOnlyList<Section>> GetProjectOverviewAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetReaderSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EmailDeliveryLog>> GetEmailHealthSummaryAsync(CancellationToken ct = default);
    Task<AuthorDashboardProgressDto> GetPublishedChapterProgressAsync(
        Guid projectId, Guid authorId, CancellationToken ct = default);

    Task<IReadOnlyList<AuthorNotification>> GetNotificationsAsync(
        Guid authorId, NotificationFilterGroup? group = null, CancellationToken ct = default);

    Task DismissNotificationAsync(
        Guid notificationId, CancellationToken ct = default);

    Task DismissAllNotificationsAsync(
        Guid authorId, CancellationToken ct = default);

    Task DismissNotificationsByTypeAsync(
        Guid authorId, NotificationEventType? type, CancellationToken ct = default);
}

/// <summary>
/// Dashboard progress data for the published manuscript structure.
/// </summary>
public sealed class AuthorDashboardProgressDto
{
    public required bool UsesStructuralGroups { get; init; }
    public required IReadOnlyList<AuthorDashboardGroupDto> Groups { get; init; }
    public required IReadOnlyList<AuthorDashboardChapterProgressDto> Chapters { get; init; }
}

/// <summary>
/// A top-level structural group such as an Act, Part, or the synthetic ungrouped row.
/// </summary>
public sealed class AuthorDashboardGroupDto
{
    public required string Title { get; init; }
    public required bool IsUngrouped { get; init; }
    public required int ViewedReaderChapterCount { get; init; }
    public required int TotalReaderChapterCount { get; init; }
    public required int CommentCount { get; init; }
    public required int NewCommentCount { get; init; }
    public required DateTime? LatestActivityAt { get; init; }
    public required IReadOnlyList<AuthorDashboardChapterProgressDto> Chapters { get; init; }
}

/// <summary>
/// Progress data for a single published chapter row on the author dashboard.
/// </summary>
public sealed class AuthorDashboardChapterProgressDto
{
    public required Section Chapter { get; init; }
    public required int ViewedReaderCount { get; init; }
    public required int TotalReaderCount { get; init; }
    public required DateTime? LatestViewAt { get; init; }
    public required int CommentCount { get; init; }
    public required int NewCommentCount { get; init; }
    public required AuthorDashboardCommentLinkDto? LatestComment { get; init; }
    public required IReadOnlyList<AuthorDashboardReaderProgressDto> Readers { get; init; }
}

/// <summary>
/// Progress data for a single reader within a published chapter row.
/// </summary>
public sealed class AuthorDashboardReaderProgressDto
{
    public required Guid ReaderId { get; init; }
    public required string ReaderName { get; init; }
    public required bool HasViewed { get; init; }
    public required DateTime? LastViewedAt { get; init; }
    public required int CommentCount { get; init; }
}

/// <summary>
/// Targets the latest comment back to its exact section context.
/// </summary>
public sealed class AuthorDashboardCommentLinkDto
{
    public required Guid SectionId { get; init; }
    public required Guid CommentId { get; init; }
    public required string SectionTitle { get; init; }
    public required DateTime CreatedAt { get; init; }
}
