using DraftView.Domain.Entities;
using DraftView.Domain.Notifications;

namespace DraftView.Web.Models;

public class DashboardViewModel
{
    public ScrivenerProject? ActiveProject { get; set; }
    public IReadOnlyList<ScrivenerProject> AllProjects { get; set; } = [];
    public IReadOnlyList<Section> PublishedSections { get; set; } = [];
    public IReadOnlyList<EmailDeliveryLog> EmailFailures { get; set; } = [];
    public int ActiveReaderCount { get; set; }
    public IReadOnlyList<NotificationItemDto> Notifications { get; set; } = [];
}

public class SectionViewModel
{
    public Section Section { get; set; } = default!;
    public string? ChapterTitle { get; set; }
    public IReadOnlyList<Comment> Comments { get; set; } = [];
    public IReadOnlyDictionary<Guid, string> CommentAuthorNames { get; set; } = new Dictionary<Guid, string>();
    public int ReadCount { get; set; }
}

public enum ReaderStatus { Invited, Active, Inactive }

public class ReaderRowViewModel
{
    public Guid   Id          { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email       { get; init; } = string.Empty;
    public ReaderStatus Status { get; init; }
    public DateTime? ActivatedAt { get; init; }
}

public class InviteReaderViewModel
{
    public string Email { get; set; } = string.Empty;
    public bool NeverExpires { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
}

public class ReaderAccessViewModel
{
    public Guid ReaderId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public ReaderStatus Status { get; init; }
    public IReadOnlyList<ScrivenerProject> ProjectsWithAccess { get; init; } = [];
    public IReadOnlyList<ScrivenerProject> ProjectsWithoutAccess { get; init; } = [];
}
