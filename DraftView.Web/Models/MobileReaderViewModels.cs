using DraftView.Domain.Entities;

namespace DraftView.Web.Models;

/// <summary>
/// Chapter list for the mobile chapters screen.
/// Entry point for the mobile reading flow.
/// </summary>
public class MobileChaptersViewModel
{
    public string ProjectName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public List<MobileChapterRowViewModel> Chapters { get; set; } = new();
    public Guid? LastReadSceneId { get; set; }
    public Guid? LastReadChapterId { get; set; }
    public bool HasContinue => LastReadSceneId.HasValue;
}

public class MobileChapterRowViewModel
{
    public Section Chapter { get; set; } = default!;
    public bool HasRead { get; set; }
    public int SceneCount { get; set; }
}

/// <summary>
/// Scene list for a selected chapter on the mobile scenes screen.
/// </summary>
public class MobileScenesViewModel
{
    public string ProjectName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Section Chapter { get; set; } = default!;
    public List<MobileSceneRowViewModel> Scenes { get; set; } = new();
}

public class MobileSceneRowViewModel
{
    public Section Scene { get; set; } = default!;
    public bool HasRead { get; set; }
}

/// <summary>
/// Single scene read view for mobile.
/// Includes prev/next navigation and comments.
/// </summary>
public class MobileReadViewModel
{
    public Section Scene { get; set; } = default!;
    public Section Chapter { get; set; } = default!;
    public string ProjectName { get; set; } = string.Empty;
    public IReadOnlyList<CommentDisplayViewModel> Comments { get; set; } = new List<CommentDisplayViewModel>();
    public Guid? PrevSceneId { get; set; }
    public Guid? NextSceneId { get; set; }
    public Guid CurrentUserId { get; set; }
    public bool CurrentUserIsModerator { get; set; }
    public bool HasPrev => PrevSceneId.HasValue;
    public bool HasNext => NextSceneId.HasValue;

    /// <summary>
    /// The HTML content to render. Latest SectionVersion if exists,
    /// fallback to Scene.HtmlContent for pre-versioning sections.
    /// </summary>
    public string? ResolvedHtmlContent { get; set; }

    /// <summary>
    /// The VersionNumber of the SectionVersion used to resolve content.
    /// Null if no version exists yet (pre-versioning section).
    /// </summary>
    public int? CurrentVersionNumber { get; set; }
}