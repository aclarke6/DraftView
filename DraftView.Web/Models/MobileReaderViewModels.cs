using DraftView.Domain.Diff;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;

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
    /// <summary>
    /// True when the chapter has no published Document children — reader links
    /// directly to Read rather than to the intermediate Scenes list.
    /// </summary>
    public bool IsLeaf { get; set; }
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
/// Chapter-level comments page for mobile.
/// Shows all comments posted against the chapter folder itself.
/// </summary>
public class MobileChapterCommentsViewModel
{
    public Section Chapter { get; set; } = default!;
    public string ProjectName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public IReadOnlyList<CommentDisplayViewModel> Comments { get; set; } = new List<CommentDisplayViewModel>();
    public Guid CurrentUserId { get; set; }
    public bool CurrentUserIsModerator { get; set; }
}

/// <summary>
/// Scene-level comments page for mobile.
/// Shows all comments posted against a specific scene.
/// </summary>
public class MobileSceneCommentsViewModel
{
    public Section Scene { get; set; } = default!;
    public Section Chapter { get; set; } = default!;
    public string ProjectName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public IReadOnlyList<CommentDisplayViewModel> Comments { get; set; } = new List<CommentDisplayViewModel>();
    public Guid CurrentUserId { get; set; }
    public bool CurrentUserIsModerator { get; set; }
}

/// <summary>
/// Single scene read view for mobile — prose only.
/// Comments are accessed via SceneComments page; only the count is carried here.
/// </summary>
public class MobileReadViewModel
{
    public Section Scene { get; set; } = default!;
    public Section Chapter { get; set; } = default!;
    public string ProjectName { get; set; } = string.Empty;
    public int SceneCommentCount { get; set; }
    public Guid? PrevSceneId { get; set; }
    public Guid? NextSceneId { get; set; }
    public ProseFont ProseFont { get; set; } = ProseFont.SystemSerif;
    public ProseFontSize ProseFontSize { get; set; } = ProseFontSize.Medium;
    public bool HasPrev => PrevSceneId.HasValue;
    public bool HasNext => NextSceneId.HasValue;

    public string? ResolvedHtmlContent { get; set; }
    public string ResumeCaptureText { get; set; } = string.Empty;
    public bool HasResumeRestoreTarget { get; set; }
    public int? ResumeRestoreStartOffset { get; set; }
    public int? ResumeRestoreEndOffset { get; set; }
    public PassageAnchorStatus? ResumeRestoreStatus { get; set; }
    public int? ResumeRestoreConfidenceScore { get; set; }
    public PassageAnchorMatchMethod? ResumeRestoreMatchMethod { get; set; }

    /// <summary>
    /// Snapshot-based change state for this scene. Null when reader is up to date.
    /// Populated by IChangeStateService in Phase 5.
    /// </summary>
    public ChangeClassification? ChangeClassification { get; set; }

    /// <summary>
    /// True when the reader's ReadEvent has IsRead = true for this scene.
    /// </summary>
    public bool IsRead { get; set; }

}
