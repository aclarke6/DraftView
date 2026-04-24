using DraftView.Application.Services;
using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.ValueObjects;
using Moq;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for HumanOverrideService permission checks and override orchestration.
/// Covers: comment-owner access, project-author access, unauthorized override rejection,
/// human rejection persistence, and manual relink persistence.
/// Excludes: UI integration.
/// </summary>
public class HumanOverrideServiceTests
{
    private readonly Mock<IPassageAnchorRepository> _anchorRepo = new();
    private readonly Mock<ISectionRepository> _sectionRepo = new();
    private readonly Mock<ICommentRepository> _commentRepo = new();
    private readonly Mock<IProjectRepository> _projectRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPassageAnchorService> _passageAnchorService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private HumanOverrideService CreateSut() => new(
        _anchorRepo.Object,
        _sectionRepo.Object,
        _commentRepo.Object,
        _projectRepo.Object,
        _userRepo.Object,
        _passageAnchorService.Object,
        _unitOfWork.Object);

    [Fact]
    public async Task EnsureCanRejectAsync_WithCommentOwner_AllowsOverride()
    {
        var author = MakeUser("reader@example.test", "Reader", Role.BetaReader);
        var owner = MakeUser("owner@example.test", "Owner", Role.BetaReader);
        var project = Project.Create("Project", "/tmp/project", author.Id, "root");
        var section = MakePublishedSection(project.Id);
        var version = SectionVersion.Create(section, author.Id, 1);
        var anchor = CreateAnchor(section, version, author.Id);
        var comment = Comment.CreateRoot(
            section.Id,
            owner.Id,
            "Anchored comment.",
            Visibility.Public,
            sectionVersionId: version.Id,
            passageAnchorId: anchor.Id);
        var sut = CreateSut();

        SetupAnchorGraph(anchor, section, project, new[] { comment });
        _userRepo.Setup(r => r.GetByIdAsync(owner.Id, default)).ReturnsAsync(owner);

        await sut.EnsureCanRejectAsync(anchor.Id, owner.Id);
    }

    [Fact]
    public async Task EnsureCanRelinkAsync_WithProjectAuthor_AllowsOverride()
    {
        var author = MakeUser("author@example.test", "Author", Role.Author);
        var otherUser = MakeUser("reader@example.test", "Reader", Role.BetaReader);
        var project = Project.Create("Project", "/tmp/project", author.Id, "root");
        var section = MakePublishedSection(project.Id);
        var version = SectionVersion.Create(section, author.Id, 1);
        var anchor = CreateAnchor(section, version, otherUser.Id);
        var comment = Comment.CreateRoot(
            section.Id,
            otherUser.Id,
            "Anchored comment.",
            Visibility.Public,
            sectionVersionId: version.Id,
            passageAnchorId: anchor.Id);
        var sut = CreateSut();

        SetupAnchorGraph(anchor, section, project, new[] { comment });
        _userRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);

        await sut.EnsureCanRelinkAsync(anchor.Id, author.Id);
    }

    [Fact]
    public async Task EnsureCanRejectAsync_WithOtherReader_ThrowsUnauthorisedOperationException()
    {
        var author = MakeUser("author@example.test", "Author", Role.Author);
        var otherUser = MakeUser("reader@example.test", "Reader", Role.BetaReader);
        var project = Project.Create("Project", "/tmp/project", author.Id, "root");
        var section = MakePublishedSection(project.Id);
        var version = SectionVersion.Create(section, author.Id, 1);
        var anchor = CreateAnchor(section, version, author.Id);
        var comment = Comment.CreateRoot(
            section.Id,
            author.Id,
            "Anchored comment.",
            Visibility.Public,
            sectionVersionId: version.Id,
            passageAnchorId: anchor.Id);
        var sut = CreateSut();

        SetupAnchorGraph(anchor, section, project, new[] { comment });
        _userRepo.Setup(r => r.GetByIdAsync(otherUser.Id, default)).ReturnsAsync(otherUser);

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.EnsureCanRejectAsync(anchor.Id, otherUser.Id));
    }

    [Fact]
    public async Task EnsureCanRelinkAsync_WithSystemSupportReader_ThrowsUnauthorisedOperationException()
    {
        var author = MakeUser("author@example.test", "Author", Role.Author);
        var support = MakeUser("support@example.test", "Support", Role.SystemSupport);
        var project = Project.Create("Project", "/tmp/project", author.Id, "root");
        var section = MakePublishedSection(project.Id);
        var version = SectionVersion.Create(section, author.Id, 1);
        var anchor = CreateAnchor(section, version, author.Id);
        var sut = CreateSut();

        SetupAnchorGraph(anchor, section, project, Array.Empty<Comment>());
        _userRepo.Setup(r => r.GetByIdAsync(support.Id, default)).ReturnsAsync(support);

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.EnsureCanRelinkAsync(anchor.Id, support.Id));
    }

    [Fact]
    public async Task RejectAsync_WithCurrentMatch_PersistsRejectionAudit()
    {
        var author = MakeUser("author@example.test", "Author", Role.Author);
        var owner = MakeUser("owner@example.test", "Owner", Role.BetaReader);
        var project = Project.Create("Project", "/tmp/project", author.Id, "root");
        var section = MakePublishedSection(project.Id);
        var version = SectionVersion.Create(section, author.Id, 1);
        var anchor = CreateAnchor(section, version, owner.Id);
        anchor.UpdateCurrentMatch(CreateMatch(version.Id));
        var comment = Comment.CreateRoot(
            section.Id,
            owner.Id,
            "Anchored comment.",
            Visibility.Public,
            sectionVersionId: version.Id,
            passageAnchorId: anchor.Id);
        var sut = CreateSut();

        SetupAnchorGraph(anchor, section, project, new[] { comment });
        _userRepo.Setup(r => r.GetByIdAsync(owner.Id, default)).ReturnsAsync(owner);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await sut.RejectAsync(anchor.Id, owner.Id, "wrong place");

        Assert.Equal(PassageAnchorStatus.UserRejected, result.Status);
        Assert.Null(result.CurrentMatch);
        Assert.NotNull(result.Rejection);
        Assert.Equal(version.Id, result.Rejection!.TargetSectionVersionId);
        Assert.Equal(owner.Id, result.Rejection.RejectedByUserId);
        Assert.Equal("wrong place", result.Rejection.Reason);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task RelinkAsync_WithValidSelection_PersistsManualRelink()
    {
        var author = MakeUser("author@example.test", "Author", Role.Author);
        var owner = MakeUser("owner@example.test", "Owner", Role.BetaReader);
        var project = Project.Create("Project", "/tmp/project", author.Id, "root");
        var section = MakePublishedSection(project.Id);
        var version = SectionVersion.Create(section, author.Id, 1);
        var anchor = CreateAnchor(section, version, owner.Id);
        var comment = Comment.CreateRoot(
            section.Id,
            owner.Id,
            "Anchored comment.",
            Visibility.Public,
            sectionVersionId: version.Id,
            passageAnchorId: anchor.Id);
        var request = new CreatePassageAnchorRequest(
            section.Id,
            version.Id,
            PassageAnchorPurpose.Comment,
            "Alpha beta",
            "Alpha beta",
            "selected-hash",
            string.Empty,
            " gamma",
            0,
            10,
            "content-hash");
        var sut = CreateSut();

        SetupAnchorGraph(anchor, section, project, new[] { comment });
        _userRepo.Setup(r => r.GetByIdAsync(owner.Id, default)).ReturnsAsync(owner);
        _passageAnchorService
            .Setup(s => s.ValidateSelectionAsync(request, owner.Id, default))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await sut.RelinkAsync(anchor.Id, request, owner.Id);

        Assert.Equal(PassageAnchorStatus.UserRelinked, result.Status);
        Assert.NotNull(result.CurrentMatch);
        Assert.Equal(PassageAnchorMatchMethod.ManualRelink, result.CurrentMatch!.MatchMethod);
        Assert.Equal(owner.Id, result.CurrentMatch.ResolvedByUserId);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    private void SetupAnchorGraph(
        PassageAnchor anchor,
        Section section,
        Project project,
        IReadOnlyList<Comment> comments)
    {
        _anchorRepo.Setup(r => r.GetByIdAsync(anchor.Id, default)).ReturnsAsync(anchor);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _projectRepo.Setup(r => r.GetByIdAsync(section.ProjectId, default))
            .ReturnsAsync(project);
        _commentRepo.Setup(r => r.GetAllBySectionIdAsync(section.Id, default))
            .ReturnsAsync(comments);
    }

    private static PassageAnchorMatch CreateMatch(Guid targetVersionId) =>
        PassageAnchorMatch.Create(
            targetVersionId,
            0,
            10,
            "Alpha beta",
            95,
            PassageAnchorMatchMethod.Exact);

    private static PassageAnchor CreateAnchor(Section section, SectionVersion version, Guid createdByUserId) =>
        PassageAnchor.Create(
            section.Id,
            version.Id,
            PassageAnchorPurpose.Comment,
            createdByUserId,
            PassageAnchorSnapshot.Create(
                "Alpha beta",
                "Alpha beta",
                "selected-hash",
                string.Empty,
                " gamma",
                0,
                10,
                "content-hash"));

    private static Section MakePublishedSection(Guid projectId)
    {
        var section = Section.CreateDocument(
            projectId,
            Guid.NewGuid().ToString(),
            "Scene 1",
            null,
            0,
            "<p>Alpha beta gamma</p>",
            "section-hash",
            "Draft");
        section.PublishAsPartOfChapter("section-hash");
        return section;
    }

    private static User MakeUser(string email, string displayName, Role role)
    {
        var user = User.Create(email, displayName, role);
        user.Activate();
        return user;
    }
}
