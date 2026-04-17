using System.ComponentModel.DataAnnotations;
using DraftView.Domain.Entities;
using DraftView.Domain.Notifications;
using Microsoft.AspNetCore.Http;

namespace DraftView.Web.Models;

public class DashboardViewModel
{
    public Project? ActiveProject { get; set; }
    public IReadOnlyList<Project> AllProjects { get; set; } = [];
    public IReadOnlyList<Section> PublishedSections { get; set; } = [];
    public IReadOnlyList<EmailDeliveryLog> EmailFailures { get; set; } = [];
    public int ActiveReaderCount { get; set; }
    public IReadOnlyList<AuthorNotification> Notifications { get; set; } = [];
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
    [Required(ErrorMessage = "Please enter a display name.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Display name must be at least 2 characters.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter an email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
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
    public IReadOnlyList<Project> ProjectsWithAccess { get; init; } = [];
    public IReadOnlyList<Project> ProjectsWithoutAccess { get; init; } = [];
}

/// <summary>
/// Form model for uploading an RTF file to a Manual project section.
/// Used by AuthorController.UploadScene GET and POST.
/// </summary>
public class UploadSceneViewModel
{
    public Guid ProjectId { get; set; }
    public Guid? ParentChapterId { get; set; }

    [Required(ErrorMessage = "Please enter a scene title.")]
    public string SceneTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a file to upload.")]
    public IFormFile? File { get; set; }
}
