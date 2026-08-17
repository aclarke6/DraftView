using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

public class ReaderDashboardServiceTests
{
    private static readonly Guid UserId    = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();

    private readonly Mock<IReadingProgressService> _progressService = new();
    private readonly Mock<ISectionRepository>      _sectionRepo     = new();
    private readonly Mock<ICommentRepository>      _commentRepo     = new();

    private ReaderDashboardService CreateSut() => new(
        _progressService.Object,
        _sectionRepo.Object,
        _commentRepo.Object);

    // -----------------------------------------------------------------------
    // GetCrossProjectResumeTargetAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResumeTarget_EmptyProjects_ReturnsNull()
    {
        var result = await CreateSut().GetCrossProjectResumeTargetAsync(UserId, []);
        Assert.Null(result);
    }

    [Fact]
    public async Task ResumeTarget_NoReadEvents_ReturnsNull()
    {
        _progressService.Setup(s => s.GetLastReadEventAsync(UserId, ProjectId, default))
            .ReturnsAsync((ReadEvent?)null);

        var result = await CreateSut().GetCrossProjectResumeTargetAsync(UserId, [ProjectId]);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResumeTarget_ReadEventOnScene_ReturnsChapterAndScene()
    {
        var chapter = Section.CreateFolder(ProjectId, "UUID-CH", "Chapter 1", null, 0);
        chapter.MarkAsPublishedContainer();
        var scene = Section.CreateDocument(ProjectId, "UUID-SC", "Scene 1",
            chapter.Id, 0, "<p>x</p>", "h", null);
        scene.PublishAsPartOfChapter("h");
        var evt = ReadEvent.Create(scene.Id, UserId);

        _progressService.Setup(s => s.GetLastReadEventAsync(UserId, ProjectId, default))
            .ReturnsAsync(evt);
        _sectionRepo.Setup(r => r.GetByIdAsync(scene.Id, default)).ReturnsAsync(scene);
        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default)).ReturnsAsync(chapter);

        var result = await CreateSut().GetCrossProjectResumeTargetAsync(UserId, [ProjectId]);

        Assert.NotNull(result);
        Assert.Equal(chapter.Id, result!.ChapterId);
        Assert.Equal(scene.Id, result.SceneId);
    }

    [Fact]
    public async Task ResumeTarget_ReadEventOnChapter_ReturnsChapterNoScene()
    {
        var chapter = Section.CreateFolder(ProjectId, "UUID-CH", "Chapter 1", null, 0);
        chapter.MarkAsPublishedContainer();
        var evt = ReadEvent.Create(chapter.Id, UserId);

        _progressService.Setup(s => s.GetLastReadEventAsync(UserId, ProjectId, default))
            .ReturnsAsync(evt);
        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default)).ReturnsAsync(chapter);

        var result = await CreateSut().GetCrossProjectResumeTargetAsync(UserId, [ProjectId]);

        Assert.NotNull(result);
        Assert.Equal(chapter.Id, result!.ChapterId);
        Assert.Null(result.SceneId);
    }

    [Fact]
    public async Task ResumeTarget_MultipleProjects_ReturnsMostRecent()
    {
        var proj1 = Guid.NewGuid();
        var proj2 = Guid.NewGuid();

        var ch1 = Section.CreateFolder(proj1, "UUID-CH1", "Chapter A", null, 0);
        ch1.MarkAsPublishedContainer();
        var ch2 = Section.CreateFolder(proj2, "UUID-CH2", "Chapter B", null, 0);
        ch2.MarkAsPublishedContainer();

        var oldEvent    = ReadEvent.Create(ch1.Id, UserId);
        await Task.Delay(5);
        var recentEvent = ReadEvent.Create(ch2.Id, UserId);

        _progressService.Setup(s => s.GetLastReadEventAsync(UserId, proj1, default)).ReturnsAsync(oldEvent);
        _progressService.Setup(s => s.GetLastReadEventAsync(UserId, proj2, default)).ReturnsAsync(recentEvent);
        _sectionRepo.Setup(r => r.GetByIdAsync(ch2.Id, default)).ReturnsAsync(ch2);

        var result = await CreateSut().GetCrossProjectResumeTargetAsync(UserId, [proj1, proj2]);

        Assert.NotNull(result);
        Assert.Equal(ch2.Id, result!.ChapterId);
    }

    [Fact]
    public async Task ResumeTarget_UnpublishedSection_ReturnsNull()
    {
        var chapter = Section.CreateFolder(ProjectId, "UUID-CH", "Chapter 1", null, 0);
        // Not published
        var evt = ReadEvent.Create(chapter.Id, UserId);

        _progressService.Setup(s => s.GetLastReadEventAsync(UserId, ProjectId, default))
            .ReturnsAsync(evt);
        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default)).ReturnsAsync(chapter);

        var result = await CreateSut().GetCrossProjectResumeTargetAsync(UserId, [ProjectId]);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResumeTarget_SceneWithUnpublishedParentChapter_ReturnsNull()
    {
        var chapter = Section.CreateFolder(ProjectId, "UUID-CH", "Chapter 1", null, 0);
        // Not published
        var scene = Section.CreateDocument(ProjectId, "UUID-SC", "Scene 1",
            chapter.Id, 0, "<p>x</p>", "h", null);
        scene.PublishAsPartOfChapter("h");
        var evt = ReadEvent.Create(scene.Id, UserId);

        _progressService.Setup(s => s.GetLastReadEventAsync(UserId, ProjectId, default))
            .ReturnsAsync(evt);
        _sectionRepo.Setup(r => r.GetByIdAsync(scene.Id, default)).ReturnsAsync(scene);
        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default)).ReturnsAsync(chapter);

        var result = await CreateSut().GetCrossProjectResumeTargetAsync(UserId, [ProjectId]);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResumeTarget_SectionNotFound_ReturnsNull()
    {
        var sectionId = Guid.NewGuid();
        var evt = ReadEvent.Create(sectionId, UserId);

        _progressService.Setup(s => s.GetLastReadEventAsync(UserId, ProjectId, default))
            .ReturnsAsync(evt);
        _sectionRepo.Setup(r => r.GetByIdAsync(sectionId, default)).ReturnsAsync((Section?)null);

        var result = await CreateSut().GetCrossProjectResumeTargetAsync(UserId, [ProjectId]);

        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // GetReaderChapterCommentCountsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CommentCounts_EmptyChapters_ReturnsEmptyDict()
    {
        var result = await CreateSut().GetReaderChapterCommentCountsAsync(UserId, []);
        Assert.Empty(result);
    }

    [Fact]
    public async Task CommentCounts_NoComments_ReturnsZeroPerChapter()
    {
        var chapterId = Guid.NewGuid();
        _commentRepo.Setup(r => r.GetByAuthorIdAsync(UserId, default))
            .ReturnsAsync([]);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default))
            .ReturnsAsync([]);

        var result = await CreateSut().GetReaderChapterCommentCountsAsync(UserId, [chapterId]);

        Assert.Equal(0, result[chapterId]);
    }

    [Fact]
    public async Task CommentCounts_RootCommentOnChapter_CountsOne()
    {
        var chapterId = Guid.NewGuid();
        var comment = Comment.CreateRoot(chapterId, UserId, "Great!", Visibility.Public);

        _commentRepo.Setup(r => r.GetByAuthorIdAsync(UserId, default))
            .ReturnsAsync([comment]);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default))
            .ReturnsAsync([]);

        var result = await CreateSut().GetReaderChapterCommentCountsAsync(UserId, [chapterId]);

        Assert.Equal(1, result[chapterId]);
    }

    [Fact]
    public async Task CommentCounts_RootCommentOnScene_CountsTowardChapter()
    {
        var chapter = Section.CreateFolder(ProjectId, "UUID-CH", "Chapter 1", null, 0);
        var scene   = Section.CreateDocument(ProjectId, "UUID-SC", "Scene 1", chapter.Id, 0, null, null, null);
        var comment = Comment.CreateRoot(scene.Id, UserId, "Nice scene!", Visibility.Public);

        _commentRepo.Setup(r => r.GetByAuthorIdAsync(UserId, default))
            .ReturnsAsync([comment]);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync([scene]);

        var result = await CreateSut().GetReaderChapterCommentCountsAsync(UserId, [chapter.Id]);

        Assert.Equal(1, result[chapter.Id]);
    }

    [Fact]
    public async Task CommentCounts_Reply_NotCounted()
    {
        var chapterId     = Guid.NewGuid();
        var root          = Comment.CreateRoot(chapterId, UserId, "Root", Visibility.Public);
        var reply         = Comment.CreateReply(chapterId, UserId, root.Id,
            Visibility.Public, "Reply text", Visibility.Public);

        _commentRepo.Setup(r => r.GetByAuthorIdAsync(UserId, default))
            .ReturnsAsync([root, reply]);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default))
            .ReturnsAsync([]);

        var result = await CreateSut().GetReaderChapterCommentCountsAsync(UserId, [chapterId]);

        Assert.Equal(1, result[chapterId]);
    }

    [Fact]
    public async Task CommentCounts_SoftDeletedComment_NotCounted()
    {
        var chapterId = Guid.NewGuid();
        var comment   = Comment.CreateRoot(chapterId, UserId, "Deleted!", Visibility.Public);
        comment.SoftDelete();

        _commentRepo.Setup(r => r.GetByAuthorIdAsync(UserId, default))
            .ReturnsAsync([comment]);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default))
            .ReturnsAsync([]);

        var result = await CreateSut().GetReaderChapterCommentCountsAsync(UserId, [chapterId]);

        Assert.Equal(0, result[chapterId]);
    }

    [Fact]
    public async Task CommentCounts_MultipleChapters_CorrectDistribution()
    {
        var ch1Id = Guid.NewGuid();
        var ch2Id = Guid.NewGuid();
        var c1a   = Comment.CreateRoot(ch1Id, UserId, "A", Visibility.Public);
        var c1b   = Comment.CreateRoot(ch1Id, UserId, "B", Visibility.Public);
        var c2    = Comment.CreateRoot(ch2Id, UserId, "C", Visibility.Public);

        _commentRepo.Setup(r => r.GetByAuthorIdAsync(UserId, default))
            .ReturnsAsync([c1a, c1b, c2]);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(ch1Id, default)).ReturnsAsync([]);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(ch2Id, default)).ReturnsAsync([]);

        var result = await CreateSut().GetReaderChapterCommentCountsAsync(UserId, [ch1Id, ch2Id]);

        Assert.Equal(2, result[ch1Id]);
        Assert.Equal(1, result[ch2Id]);
    }
}
