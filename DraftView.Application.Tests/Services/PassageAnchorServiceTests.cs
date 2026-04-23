using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.ValueObjects;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for PassageAnchorService create and retrieve orchestration.
/// Covers: access checks, selection validation, anchor persistence, DTO mapping,
/// exact-match relocation, and context disambiguation.
/// Excludes: UI activation, fuzzy relocation, and reader resume integration.
/// </summary>
public class PassageAnchorServiceTests
{
    private readonly Mock<IPassageAnchorRepository> _anchorRepo = new();
    private readonly Mock<ISectionRepository> _sectionRepo = new();
    private readonly Mock<ISectionVersionRepository> _sectionVersionRepo = new();
    private readonly Mock<IReaderAccessRepository> _readerAccessRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IAuthorizationFacade> _authFacade = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private PassageAnchorService CreateSut() => new(
        _anchorRepo.Object,
        _sectionRepo.Object,
        _sectionVersionRepo.Object,
        _readerAccessRepo.Object,
        _userRepo.Object,
        _authFacade.Object,
        _unitOfWork.Object);

    [Fact]
    public async Task CreateAsync_WithAccessibleSectionVersion_CreatesAnchor()
    {
        var reader = MakeReader();
        var section = MakePublishedSection();
        var version = SectionVersion.Create(section, Guid.NewGuid(), 1);
        var request = CreateRequest(section.Id, version.Id, "Alpha beta");
        var sut = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync(version);
        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(reader.Id, section.ProjectId, default))
            .ReturnsAsync(ReaderAccess.Grant(reader.Id, Guid.NewGuid(), section.ProjectId));
        _authFacade.Setup(f => f.IsBetaReader()).Returns(true);

        PassageAnchor? added = null;
        _anchorRepo.Setup(r => r.AddAsync(It.IsAny<PassageAnchor>(), default))
            .Callback<PassageAnchor, CancellationToken>((anchor, _) => added = anchor)
            .Returns(Task.CompletedTask);

        var result = await sut.CreateAsync(request, reader.Id);

        Assert.NotNull(added);
        Assert.Equal(section.Id, result.SectionId);
        Assert.Equal(version.Id, result.OriginalSectionVersionId);
        Assert.Equal(PassageAnchorStatus.Original, result.Status);
        Assert.Equal("Alpha beta", result.OriginalSnapshot.SelectedText);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidSelection_ThrowsInvariantViolationException()
    {
        var reader = MakeReader();
        var section = MakePublishedSection();
        var version = SectionVersion.Create(section, Guid.NewGuid(), 1);
        var request = CreateRequest(section.Id, version.Id, "Wrong text");
        var sut = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync(version);
        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(reader.Id, section.ProjectId, default))
            .ReturnsAsync(ReaderAccess.Grant(reader.Id, Guid.NewGuid(), section.ProjectId));
        _authFacade.Setup(f => f.IsBetaReader()).Returns(true);

        await Assert.ThrowsAsync<InvariantViolationException>(() => sut.CreateAsync(request, reader.Id));
    }

    [Fact]
    public async Task ValidateSelectionAsync_WithValidSelection_DoesNotPersistAnchor()
    {
        var reader = MakeReader();
        var section = MakePublishedSection();
        var version = SectionVersion.Create(section, Guid.NewGuid(), 1);
        var request = CreateRequest(section.Id, version.Id, "Alpha beta");
        var sut = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync(version);
        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(reader.Id, section.ProjectId, default))
            .ReturnsAsync(ReaderAccess.Grant(reader.Id, Guid.NewGuid(), section.ProjectId));
        _authFacade.Setup(f => f.IsBetaReader()).Returns(true);

        await sut.ValidateSelectionAsync(request, reader.Id);

        _anchorRepo.Verify(r => r.AddAsync(It.IsAny<PassageAnchor>(), default), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task ValidateSelectionAsync_WithInlineMarkupSelection_AllowsCanonicalText()
    {
        var reader = MakeReader();
        var section = Section.CreateDocument(
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            "Scene 1",
            null,
            0,
            "<p><strong>Alpha</strong> beta gamma</p>",
            "section-hash",
            "Draft");
        section.PublishAsPartOfChapter("section-hash");
        var version = SectionVersion.Create(section, Guid.NewGuid(), 1);
        var request = CreateRequest(section.Id, version.Id, "Alpha beta");
        var sut = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync(version);
        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(reader.Id, section.ProjectId, default))
            .ReturnsAsync(ReaderAccess.Grant(reader.Id, Guid.NewGuid(), section.ProjectId));
        _authFacade.Setup(f => f.IsBetaReader()).Returns(true);

        await sut.ValidateSelectionAsync(request, reader.Id);

        _anchorRepo.Verify(r => r.AddAsync(It.IsAny<PassageAnchor>(), default), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithUnauthorizedUser_ThrowsUnauthorisedOperationException()
    {
        var reader = MakeReader();
        var section = MakePublishedSection();
        var version = SectionVersion.Create(section, Guid.NewGuid(), 1);
        var request = CreateRequest(section.Id, version.Id, "Alpha beta");
        var sut = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync(version);
        _authFacade.Setup(f => f.IsBetaReader()).Returns(true);
        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(reader.Id, section.ProjectId, default))
            .ReturnsAsync((ReaderAccess?)null);

        await Assert.ThrowsAsync<UnauthorisedOperationException>(() => sut.CreateAsync(request, reader.Id));
    }

    [Fact]
    public async Task ValidateSelectionAsync_WithUnauthorizedUser_ThrowsUnauthorisedOperationException()
    {
        var reader = MakeReader();
        var section = MakePublishedSection();
        var version = SectionVersion.Create(section, Guid.NewGuid(), 1);
        var request = CreateRequest(section.Id, version.Id, "Alpha beta");
        var sut = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync(version);
        _authFacade.Setup(f => f.IsBetaReader()).Returns(true);
        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(reader.Id, section.ProjectId, default))
            .ReturnsAsync((ReaderAccess?)null);

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.ValidateSelectionAsync(request, reader.Id));
    }

    [Fact]
    public async Task GetByIdAsync_WithAccessibleAnchor_ReturnsStatusAndOriginalMetadata()
    {
        var reader = MakeReader();
        var section = MakePublishedSection();
        var version = SectionVersion.Create(section, Guid.NewGuid(), 1);
        var snapshot = PassageAnchorSnapshot.Create(
            "Alpha beta",
            "Alpha beta",
            "hash",
            string.Empty,
            " gamma",
            0,
            10,
            "content-hash");
        var anchor = PassageAnchor.Create(
            section.Id,
            version.Id,
            PassageAnchorPurpose.Comment,
            reader.Id,
            snapshot);
        var sut = CreateSut();

        _anchorRepo.Setup(r => r.GetByIdAsync(anchor.Id, default)).ReturnsAsync(anchor);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _userRepo.Setup(r => r.GetByIdAsync(reader.Id, default)).ReturnsAsync(reader);
        _authFacade.Setup(f => f.IsBetaReader()).Returns(true);
        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(reader.Id, section.ProjectId, default))
            .ReturnsAsync(ReaderAccess.Grant(reader.Id, Guid.NewGuid(), section.ProjectId));

        var result = await sut.GetByIdAsync(anchor.Id, reader.Id);

        Assert.Equal(anchor.Id, result.Id);
        Assert.Equal(PassageAnchorStatus.Original, result.Status);
        Assert.Equal("Alpha beta", result.OriginalSnapshot.SelectedText);
        Assert.Equal(" gamma", result.OriginalSnapshot.SuffixContext);
    }

    [Fact]
    public async Task TryResolveExactMatchAsync_WithUniqueMatch_ReturnsExactMatchWithConfidence100()
    {
        var author = MakeAuthor();
        var section = Section.CreateDocument(
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            "Scene 1",
            null,
            0,
            "<p>Alpha beta gamma</p>",
            "section-hash",
            "Draft");
        section.PublishAsPartOfChapter("section-hash");
        var version = SectionVersion.Create(section, author.Id, 1);
        var anchor = PassageAnchor.Create(
            section.Id,
            version.Id,
            PassageAnchorPurpose.Comment,
            author.Id,
            PassageAnchorSnapshot.Create(
                "Alpha beta",
                "Alpha beta",
                "hash",
                string.Empty,
                " gamma",
                0,
                10,
                "content-hash"));
        var sut = CreateSut();

        _anchorRepo.Setup(r => r.GetByIdAsync(anchor.Id, default)).ReturnsAsync(anchor);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync(version);
        _userRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        _authFacade.Setup(f => f.IsAuthor()).Returns(true);

        var result = await sut.TryResolveExactMatchAsync(anchor.Id, author.Id);

        Assert.NotNull(result);
        Assert.Equal(version.Id, result!.TargetSectionVersionId);
        Assert.Equal(0, result.StartOffset);
        Assert.Equal(10, result.EndOffset);
        Assert.Equal(100, result.ConfidenceScore);
        Assert.Equal(PassageAnchorMatchMethod.Exact, result.MatchMethod);
    }

    [Fact]
    public async Task TryResolveExactMatchAsync_WithDuplicateMatch_ReturnsNull()
    {
        var author = MakeAuthor();
        var section = Section.CreateDocument(
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            "Scene 1",
            null,
            0,
            "<p>Alpha beta gamma Alpha beta</p>",
            "section-hash",
            "Draft");
        section.PublishAsPartOfChapter("section-hash");
        var version = SectionVersion.Create(section, author.Id, 1);
        var anchor = PassageAnchor.Create(
            section.Id,
            version.Id,
            PassageAnchorPurpose.Comment,
            author.Id,
            PassageAnchorSnapshot.Create(
                "Alpha beta",
                "Alpha beta",
                "hash",
                string.Empty,
                " gamma",
                0,
                10,
                "content-hash"));
        var sut = CreateSut();

        _anchorRepo.Setup(r => r.GetByIdAsync(anchor.Id, default)).ReturnsAsync(anchor);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync(version);
        _userRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        _authFacade.Setup(f => f.IsAuthor()).Returns(true);

        var result = await sut.TryResolveExactMatchAsync(anchor.Id, author.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryResolveContextMatchAsync_WithRepeatedTextAndUniqueContext_ReturnsContextMatch()
    {
        var author = MakeAuthor();
        var section = Section.CreateDocument(
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            "Scene 1",
            null,
            0,
            "<p>Alpha beta gamma. Delta Alpha beta omega.</p>",
            "section-hash",
            "Draft");
        section.PublishAsPartOfChapter("section-hash");
        var version = SectionVersion.Create(section, author.Id, 1);
        var anchor = PassageAnchor.Create(
            section.Id,
            version.Id,
            PassageAnchorPurpose.Comment,
            author.Id,
            PassageAnchorSnapshot.Create(
                "Alpha beta",
                "Alpha beta",
                "hash",
                "Delta ",
                " omega",
                0,
                10,
                "content-hash"));
        var sut = CreateSut();

        _anchorRepo.Setup(r => r.GetByIdAsync(anchor.Id, default)).ReturnsAsync(anchor);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync(version);
        _userRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        _authFacade.Setup(f => f.IsAuthor()).Returns(true);

        var result = await sut.TryResolveContextMatchAsync(anchor.Id, author.Id);

        Assert.NotNull(result);
        Assert.Equal(version.Id, result!.TargetSectionVersionId);
        Assert.Equal(80, result.ConfidenceScore);
        Assert.Equal(PassageAnchorMatchMethod.Context, result.MatchMethod);
        Assert.Equal(24, result.StartOffset);
        Assert.Equal(34, result.EndOffset);
    }

    [Fact]
    public async Task TryResolveContextMatchAsync_WithAmbiguousContext_ReturnsNull()
    {
        var author = MakeAuthor();
        var section = Section.CreateDocument(
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            "Scene 1",
            null,
            0,
            "<p>Alpha beta gamma Alpha beta gamma</p>",
            "section-hash",
            "Draft");
        section.PublishAsPartOfChapter("section-hash");
        var version = SectionVersion.Create(section, author.Id, 1);
        var anchor = PassageAnchor.Create(
            section.Id,
            version.Id,
            PassageAnchorPurpose.Comment,
            author.Id,
            PassageAnchorSnapshot.Create(
                "Alpha beta",
                "Alpha beta",
                "hash",
                string.Empty,
                " gamma",
                0,
                10,
                "content-hash"));
        var sut = CreateSut();

        _anchorRepo.Setup(r => r.GetByIdAsync(anchor.Id, default)).ReturnsAsync(anchor);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync(version);
        _userRepo.Setup(r => r.GetByIdAsync(author.Id, default)).ReturnsAsync(author);
        _authFacade.Setup(f => f.IsAuthor()).Returns(true);

        var result = await sut.TryResolveContextMatchAsync(anchor.Id, author.Id);

        Assert.Null(result);
    }

    private static User MakeReader()
    {
        var reader = User.Create("reader@example.com", "Reader", Role.BetaReader);
        reader.Activate();
        return reader;
    }

    private static User MakeAuthor()
    {
        var author = User.Create("author@example.com", "Author", Role.Author);
        author.Activate();
        return author;
    }

    /// <summary>
    /// Creates a published document section with simple reader-visible content for anchor tests.
    /// </summary>
    private static Section MakePublishedSection()
    {
        var section = Section.CreateDocument(
            Guid.NewGuid(),
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

    /// <summary>
    /// Creates a valid anchor request for the known test content shape.
    /// </summary>
    private static CreatePassageAnchorRequest CreateRequest(
        Guid sectionId,
        Guid versionId,
        string normalizedSelectedText)
    {
        return new CreatePassageAnchorRequest(
            sectionId,
            versionId,
            PassageAnchorPurpose.Comment,
            "Alpha beta",
            normalizedSelectedText,
            "selected-hash",
            string.Empty,
            " gamma",
            0,
            10,
            "content-hash");
    }
}
