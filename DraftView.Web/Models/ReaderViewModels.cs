using DraftView.Domain.Diff;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;

namespace DraftView.Web.Models;

public class DesktopTopLevelViewModel
{
    public IReadOnlyList<Section> TopLevelSections { get; set; } = new List<Section>();
    public string ProjectName { get; set; } = string.Empty;
}

public class DesktopSectionContentsViewModel
{
    public Section TopLevelSection { get; set; } = default!;
    public IReadOnlyList<ContentGroup> Groups { get; set; } = new List<ContentGroup>();
    public string ProjectName { get; set; } = string.Empty;
}

public class ContentGroup
{
    public string Heading { get; set; } = string.Empty;
    public int Depth { get; set; }
    public Section? ChapterSection { get; set; }
    public IReadOnlyList<Section> Scenes { get; set; } = new List<Section>();
    public IReadOnlyList<ContentGroup> SubGroups { get; set; } = new List<ContentGroup>();
}

public class DesktopChapterReadViewModel
{
    public Section Chapter { get; set; } = default!;
    public IReadOnlyList<string> Breadcrumb { get; set; } = new List<string>();
    public IReadOnlyList<SceneWithComments> Scenes { get; set; } = new List<SceneWithComments>();
    public IReadOnlyList<CommentDisplayViewModel> ChapterComments { get; set; } = new List<CommentDisplayViewModel>();
    public DesktopSectionContentsViewModel? BookContents { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid CurrentUserId { get; set; }
    public bool CurrentUserIsModerator { get; set; }
    public ProseFont ProseFont { get; set; } = ProseFont.SystemSerif;
    public ProseFontSize ProseFontSize { get; set; } = ProseFontSize.Medium;
}

public class SceneWithComments
{
    public Section Scene { get; set; } = default!;
    public IReadOnlyList<CommentDisplayViewModel> Comments { get; set; } = new List<CommentDisplayViewModel>();

    /// <summary>
    /// The HTML content to render for this scene. Resolved from the latest
    /// SectionVersion if one exists, falling back to Section.HtmlContent
    /// for pre-versioning published sections.
    /// </summary>
    public string? ResolvedHtmlContent { get; set; }

    /// <summary>
    /// Paragraph-level diff results when the reader has a prior read version
    /// and a newer version exists. Empty when no diff or no changes.
    /// When non-empty, the view renders diff paragraphs instead of ResolvedHtmlContent.
    /// </summary>
    public IReadOnlyList<ParagraphDiffResult> DiffParagraphs { get; set; }
        = Array.Empty<ParagraphDiffResult>();

    /// <summary>
    /// True when DiffParagraphs contains highlighted changes to show the reader.
    /// </summary>
    public bool HasDiff => DiffParagraphs.Any(p => p.Type != DiffResultType.Unchanged);

    /// <summary>
    /// True when the reader has previously read this section and a newer version
    /// now exists. Drives the "Updated since you last read" inline message.
    /// False on first read or when the reader is already on the latest version.
    /// </summary>
    public bool UpdatedSinceLastRead { get; set; }

    /// <summary>
    /// True when the update banner should be shown for this scene.
    /// Requires: reader has read before, a newer version exists, and
    /// the reader has not yet dismissed the banner at the current version.
    /// </summary>
    public bool ShowUpdateBanner { get; set; }

    /// <summary>
    /// The current version number. Used in the banner label.
    /// </summary>
    public int? CurrentVersionNumber { get; set; }

    /// <summary>
    /// The AI-generated one-line summary from the current SectionVersion.
    /// Null when no summary exists for the current version.
    /// Shown inside the update banner when ShowUpdateBanner is true.
    /// </summary>
    public string? AiSummary { get; set; }
}

public class CommentDisplayViewModel
{
    public Comment Comment { get; set; } = default!;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public bool HasChildren { get; set; }
    public bool CanDelete { get; set; }
    public bool IsModerator { get; set; }
    public bool CanEdit { get; set; }
    public bool UseModeratorDelete => !CanDelete && IsModerator;
}

public class AddCommentViewModel
{
    public Guid SectionId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public Guid? ParentCommentId { get; set; }
    public Guid? ReturnSceneId { get; set; }
}

/// <summary>
/// Progress and chapter list for a single project on the reader dashboard.
/// </summary>
public class DesktopProjectViewModel
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TotalChapters { get; set; }
    public int ReadChapters { get; set; }
    public List<DesktopChapterProgressViewModel> PublishedChapters { get; set; } = new();
    public int ProgressPercent => TotalChapters > 0
        ? (int)((double)ReadChapters / TotalChapters * 100)
        : 0;
}

public class DesktopDashboardViewModel
{
    public List<DesktopProjectViewModel> Projects { get; set; } = new();
    public bool HasProjects => Projects.Any();
    public int TotalReadChapters => Projects.Sum(p => p.ReadChapters);
    public int TotalChapters => Projects.Sum(p => p.TotalChapters);
}

public class DesktopChapterProgressViewModel
{
    public Section Chapter { get; set; } = default!;
    public bool HasRead { get; set; }
}
