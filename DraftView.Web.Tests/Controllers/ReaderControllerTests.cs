using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;
using DraftView.Web;
using DraftView.Web.Controllers;
using DraftView.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DraftView.Web.Tests.Controllers;

public class ReaderControllerTests
{
    private readonly Mock<IProjectRepository> projectRepo = new();
    private readonly Mock<ISectionRepository> sectionRepo = new();
    private readonly Mock<ICommentService> commentService = new();
    private readonly Mock<IReadingProgressService> progressService = new();
    private readonly Mock<IUserRepository> userRepo = new();
    private readonly Mock<IUserPreferencesRepository> prefsRepo = new();
    private readonly Mock<IReaderAccessRepository> readerAccessRepo = new();
    private readonly Mock<ISectionVersionRepository> sectionVersionRepo = new();
    private readonly Mock<IReadEventRepository> readEventRepo = new();
    private readonly Mock<ISectionDiffService> sectionDiffService = new();
    private readonly Mock<IPassageAnchorService> passageAnchorService = new();
    private readonly Mock<ILogger<ReaderController>> logger = new();

    [Fact]
    public async Task Read_DesktopRead_PopulatesModelWithStoredProsePreferences()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.Activate();

        var project = Project.Create("Project 1", "/Apps/Scrivener/Project1", user.Id, "project-root");

        var chapter = Section.CreateFolder(project.Id, "chapter-uuid", "Chapter 1", null, 1);
        chapter.MarkAsPublishedContainer();

        var scene = Section.CreateDocument(project.Id, "scene-uuid", "Scene 1", chapter.Id, 1, "<p>Hello</p>", "scene-hash", "Draft");
        scene.PublishAsPartOfChapter("scene-hash");

        var prefs = UserPreferences.CreateForBetaReader(user.Id);
        prefs.UpdateProseFontPreferences(ProseFont.Humanist, ProseFontSize.Large);

        var sut = CreateSut(user, userAgent: "Mozilla/5.0");

        userRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);
        sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync([chapter, scene]);
        projectRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        progressService.Setup(r => r.RecordOpenAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        commentService.Setup(r => r.GetThreadsForSectionAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Comment>());
        prefsRepo.Setup(r => r.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(prefs);

        var result = await sut.Read(chapter.Id);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("DesktopRead", view.ViewName);

        var model = Assert.IsType<DesktopChapterReadViewModel>(view.Model);
        Assert.Equal(ProseFont.Humanist, model.ProseFont);
        Assert.Equal(ProseFontSize.Large, model.ProseFontSize);
    }

    [Fact]
    public async Task Read_MobileRead_PopulatesModelWithStoredProsePreferences()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.Activate();

        var project = Project.Create("Project 1", "/Apps/Scrivener/Project1", user.Id, "project-root");

        var chapter = Section.CreateFolder(project.Id, "chapter-uuid", "Chapter 1", null, 1);
        chapter.MarkAsPublishedContainer();

        var scene = Section.CreateDocument(project.Id, "scene-uuid", "Scene 1", chapter.Id, 1, "<p>Hello</p>", "scene-hash", "Draft");
        scene.PublishAsPartOfChapter("scene-hash");

        var prefs = UserPreferences.CreateForBetaReader(user.Id);
        prefs.UpdateProseFontPreferences(ProseFont.Classic, ProseFontSize.ExtraLarge);

        var sut = CreateSut(user, userAgent: "Mozilla/5.0 (iPhone)");

        userRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        sectionRepo.Setup(r => r.GetByIdAsync(scene.Id, It.IsAny<CancellationToken>())).ReturnsAsync(scene);
        sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);
        sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync([chapter, scene]);
        projectRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        progressService.Setup(r => r.RecordOpenAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        commentService.Setup(r => r.GetThreadsForSectionAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Comment>());
        prefsRepo.Setup(r => r.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(prefs);

        var result = await sut.Read(scene.Id);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("MobileRead", view.ViewName);

        var model = Assert.IsType<MobileReadViewModel>(view.Model);
        Assert.Equal(ProseFont.Classic, model.ProseFont);
        Assert.Equal(ProseFontSize.ExtraLarge, model.ProseFontSize);
    }

    [Fact]
    public async Task Read_Desktop_WithVersionAndDiff_DoesNotRenderDiffAsProse()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.Activate();

        var project = Project.Create("Project 1", "/Apps/Scrivener/Project1", user.Id, "project-root");
        var chapter = Section.CreateFolder(project.Id, "chapter-uuid", "Chapter 1", null, 1);
        chapter.MarkAsPublishedContainer();
        var scene = Section.CreateDocument(project.Id, "scene-uuid", "Scene 1", chapter.Id, 1, "<p>Working unpublished text</p>", "scene-hash", "Draft");
        scene.PublishAsPartOfChapter("scene-hash");

        var publishedSection = Section.CreateDocument(project.Id, "scene-published", "Scene 1", chapter.Id, 1, "<p>Published text</p>", "published-hash", "Published");
        var latestVersion = SectionVersion.Create(publishedSection, Guid.NewGuid(), 1);

        var sut = CreateSut(user, userAgent: "Mozilla/5.0");

        userRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);
        sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync([chapter, scene]);
        projectRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        sectionVersionRepo.Setup(r => r.GetLatestAsync(scene.Id, It.IsAny<CancellationToken>())).ReturnsAsync(latestVersion);
        readEventRepo.Setup(r => r.GetAsync(scene.Id, user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ReadEvent.Create(scene.Id, user.Id));
        sectionDiffService.Setup(s => s.GetDiffForReaderAsync(scene.Id, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SectionDiffResult
            {
                FromVersionNumber = 1,
                CurrentVersionNumber = 2,
                HasChanges = true,
                Paragraphs = [new DraftView.Domain.Diff.ParagraphDiffResult("Unpublished", "<p>Working unpublished text</p>", DraftView.Domain.Enumerations.DiffResultType.Added)]
            });
        progressService.Setup(r => r.RecordOpenAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        progressService.Setup(r => r.UpdateLastReadVersionAsync(scene.Id, user.Id, latestVersion.VersionNumber, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        commentService.Setup(r => r.GetThreadsForSectionAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Comment>());

        var result = await sut.Read(chapter.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DesktopChapterReadViewModel>(view.Model);
        var renderedScene = Assert.Single(model.Scenes);
        Assert.Equal("<p>Published text</p>", renderedScene.ResolvedHtmlContent);
        Assert.False(renderedScene.HasDiff);
    }

    [Fact]
    public async Task Read_Mobile_WhenBannerShown_WithCurrentVersion_SetsVersionNumber()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.Activate();

        var project = Project.Create("Project 1", "/Apps/Scrivener/Project1", user.Id, "project-root");
        var chapter = Section.CreateFolder(project.Id, "chapter-uuid", "Chapter 1", null, 1);
        chapter.MarkAsPublishedContainer();
        var scene = Section.CreateDocument(project.Id, "scene-uuid", "Scene 1", chapter.Id, 1, "<p>Working</p>", "scene-hash", "Draft");
        scene.PublishAsPartOfChapter("scene-hash");

        var publishedSection = Section.CreateDocument(project.Id, "scene-published", "Scene 1", chapter.Id, 1, "<p>Published text</p>", "published-hash", "Published");
        var latestVersion = SectionVersion.Create(publishedSection, Guid.NewGuid(), 3);
        var readEvent = ReadEvent.Create(scene.Id, user.Id);
        readEvent.UpdateLastReadVersion(1);

        var sut = CreateSut(user, userAgent: "Mozilla/5.0 (iPhone)");

        userRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        sectionRepo.Setup(r => r.GetByIdAsync(scene.Id, It.IsAny<CancellationToken>())).ReturnsAsync(scene);
        sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);
        sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync([chapter, scene]);
        projectRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        sectionVersionRepo.Setup(r => r.GetLatestAsync(scene.Id, It.IsAny<CancellationToken>())).ReturnsAsync(latestVersion);
        readEventRepo.Setup(r => r.GetAsync(scene.Id, user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(readEvent);
        sectionDiffService.Setup(s => s.GetDiffForReaderAsync(scene.Id, readEvent.LastReadVersionNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SectionDiffResult
            {
                FromVersionNumber = 1,
                CurrentVersionNumber = 3,
                HasChanges = true,
                Paragraphs = []
            });
        progressService.Setup(r => r.RecordOpenAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        progressService.Setup(r => r.UpdateLastReadVersionAsync(scene.Id, user.Id, latestVersion.VersionNumber, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        commentService.Setup(r => r.GetThreadsForSectionAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Comment>());

        var result = await sut.Read(scene.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MobileReadViewModel>(view.Model);
        Assert.True(model.ShowUpdateBanner);
        Assert.Equal(3, model.CurrentVersionNumber);
    }

    [Fact]
    public async Task Read_Desktop_WhenResumeRestoreTargetExists_PopulatesSceneResumeRestoreMetadata()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.Activate();

        var project = Project.Create("Project 1", "/Apps/Scrivener/Project1", user.Id, "project-root");
        var chapter = Section.CreateFolder(project.Id, "chapter-uuid", "Chapter 1", null, 1);
        chapter.MarkAsPublishedContainer();
        var scene = Section.CreateDocument(project.Id, "scene-uuid", "Scene 1", chapter.Id, 1, "<p>Hello</p>", "scene-hash", "Draft");
        scene.PublishAsPartOfChapter("scene-hash");
        var latestVersion = SectionVersion.Create(
            Section.CreateDocument(project.Id, "scene-published", "Scene 1", chapter.Id, 1, "<p>Hello</p>", "published-hash", "Published"),
            Guid.NewGuid(),
            2);

        var sut = CreateSut(user, userAgent: "Mozilla/5.0");

        userRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);
        sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync([chapter, scene]);
        projectRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        sectionVersionRepo.Setup(r => r.GetLatestAsync(scene.Id, It.IsAny<CancellationToken>())).ReturnsAsync(latestVersion);
        readEventRepo.Setup(r => r.GetAsync(scene.Id, user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ReadEvent.Create(scene.Id, user.Id));
        sectionDiffService.Setup(s => s.GetDiffForReaderAsync(scene.Id, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SectionDiffResult?)null);
        progressService.Setup(r => r.RecordOpenAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        progressService.Setup(r => r.UpdateLastReadVersionAsync(scene.Id, user.Id, latestVersion.VersionNumber, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        progressService.Setup(r => r.GetResumeRestoreTargetAsync(scene.Id, latestVersion.Id, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResumeRestoreTargetDto(
                Guid.NewGuid(),
                scene.Id,
                latestVersion.Id,
                PassageAnchorStatus.Context,
                true,
                12,
                22,
                "Alpha beta",
                84,
                PassageAnchorMatchMethod.Context));
        commentService.Setup(r => r.GetThreadsForSectionAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Comment>());

        var result = await sut.Read(chapter.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DesktopChapterReadViewModel>(view.Model);
        var renderedScene = Assert.Single(model.Scenes);
        Assert.True(renderedScene.HasResumeRestoreTarget);
        Assert.Equal(12, renderedScene.ResumeRestoreStartOffset);
        Assert.Equal(22, renderedScene.ResumeRestoreEndOffset);
        Assert.Equal(PassageAnchorStatus.Context, renderedScene.ResumeRestoreStatus);
        Assert.Equal(84, renderedScene.ResumeRestoreConfidenceScore);
        Assert.Equal(PassageAnchorMatchMethod.Context, renderedScene.ResumeRestoreMatchMethod);
    }

    [Fact]
    public async Task Read_Desktop_WhenResumeRestoreTargetCrossVersion_PopulatesSceneResumeRestoreMetadata()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.Activate();

        var project = Project.Create("Project 1", "/Apps/Scrivener/Project1", user.Id, "project-root");
        var chapter = Section.CreateFolder(project.Id, "chapter-uuid", "Chapter 1", null, 1);
        chapter.MarkAsPublishedContainer();
        var scene = Section.CreateDocument(project.Id, "scene-uuid", "Scene 1", chapter.Id, 1, "<p>Hello</p>", "scene-hash", "Draft");
        scene.PublishAsPartOfChapter("scene-hash");
        var latestVersion = SectionVersion.Create(
            Section.CreateDocument(project.Id, "scene-published", "Scene 1", chapter.Id, 1, "<p>Hello</p>", "published-hash", "Published"),
            Guid.NewGuid(),
            2);

        var sut = CreateSut(user, userAgent: "Mozilla/5.0");

        userRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);
        sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync([chapter, scene]);
        projectRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        sectionVersionRepo.Setup(r => r.GetLatestAsync(scene.Id, It.IsAny<CancellationToken>())).ReturnsAsync(latestVersion);
        readEventRepo.Setup(r => r.GetAsync(scene.Id, user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ReadEvent.Create(scene.Id, user.Id));
        sectionDiffService.Setup(s => s.GetDiffForReaderAsync(scene.Id, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SectionDiffResult?)null);
        progressService.Setup(r => r.RecordOpenAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        progressService.Setup(r => r.UpdateLastReadVersionAsync(scene.Id, user.Id, latestVersion.VersionNumber, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        progressService.Setup(r => r.GetResumeRestoreTargetAsync(scene.Id, latestVersion.Id, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResumeRestoreTargetDto(
                Guid.NewGuid(),
                scene.Id,
                latestVersion.Id,
                PassageAnchorStatus.Exact,
                true,
                0,
                10,
                "Alpha beta",
                100,
                PassageAnchorMatchMethod.Exact));
        commentService.Setup(r => r.GetThreadsForSectionAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Comment>());

        var result = await sut.Read(chapter.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DesktopChapterReadViewModel>(view.Model);
        var renderedScene = Assert.Single(model.Scenes);
        Assert.True(renderedScene.HasResumeRestoreTarget);
        Assert.Equal(0, renderedScene.ResumeRestoreStartOffset);
        Assert.Equal(10, renderedScene.ResumeRestoreEndOffset);
        Assert.Equal(PassageAnchorStatus.Exact, renderedScene.ResumeRestoreStatus);
        Assert.Equal(100, renderedScene.ResumeRestoreConfidenceScore);
        Assert.Equal(PassageAnchorMatchMethod.Exact, renderedScene.ResumeRestoreMatchMethod);
    }

    [Fact]
    public async Task Read_Mobile_WhenResumeRestoreFallsBack_PopulatesSafeFallbackMetadata()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.Activate();

        var project = Project.Create("Project 1", "/Apps/Scrivener/Project1", user.Id, "project-root");
        var chapter = Section.CreateFolder(project.Id, "chapter-uuid", "Chapter 1", null, 1);
        chapter.MarkAsPublishedContainer();
        var scene = Section.CreateDocument(project.Id, "scene-uuid", "Scene 1", chapter.Id, 1, "<p>Hello</p>", "scene-hash", "Draft");
        scene.PublishAsPartOfChapter("scene-hash");
        var latestVersion = SectionVersion.Create(
            Section.CreateDocument(project.Id, "scene-published", "Scene 1", chapter.Id, 1, "<p>Hello</p>", "published-hash", "Published"),
            Guid.NewGuid(),
            2);

        var sut = CreateSut(user, userAgent: "Mozilla/5.0 (iPhone)");

        userRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        sectionRepo.Setup(r => r.GetByIdAsync(scene.Id, It.IsAny<CancellationToken>())).ReturnsAsync(scene);
        sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);
        sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync([chapter, scene]);
        projectRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        sectionVersionRepo.Setup(r => r.GetLatestAsync(scene.Id, It.IsAny<CancellationToken>())).ReturnsAsync(latestVersion);
        readEventRepo.Setup(r => r.GetAsync(scene.Id, user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ReadEvent.Create(scene.Id, user.Id));
        sectionDiffService.Setup(s => s.GetDiffForReaderAsync(scene.Id, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SectionDiffResult?)null);
        progressService.Setup(r => r.RecordOpenAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        progressService.Setup(r => r.UpdateLastReadVersionAsync(scene.Id, user.Id, latestVersion.VersionNumber, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        progressService.Setup(r => r.GetResumeRestoreTargetAsync(scene.Id, latestVersion.Id, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResumeRestoreTargetDto(
                Guid.NewGuid(),
                scene.Id,
                latestVersion.Id,
                PassageAnchorStatus.Orphaned,
                false,
                null,
                null,
                null,
                null,
                null));
        commentService.Setup(r => r.GetThreadsForSectionAsync(It.IsAny<Guid>(), user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Comment>());

        var result = await sut.Read(scene.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MobileReadViewModel>(view.Model);
        Assert.False(model.HasResumeRestoreTarget);
        Assert.Null(model.ResumeRestoreStartOffset);
        Assert.Null(model.ResumeRestoreEndOffset);
        Assert.Equal(PassageAnchorStatus.Orphaned, model.ResumeRestoreStatus);
        Assert.Null(model.ResumeRestoreConfidenceScore);
        Assert.Null(model.ResumeRestoreMatchMethod);
    }

    [Fact]
    public async Task CaptureResumePosition_ValidRequest_CallsProgressServiceAndReturnsOk()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.Activate();
        var sut = CreateSut(user, userAgent: "Mozilla/5.0");
        var request = new CaptureResumePositionRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Alpha beta",
            "Alpha beta",
            "selected-hash",
            string.Empty,
            " gamma",
            0,
            10,
            "content-hash",
            "#scene");

        userRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        progressService.Setup(r => r.CaptureResumePositionAsync(request, user.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await sut.CaptureResumePosition(request);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task CaptureResumePosition_InvalidRequest_ReturnsBadRequest()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.Activate();
        var sut = CreateSut(user, userAgent: "Mozilla/5.0");
        var request = new CaptureResumePositionRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            string.Empty);

        userRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        progressService.Setup(r => r.CaptureResumePositionAsync(request, user.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvariantViolationException("I-ANCHOR-SELECTION", "Invalid position."));

        var result = await sut.CaptureResumePosition(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CaptureResumePosition_InaccessibleContent_ReturnsForbid()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.Activate();
        var sut = CreateSut(user, userAgent: "Mozilla/5.0");
        var request = new CaptureResumePositionRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Alpha beta",
            "Alpha beta",
            "selected-hash",
            string.Empty,
            " gamma",
            0,
            10,
            "content-hash",
            "#scene");

        userRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        progressService.Setup(r => r.CaptureResumePositionAsync(request, user.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorisedOperationException("Forbidden"));

        var result = await sut.CaptureResumePosition(request);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CapturePassageAnchorSelection_ValidRequest_ReturnsOk()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.Activate();
        var sut = CreateSut(user, userAgent: "Mozilla/5.0");
        var request = new CreatePassageAnchorRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PassageAnchorPurpose.Comment,
            "Alpha beta",
            "Alpha beta",
            "selected-hash",
            string.Empty,
            " gamma",
            0,
            10,
            "content-hash",
            "#scene");

        userRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        passageAnchorService.Setup(s => s.ValidateSelectionAsync(request, user.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await sut.CapturePassageAnchorSelection(request);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task CapturePassageAnchorSelection_InvalidRequest_ReturnsBadRequest()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.Activate();
        var sut = CreateSut(user, userAgent: "Mozilla/5.0");
        var request = new CreatePassageAnchorRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PassageAnchorPurpose.Comment,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            string.Empty,
            "#scene");

        userRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        passageAnchorService.Setup(s => s.ValidateSelectionAsync(request, user.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvariantViolationException("I-ANCHOR-SELECTION", "Invalid selection."));

        var result = await sut.CapturePassageAnchorSelection(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CapturePassageAnchorSelection_InaccessibleContent_ReturnsForbid()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.Activate();
        var sut = CreateSut(user, userAgent: "Mozilla/5.0");
        var request = new CreatePassageAnchorRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PassageAnchorPurpose.Comment,
            "Alpha beta",
            "Alpha beta",
            "selected-hash",
            string.Empty,
            " gamma",
            0,
            10,
            "content-hash",
            "#scene");

        userRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        passageAnchorService.Setup(s => s.ValidateSelectionAsync(request, user.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorisedOperationException("Forbidden"));

        var result = await sut.CapturePassageAnchorSelection(request);

        Assert.IsType<ForbidResult>(result);
    }

    private ReaderController CreateSut(User user, string userAgent)
    {
        var controller = new ReaderController(
            projectRepo.Object,
            sectionRepo.Object,
            commentService.Object,
            progressService.Object,
            userRepo.Object,
            prefsRepo.Object,
            readerAccessRepo.Object,
            sectionVersionRepo.Object,
            readEventRepo.Object,
            sectionDiffService.Object,
            passageAnchorService.Object,
            logger.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        [new Claim(ClaimTypes.Name, user.Email)],
                        "TestAuth"))
            }
        };

        controller.ControllerContext.HttpContext.Request.Headers.UserAgent = userAgent;

        return controller;
    }
}

public class ReaderReadRenderingRegressionTests : IClassFixture<ReaderReadRenderingRegressionTests.ReaderReadFactory>
{
    private readonly ReaderReadFactory factory;

    public ReaderReadRenderingRegressionTests(ReaderReadFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Read_Desktop_RendersModelDrivenProseDataAttributes()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, TestAuthHandler.ReaderMode);

        var response = await client.GetAsync($"/Reader/Read/{factory.ChapterId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Matches(
            new Regex("<div\\s+class=\\\"reader-page\\\"[^>]*data-prose-font=\\\"Humanist\\\"[^>]*data-prose-font-size=\\\"Large\\\"", RegexOptions.IgnoreCase),
            html);
    }

    [Fact]
    public async Task Read_Desktop_RendersSceneVersionLabel_WhenVersionExists()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, TestAuthHandler.ReaderMode);

        var response = await client.GetAsync($"/Reader/Read/{factory.ChapterId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Matches(new Regex("href=\"#scene-[^\"]+\"[^>]*>\\s*Scene 1\\s*\\(v2\\)\\s*</a>", RegexOptions.IgnoreCase), html);
    }

    [Fact]
    public async Task Read_Desktop_DoesNotRenderPersistentVersionLabel_InMainSceneHeading()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, TestAuthHandler.ReaderMode);

        var response = await client.GetAsync($"/Reader/Read/{factory.ChapterId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("scene-version-label", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Read_Mobile_RendersSceneVersionLabel_WhenVersionExists()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, TestAuthHandler.ReaderMode);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (iPhone)");

        var response = await client.GetAsync($"/Reader/Read/{factory.SceneId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Scenes", html);
        Assert.Contains("v2", html);
        Assert.DoesNotContain("scene-version-label", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Read_Desktop_RendersAnchorResumeIntegration_WithScrollFallbackScript()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, TestAuthHandler.ReaderMode);

        var response = await client.GetAsync($"/Reader/Read/{factory.ChapterId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("data-resume-restore-has-target=", html);
        Assert.Contains("data-resume-restore-start-offset=", html);
        Assert.Contains("data-resume-restore-end-offset=", html);
        Assert.Contains("scroll_chapter_", html);
        Assert.Contains("data-resume-restore-has-target", html);
        Assert.Contains("/Reader/CapturePassageAnchorSelection", html);
    }

    [Fact]
    public async Task Read_Mobile_RendersPassageAnchorSelectionCaptureScript()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, TestAuthHandler.ReaderMode);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (iPhone)");

        var response = await client.GetAsync($"/Reader/Read/{factory.SceneId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("/Reader/CapturePassageAnchorSelection", html);
    }

    [Fact]
    public async Task Read_Mobile_RendersAnchorResumeIntegration_Metadata()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, TestAuthHandler.ReaderMode);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (iPhone)");

        var response = await client.GetAsync($"/Reader/Read/{factory.SceneId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("data-resume-restore-has-target=", html);
        Assert.Contains("data-resume-restore-start-offset=", html);
        Assert.Contains("data-resume-restore-end-offset=", html);
        Assert.Contains("data-resume-restore-status=", html);
    }

    [Fact]
    public async Task Read_Desktop_RendersLegacyScrollFallback_WhenNoResumeRestoreTarget()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, TestAuthHandler.ReaderMode);

        var response = await client.GetAsync($"/Reader/Read/{factory.ChapterId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("data-resume-restore-has-target=\"false\"", html);
        Assert.Contains("scroll_chapter_", html);
    }

    public sealed class ReaderReadFactory : WebApplicationFactory<Program>
    {
        public static readonly Guid ReaderId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private readonly User reader;
        private readonly Project project;
        private readonly Section chapter;
        private readonly Section scene;
        private readonly UserPreferences prefs;
        private readonly SectionVersion latestVersion;

        public Guid ChapterId => chapter.Id;
        public Guid SceneId => scene.Id;

        public ReaderReadFactory()
        {
            reader = User.Create("reader.render@example.test", "Reader", Role.BetaReader);
            reader.Activate();

            project = Project.Create("Project 1", "/Apps/Scrivener/Project1", reader.Id, "project-root");

            chapter = Section.CreateFolder(project.Id, "chapter-uuid", "Chapter 1", null, 1);
            chapter.MarkAsPublishedContainer();

            scene = Section.CreateDocument(project.Id, "scene-uuid", "Scene 1", chapter.Id, 1, "<p>Hello</p>", "scene-hash", "Draft");
            scene.PublishAsPartOfChapter("scene-hash");

            var publishedScene = Section.CreateDocument(project.Id, "scene-uuid-version", "Scene 1", chapter.Id, 1, "<p>Hello</p>", "scene-hash", "Published");
            latestVersion = SectionVersion.Create(publishedScene, ReaderId, 2);

            prefs = UserPreferences.CreateForBetaReader(reader.Id);
            prefs.UpdateProseFontPreferences(ProseFont.Humanist, ProseFontSize.Large);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=draftview_tests;Username=test;Password=test",
                    ["EmailProtection:EncryptionKey"] = "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=",
                    ["EmailProtection:LookupHmacKey"] = "RkVEQ0JBOTg3NjU0MzIxMEZFRENCQTk4NzY1NDMyMTA=",
                    ["Email:Provider"] = "Console"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();

                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });

                services.PostConfigureAll<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                });

                var userRepo = new Mock<IUserRepository>();
                userRepo.Setup(r => r.GetByIdAsync(ReaderId, It.IsAny<CancellationToken>())).ReturnsAsync(reader);
                userRepo.Setup(r => r.GetByIdAsync(reader.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reader);
                userRepo.Setup(r => r.GetByEmailAsync("reader.render@example.test", It.IsAny<CancellationToken>())).ReturnsAsync(reader);

                var prefsRepo = new Mock<IUserPreferencesRepository>();
                prefsRepo.Setup(r => r.GetByUserIdAsync(reader.Id, It.IsAny<CancellationToken>())).ReturnsAsync(prefs);

                var projectRepo = new Mock<IProjectRepository>();
                projectRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

                var sectionRepo = new Mock<ISectionRepository>();
                sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);
                sectionRepo.Setup(r => r.GetByIdAsync(scene.Id, It.IsAny<CancellationToken>())).ReturnsAsync(scene);
                sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync([chapter, scene]);

                var commentService = new Mock<ICommentService>();
                commentService.Setup(r => r.GetThreadsForSectionAsync(It.IsAny<Guid>(), reader.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Array.Empty<Comment>());

                var progressService = new Mock<IReadingProgressService>();
                progressService.Setup(r => r.RecordOpenAsync(It.IsAny<Guid>(), reader.Id, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                var readEventRepo = new Mock<IReadEventRepository>();
                readEventRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), reader.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ReadEvent?)null);

                var sectionDiffService = new Mock<ISectionDiffService>();
                sectionDiffService.Setup(s => s.GetDiffForReaderAsync(It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Domain.Contracts.SectionDiffResult?)null);

                var systemStateMessageService = new Mock<ISystemStateMessageService>();
                systemStateMessageService.Setup(s => s.GetActiveMessageAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync((SystemStateMessage?)null);

                services.RemoveAll<IUserRepository>();
                services.RemoveAll<IUserPreferencesRepository>();
                services.RemoveAll<IProjectRepository>();
                services.RemoveAll<ISectionRepository>();
                services.RemoveAll<ICommentService>();
                services.RemoveAll<IReadingProgressService>();
                services.RemoveAll<IReaderAccessRepository>();
                services.RemoveAll<ISectionVersionRepository>();
                services.RemoveAll<IReadEventRepository>();
                services.RemoveAll<ISectionDiffService>();
                services.RemoveAll<ISystemStateMessageService>();

                var sectionVersionRepo = new Mock<ISectionVersionRepository>();
                sectionVersionRepo.Setup(r => r.GetLatestAsync(scene.Id, It.IsAny<CancellationToken>())).ReturnsAsync(latestVersion);

                services.AddSingleton(userRepo.Object);
                services.AddSingleton(prefsRepo.Object);
                services.AddSingleton(projectRepo.Object);
                services.AddSingleton(sectionRepo.Object);
                services.AddSingleton(commentService.Object);
                services.AddSingleton(progressService.Object);
                services.AddSingleton(Mock.Of<IReaderAccessRepository>());
                services.AddSingleton(sectionVersionRepo.Object);
                services.AddSingleton(readEventRepo.Object);
                services.AddSingleton(sectionDiffService.Object);
                services.AddSingleton(systemStateMessageService.Object);
            });
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "ReaderReadTestAuth";
        public const string HeaderName = "X-Test-Auth";
        public const string ReaderMode = "Reader";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(HeaderName, out var mode))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (!string.Equals(mode, ReaderMode, StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, ReaderReadFactory.ReaderId.ToString()),
                new Claim(ClaimTypes.Name, "reader.render@example.test"),
                new Claim(ClaimTypes.Role, Role.BetaReader.ToString())
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
