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

    private readonly Mock<IReadingProgressService>   _progressService    = new();
    private readonly Mock<ISectionRepository>        _sectionRepo        = new();
    private readonly Mock<ICommentRepository>        _commentRepo        = new();
    private readonly Mock<IReadEventRepository>      _readEventRepo      = new();
    private readonly Mock<ISectionVersionRepository> _sectionVersionRepo = new();

    private ReaderDashboardService CreateSut() => new(
        _progressService.Object,
        _sectionRepo.Object,
        _commentRepo.Object,
        _readEventRepo.Object,
        _sectionVersionRepo.Object);

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

    // -----------------------------------------------------------------------
    // GetChapterHasReadStatusesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ChapterHasReadStatuses_EmptyChapterIds_ReturnsEmptyDictionary()
    {
        var result = await CreateSut().GetChapterHasReadStatusesAsync(UserId, []);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ChapterHasReadStatuses_ChapterFolderHasReadEvent_ReturnsTrue()
    {
        var chapterId = Guid.NewGuid();
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default)).ReturnsAsync([]);
        _readEventRepo.Setup(r => r.HasReadAsync(chapterId, UserId, default)).ReturnsAsync(true);

        var result = await CreateSut().GetChapterHasReadStatusesAsync(UserId, [chapterId]);

        Assert.True(result[chapterId]);
    }

    [Fact]
    public async Task ChapterHasReadStatuses_OnlySceneHasReadEvent_ReturnsTrue()
    {
        // Jenny-Bug: reader opened a scene via mobile — no ReadEvent on chapter folder.
        var chapterId = Guid.NewGuid();
        var scene = CreateScene(chapterId);

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default)).ReturnsAsync([scene]);
        _readEventRepo.Setup(r => r.HasReadAsync(chapterId, UserId, default)).ReturnsAsync(false);
        _readEventRepo.Setup(r => r.HasReadAsync(scene.Id, UserId, default)).ReturnsAsync(true);

        var result = await CreateSut().GetChapterHasReadStatusesAsync(UserId, [chapterId]);

        Assert.True(result[chapterId]);
    }

    [Fact]
    public async Task ChapterHasReadStatuses_NoReadEventsAnywhere_ReturnsFalse()
    {
        var chapterId = Guid.NewGuid();
        var scene = CreateScene(chapterId);

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default)).ReturnsAsync([scene]);
        _readEventRepo.Setup(r => r.HasReadAsync(chapterId, UserId, default)).ReturnsAsync(false);
        _readEventRepo.Setup(r => r.HasReadAsync(scene.Id, UserId, default)).ReturnsAsync(false);

        var result = await CreateSut().GetChapterHasReadStatusesAsync(UserId, [chapterId]);

        Assert.False(result[chapterId]);
    }

    [Fact]
    public async Task ChapterHasReadStatuses_MultipleChapters_CorrectDistribution()
    {
        var ch1Id = Guid.NewGuid();
        var ch2Id = Guid.NewGuid();
        var scene1 = CreateScene(ch1Id);
        var scene2 = CreateScene(ch2Id);

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(ch1Id, default)).ReturnsAsync([scene1]);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(ch2Id, default)).ReturnsAsync([scene2]);

        // ch1: scene has been read (mobile path), folder has not
        _readEventRepo.Setup(r => r.HasReadAsync(ch1Id, UserId, default)).ReturnsAsync(false);
        _readEventRepo.Setup(r => r.HasReadAsync(scene1.Id, UserId, default)).ReturnsAsync(true);

        // ch2: nothing read
        _readEventRepo.Setup(r => r.HasReadAsync(ch2Id, UserId, default)).ReturnsAsync(false);
        _readEventRepo.Setup(r => r.HasReadAsync(scene2.Id, UserId, default)).ReturnsAsync(false);

        var result = await CreateSut().GetChapterHasReadStatusesAsync(UserId, [ch1Id, ch2Id]);

        Assert.True(result[ch1Id]);
        Assert.False(result[ch2Id]);
    }

    // -----------------------------------------------------------------------
    // GetChapterChangeStatusesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ChapterChangeStatuses_EmptyChapterIds_ReturnsEmptyDictionary()
    {
        var result = await CreateSut().GetChapterChangeStatusesAsync(
            UserId, [], ReadingStyle.StoryReader);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ChapterChangeStatuses_WhenReaderIsUpToDate_ReturnsNullClassification()
    {
        var chapterId = Guid.NewGuid();
        var scene = CreateScene(chapterId);
        var readEvent = ReadEvent.Create(scene.Id, UserId);
        readEvent.MarkAsRead(1);

        var version = CreateVersion(scene, 1, ChangeClassification.Polish);

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default))
            .ReturnsAsync([scene]);
        _readEventRepo.Setup(r => r.GetAsync(scene.Id, UserId, default))
            .ReturnsAsync(readEvent);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(scene.Id, default))
            .ReturnsAsync(version);

        var result = await CreateSut().GetChapterChangeStatusesAsync(
            UserId, [chapterId], ReadingStyle.StoryReader);

        Assert.Null(result[chapterId]);
    }

    [Fact]
    public async Task ChapterChangeStatuses_WhenSceneUpdated_ReturnsMaxClassification()
    {
        var chapterId = Guid.NewGuid();
        var scene = CreateScene(chapterId);
        var readEvent = ReadEvent.Create(scene.Id, UserId);
        readEvent.MarkAsRead(1);

        var version = CreateVersion(scene, 2, ChangeClassification.Revision);

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default))
            .ReturnsAsync([scene]);
        _readEventRepo.Setup(r => r.GetAsync(scene.Id, UserId, default))
            .ReturnsAsync(readEvent);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(scene.Id, default))
            .ReturnsAsync(version);

        var result = await CreateSut().GetChapterChangeStatusesAsync(
            UserId, [chapterId], ReadingStyle.StoryReader);

        Assert.Equal(ChangeClassification.Revision, result[chapterId]);
    }

    [Fact]
    public async Task ChapterChangeStatuses_WhenClassificationBelowThreshold_ReturnsNull()
    {
        var chapterId = Guid.NewGuid();
        var scene = CreateScene(chapterId);
        var readEvent = ReadEvent.Create(scene.Id, UserId);
        readEvent.MarkAsRead(1);

        // Latest version has Trivial changes — below StoryReader threshold (Polish+)
        var version = CreateVersion(scene, 2, ChangeClassification.Trivial);

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default))
            .ReturnsAsync([scene]);
        _readEventRepo.Setup(r => r.GetAsync(scene.Id, UserId, default))
            .ReturnsAsync(readEvent);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(scene.Id, default))
            .ReturnsAsync(version);

        var result = await CreateSut().GetChapterChangeStatusesAsync(
            UserId, [chapterId], ReadingStyle.StoryReader);

        Assert.Null(result[chapterId]);
    }

    [Fact]
    public async Task ChapterChangeStatuses_WhenNoReadEvent_ReturnsNullClassification()
    {
        var chapterId = Guid.NewGuid();
        var scene = CreateScene(chapterId);

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default))
            .ReturnsAsync([scene]);
        _readEventRepo.Setup(r => r.GetAsync(scene.Id, UserId, default))
            .ReturnsAsync((ReadEvent?)null);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(scene.Id, default))
            .ReturnsAsync(CreateVersion(scene, 1, ChangeClassification.Polish));

        var result = await CreateSut().GetChapterChangeStatusesAsync(
            UserId, [chapterId], ReadingStyle.StoryReader);

        // No read event = never read = "New" state, not "Updated" — no classification
        Assert.Null(result[chapterId]);
    }

    [Fact]
    public async Task ChapterChangeStatuses_MultipleScenes_ReturnsMaxAcrossScenes()
    {
        var chapterId = Guid.NewGuid();
        var scene1    = CreateScene(chapterId);
        var scene2    = CreateScene(chapterId);

        var readEvent1 = ReadEvent.Create(scene1.Id, UserId);
        readEvent1.MarkAsRead(1);
        var readEvent2 = ReadEvent.Create(scene2.Id, UserId);
        readEvent2.MarkAsRead(1);

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default))
            .ReturnsAsync([scene1, scene2]);
        _readEventRepo.Setup(r => r.GetAsync(scene1.Id, UserId, default))
            .ReturnsAsync(readEvent1);
        _readEventRepo.Setup(r => r.GetAsync(scene2.Id, UserId, default))
            .ReturnsAsync(readEvent2);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(scene1.Id, default))
            .ReturnsAsync(CreateVersion(scene1, 2, ChangeClassification.Polish));
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(scene2.Id, default))
            .ReturnsAsync(CreateVersion(scene2, 2, ChangeClassification.Rewrite));

        var result = await CreateSut().GetChapterChangeStatusesAsync(
            UserId, [chapterId], ReadingStyle.StoryReader);

        Assert.Equal(ChangeClassification.Rewrite, result[chapterId]);
    }

    [Fact]
    public async Task ChapterChangeStatuses_WhenNullLastReadVersionNumber_WithVersionMeetingThreshold_ReturnsClassification()
    {
        // Backfill scenario: ReadEvent exists but LastReadVersionNumber is null
        // (reader credited with a pre-versioning read). Any published version counts as pending.
        var chapterId = Guid.NewGuid();
        var scene = CreateScene(chapterId);
        var readEvent = ReadEvent.Create(scene.Id, UserId); // LastReadVersionNumber stays null
        var version = CreateVersion(scene, 1, ChangeClassification.Polish);

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default))
            .ReturnsAsync([scene]);
        _readEventRepo.Setup(r => r.GetAsync(scene.Id, UserId, default))
            .ReturnsAsync(readEvent);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(scene.Id, default))
            .ReturnsAsync(version);

        var result = await CreateSut().GetChapterChangeStatusesAsync(
            UserId, [chapterId], ReadingStyle.StoryReader);

        Assert.Equal(ChangeClassification.Polish, result[chapterId]);
    }

    [Fact]
    public async Task ChapterChangeStatuses_WhenNullLastReadVersionNumber_WithClassificationBelowThreshold_ReturnsNull()
    {
        // Backfill scenario: ReadEvent exists, LastReadVersionNumber is null,
        // but the published version is Trivial — below StoryReader threshold.
        var chapterId = Guid.NewGuid();
        var scene = CreateScene(chapterId);
        var readEvent = ReadEvent.Create(scene.Id, UserId);
        var version = CreateVersion(scene, 1, ChangeClassification.Trivial);

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapterId, default))
            .ReturnsAsync([scene]);
        _readEventRepo.Setup(r => r.GetAsync(scene.Id, UserId, default))
            .ReturnsAsync(readEvent);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(scene.Id, default))
            .ReturnsAsync(version);

        var result = await CreateSut().GetChapterChangeStatusesAsync(
            UserId, [chapterId], ReadingStyle.StoryReader);

        Assert.Null(result[chapterId]);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Section CreateScene(Guid chapterId)
    {
        var scene = Section.CreateDocument(
            Guid.NewGuid(), Guid.NewGuid().ToString(), "Scene",
            chapterId, 1, "<p>content</p>", "hash", "First Draft");
        scene.PublishAsPartOfChapter("hash");
        return scene;
    }

    private static SectionVersion CreateVersion(Section section, int versionNumber, ChangeClassification classification)
    {
        section.UpdateContent("<p>content</p>", "hash");
        var version = SectionVersion.Create(section, Guid.NewGuid(), versionNumber, 1, 0);
        version.SetChangeClassification(classification);
        return version;
    }
}
