using Moq;
using DraftView.Domain.Contracts;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.ValueObjects;
using Xunit;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for OriginalContextService retrieval of original anchor context.
/// Covers: versioned original content, legacy fallback, snapshot data, authorization,
/// missing anchor, missing original content, and immutability guarantees.
/// </summary>
public class OriginalContextServiceTests
{
    private readonly Mock<IPassageAnchorRepository> _anchorRepo = new();
    private readonly Mock<ISectionVersionRepository> _sectionVersionRepo = new();
    private readonly Mock<ISectionRepository> _sectionRepo = new();
    private readonly Mock<IReaderAccessRepository> _readerAccessRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IAuthorizationFacade> _authFacade = new();

    private OriginalContextService CreateSut() => new(
        _anchorRepo.Object,
        _sectionVersionRepo.Object,
        _sectionRepo.Object,
        _readerAccessRepo.Object,
        _userRepo.Object,
        _authFacade.Object);

    [Fact]
    public async Task GetOriginalContextAsync_WithVersionedAnchor_LoadsFromSectionVersion()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var snapshot = PassageAnchorSnapshot.Create(
            selectedText: "original text",
            normalizedSelectedText: "original text",
            selectedTextHash: "hash123",
            prefixContext: "before",
            suffixContext: "after",
            startOffset: 10,
            endOffset: 23,
            canonicalContentHash: "contenthash");

        var anchor = PassageAnchor.Create(
            sectionId,
            versionId,
            PassageAnchorPurpose.Comment,
            authorId,
            snapshot);

        var section = Section.CreateDocument(
            Guid.NewGuid(),
            "uuid",
            "Title",
            null,
            0,
            "<p>current content</p>",
            "currenthash",
            null);
        section.PublishAsPartOfChapter("hash");

        var version = SectionVersion.Create(section, authorId, 1);
        // Use reflection to set HtmlContent since it's init-only
        typeof(SectionVersion).GetProperty(nameof(SectionVersion.HtmlContent))!
            .SetValue(version, "<p>before original text after</p>");

        var author = User.Create("author@test.com", "Author", Role.Author);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(author, authorId);

        _anchorRepo.Setup(r => r.GetByIdAsync(anchor.Id, default))
            .ReturnsAsync(anchor);
        _sectionRepo.Setup(r => r.GetByIdAsync(sectionId, default))
            .ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetByIdAsync(versionId, default))
            .ReturnsAsync(version);
        _userRepo.Setup(r => r.GetByIdAsync(authorId, default))
            .ReturnsAsync(author);
        _authFacade.Setup(f => f.IsAuthor()).Returns(true);

        var sut = CreateSut();

        // Act
        var result = await sut.GetOriginalContextAsync(anchor.Id, authorId);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Context);
        Assert.Equal("<p>before original text after</p>", result.Context.OriginalHtmlContent);
        Assert.False(result.Context.IsLegacyFallback);
        Assert.Equal(versionId, result.Context.OriginalSectionVersionId);
        Assert.Equal("original text", result.Context.OriginalSelectedText);
        Assert.Equal(10, result.Context.StartOffset);
        Assert.Equal(23, result.Context.EndOffset);

        // Verify Section.HtmlContent was NOT used
        _sectionRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOriginalContextAsync_WithLegacyAnchor_UsesFallback()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();

        var snapshot = PassageAnchorSnapshot.Create(
            selectedText: "legacy text",
            normalizedSelectedText: "legacy text",
            selectedTextHash: "hash456",
            prefixContext: "pre",
            suffixContext: "post",
            startOffset: 5,
            endOffset: 16,
            canonicalContentHash: "legacyhash");

        var anchor = PassageAnchor.Create(
            sectionId,
            null, // No original version ID
            PassageAnchorPurpose.Resume,
            authorId,
            snapshot);

        var section = Section.CreateDocument(
            Guid.NewGuid(),
            "uuid",
            "Title",
            null,
            0,
            "<p>pre legacy text post</p>",
            "legacyhash",
            null);
        section.PublishAsPartOfChapter("hash");

        var author = User.Create("author@test.com", "Author", Role.Author);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(author, authorId);

        _anchorRepo.Setup(r => r.GetByIdAsync(anchor.Id, default))
            .ReturnsAsync(anchor);
        _sectionRepo.Setup(r => r.GetByIdAsync(sectionId, default))
            .ReturnsAsync(section);
        _userRepo.Setup(r => r.GetByIdAsync(authorId, default))
            .ReturnsAsync(author);
        _authFacade.Setup(f => f.IsAuthor()).Returns(true);

        var sut = CreateSut();

        // Act
        var result = await sut.GetOriginalContextAsync(anchor.Id, authorId);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Context);
        Assert.True(result.Context.IsLegacyFallback);
        Assert.Null(result.Context.OriginalSectionVersionId);
        Assert.Equal("<p>pre legacy text post</p>", result.Context.OriginalHtmlContent);
        Assert.Equal("legacy text", result.Context.OriginalSelectedText);

        // Verify SectionVersion was NOT queried
        _sectionVersionRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOriginalContextAsync_WithMissingAnchor_ReturnsNotFound()
    {
        // Arrange
        var anchorId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _anchorRepo.Setup(r => r.GetByIdAsync(anchorId, default))
            .ReturnsAsync((PassageAnchor?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetOriginalContextAsync(anchorId, userId);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(OriginalContextFailureReason.NotFound, result.FailureReason);
        Assert.Null(result.Context);
    }

    [Fact]
    public async Task GetOriginalContextAsync_WithUnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var unauthorizedUserId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var snapshot = PassageAnchorSnapshot.Create(
            selectedText: "text",
            normalizedSelectedText: "text",
            selectedTextHash: "hash",
            prefixContext: "",
            suffixContext: "",
            startOffset: 0,
            endOffset: 4,
            canonicalContentHash: "hash");

        var anchor = PassageAnchor.Create(
            sectionId,
            versionId,
            PassageAnchorPurpose.Comment,
            authorId,
            snapshot);

        var section = Section.CreateDocument(
            Guid.NewGuid(),
            "uuid",
            "Title",
            null,
            0,
            "<p>content</p>",
            "hash",
            null);
        section.PublishAsPartOfChapter("hash");

        var unauthorizedUser = User.Create("unauth@test.com", "Unauth", Role.BetaReader);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(unauthorizedUser, unauthorizedUserId);

        _anchorRepo.Setup(r => r.GetByIdAsync(anchor.Id, default))
            .ReturnsAsync(anchor);
        _sectionRepo.Setup(r => r.GetByIdAsync(sectionId, default))
            .ReturnsAsync(section);
        _userRepo.Setup(r => r.GetByIdAsync(unauthorizedUserId, default))
            .ReturnsAsync(unauthorizedUser);
        _authFacade.Setup(f => f.IsAuthor()).Returns(false);
        _authFacade.Setup(f => f.IsSystemSupport()).Returns(false);
        _authFacade.Setup(f => f.IsBetaReader()).Returns(true);
        _readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(unauthorizedUserId, section.ProjectId, default))
            .ReturnsAsync((ReaderAccess?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetOriginalContextAsync(anchor.Id, unauthorizedUserId);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(OriginalContextFailureReason.Unauthorized, result.FailureReason);
        Assert.Null(result.Context);
    }

    [Fact]
    public async Task GetOriginalContextAsync_WithMissingOriginalContent_ReturnsOriginalContentMissing()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var snapshot = PassageAnchorSnapshot.Create(
            selectedText: "text",
            normalizedSelectedText: "text",
            selectedTextHash: "hash",
            prefixContext: "",
            suffixContext: "",
            startOffset: 0,
            endOffset: 4,
            canonicalContentHash: "hash");

        var anchor = PassageAnchor.Create(
            sectionId,
            versionId,
            PassageAnchorPurpose.Comment,
            authorId,
            snapshot);

        var section = Section.CreateDocument(
            Guid.NewGuid(),
            "uuid",
            "Title",
            null,
            0,
            "<p>content</p>",
            "hash",
            null);
        section.PublishAsPartOfChapter("hash");

        var author = User.Create("author@test.com", "Author", Role.Author);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(author, authorId);

        _anchorRepo.Setup(r => r.GetByIdAsync(anchor.Id, default))
            .ReturnsAsync(anchor);
        _sectionRepo.Setup(r => r.GetByIdAsync(sectionId, default))
            .ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetByIdAsync(versionId, default))
            .ReturnsAsync((SectionVersion?)null);
        _userRepo.Setup(r => r.GetByIdAsync(authorId, default))
            .ReturnsAsync(author);
        _authFacade.Setup(f => f.IsAuthor()).Returns(true);

        var sut = CreateSut();

        // Act
        var result = await sut.GetOriginalContextAsync(anchor.Id, authorId);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(OriginalContextFailureReason.OriginalContentMissing, result.FailureReason);
        Assert.Null(result.Context);
    }

    [Fact]
    public async Task GetOriginalContextAsync_WithVersionedAnchor_ReturnsVersionMetadata()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-1);

        var snapshot = PassageAnchorSnapshot.Create(
            selectedText: "text",
            normalizedSelectedText: "text",
            selectedTextHash: "hash",
            prefixContext: "",
            suffixContext: "",
            startOffset: 0,
            endOffset: 4,
            canonicalContentHash: "hash");

        var anchor = PassageAnchor.Create(
            sectionId,
            versionId,
            PassageAnchorPurpose.Comment,
            authorId,
            snapshot);

        var section = Section.CreateDocument(
            Guid.NewGuid(),
            "uuid",
            "Title",
            null,
            0,
            "<p>content</p>",
            "hash",
            null);
        section.PublishAsPartOfChapter("hash");

        var version = SectionVersion.Create(section, authorId, 3);
        typeof(SectionVersion).GetProperty(nameof(SectionVersion.HtmlContent))!
            .SetValue(version, "<p>text</p>");
        typeof(SectionVersion).GetProperty(nameof(SectionVersion.CreatedAt))!
            .SetValue(version, createdAt);

        var author = User.Create("author@test.com", "Author", Role.Author);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(author, authorId);

        _anchorRepo.Setup(r => r.GetByIdAsync(anchor.Id, default))
            .ReturnsAsync(anchor);
        _sectionRepo.Setup(r => r.GetByIdAsync(sectionId, default))
            .ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetByIdAsync(versionId, default))
            .ReturnsAsync(version);
        _userRepo.Setup(r => r.GetByIdAsync(authorId, default))
            .ReturnsAsync(author);
        _authFacade.Setup(f => f.IsAuthor()).Returns(true);

        var sut = CreateSut();

        // Act
        var result = await sut.GetOriginalContextAsync(anchor.Id, authorId);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Context);
        Assert.Equal(3, result.Context.OriginalVersionNumber);
        Assert.Equal(createdAt, result.Context.OriginalVersionCreatedAtUtc);
        Assert.Equal("v3", result.Context.OriginalVersionLabel);
    }

    [Fact]
    public async Task GetOriginalContextAsync_ReturnsSnapshotDataFromImmutableOriginal()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var snapshot = PassageAnchorSnapshot.Create(
            selectedText: "exact selected text",
            normalizedSelectedText: "normalized version",
            selectedTextHash: "texthash",
            prefixContext: "prefix content",
            suffixContext: "suffix content",
            startOffset: 100,
            endOffset: 119,
            canonicalContentHash: "canonicalhash");

        var anchor = PassageAnchor.Create(
            sectionId,
            versionId,
            PassageAnchorPurpose.Comment,
            authorId,
            snapshot);

        var section = Section.CreateDocument(
            Guid.NewGuid(),
            "uuid",
            "Title",
            null,
            0,
            "<p>content</p>",
            "hash",
            null);
        section.PublishAsPartOfChapter("hash");

        var version = SectionVersion.Create(section, authorId, 1);
        typeof(SectionVersion).GetProperty(nameof(SectionVersion.HtmlContent))!
            .SetValue(version, "<p>version content</p>");

        var author = User.Create("author@test.com", "Author", Role.Author);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(author, authorId);

        _anchorRepo.Setup(r => r.GetByIdAsync(anchor.Id, default))
            .ReturnsAsync(anchor);
        _sectionRepo.Setup(r => r.GetByIdAsync(sectionId, default))
            .ReturnsAsync(section);
        _sectionVersionRepo.Setup(r => r.GetByIdAsync(versionId, default))
            .ReturnsAsync(version);
        _userRepo.Setup(r => r.GetByIdAsync(authorId, default))
            .ReturnsAsync(author);
        _authFacade.Setup(f => f.IsAuthor()).Returns(true);

        var sut = CreateSut();

        // Act
        var result = await sut.GetOriginalContextAsync(anchor.Id, authorId);

        // Assert
        Assert.True(result.Succeeded);
        var ctx = result.Context!;
        Assert.Equal("exact selected text", ctx.OriginalSelectedText);
        Assert.Equal("normalized version", ctx.NormalizedSelectedText);
        Assert.Equal("prefix content", ctx.PrefixContext);
        Assert.Equal("suffix content", ctx.SuffixContext);
        Assert.Equal(100, ctx.StartOffset);
        Assert.Equal(119, ctx.EndOffset);
    }
}
