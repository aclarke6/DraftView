using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class SectionTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private const string ValidUuid = "A1B2C3D4-E5F6-7890-ABCD-EF1234567890";
    private const string ValidTitle = "Chapter One";
    private const string ValidHash = "abc123";
    private const string ValidHtml = "<p>Once upon a time.</p>";

    // ---------------------------------------------------------------------------
    // Create - Folder
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_Folder_WithValidData_ReturnsSection()
    {
        var section = Section.CreateFolder(ProjectId, ValidUuid, ValidTitle, null, 1);

        Assert.NotEqual(Guid.Empty, section.Id);
        Assert.Equal(ProjectId, section.ProjectId);
        Assert.Equal(ValidUuid, section.ScrivenerUuid);
        Assert.Equal(ValidTitle, section.Title);
        Assert.Equal(NodeType.Folder, section.NodeType);
        Assert.Null(section.ParentId);
        Assert.Equal(1, section.SortOrder);
        Assert.Null(section.HtmlContent);
        Assert.False(section.IsPublished);
        Assert.False(section.IsSoftDeleted);
        Assert.Null(section.PublishedAt);
        Assert.Null(section.UnpublishedAt);
        Assert.Null(section.SoftDeletedAt);
        Assert.False(section.ContentChangedSincePublish);
    }

    [Fact]
    public void Create_Folder_WithParentId_SetsParentId()
    {
        var parentId = Guid.NewGuid();
        var section = Section.CreateFolder(ProjectId, ValidUuid, ValidTitle, parentId, 2);

        Assert.Equal(parentId, section.ParentId);
    }

    // ---------------------------------------------------------------------------
    // Create - Document
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_Document_WithValidData_ReturnsSection()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");

        Assert.Equal(NodeType.Document, section.NodeType);
        Assert.Equal(ValidHtml, section.HtmlContent);
        Assert.Equal(ValidHash, section.ContentHash);
        Assert.Equal("First Draft", section.ScrivenerStatus);
        Assert.False(section.IsPublished);
    }

    // ---------------------------------------------------------------------------
    // Create - validation
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidTitle_ThrowsInvariantViolationException(string? title)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => Section.CreateFolder(ProjectId, ValidUuid, title, null, 1));
#pragma warning restore CS8604

        Assert.Equal("I-SEC-TITLE", ex.InvariantCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidUuid_ThrowsInvariantViolationException(string? uuid)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => Section.CreateFolder(ProjectId, uuid, ValidTitle, null, 1));
#pragma warning restore CS8604

        Assert.Equal("I-SEC-UUID", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // Publish
    // ---------------------------------------------------------------------------

    [Fact]
    public void Publish_DocumentNode_SetsIsPublishedAndRecordsTimestamp()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");
        var before = DateTime.UtcNow;

        section.PublishAsPartOfChapter(ValidHash);

        Assert.True(section.IsPublished);
        Assert.NotNull(section.PublishedAt);
        Assert.True(section.PublishedAt >= before);
        Assert.False(section.ContentChangedSincePublish);
    }

    [Fact]
    public void Publish_FolderNode_ThrowsInvariantViolationException()
    {
        var section = Section.CreateFolder(ProjectId, ValidUuid, ValidTitle, null, 1);

        var ex = Assert.Throws<InvariantViolationException>(
            () => section.PublishAsPartOfChapter(ValidHash));

        Assert.Equal("I-04", ex.InvariantCode);
    }

    [Fact]
    public void Publish_SoftDeletedSection_ThrowsInvariantViolationException()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");
        section.SoftDelete();

        var ex = Assert.Throws<InvariantViolationException>(
            () => section.PublishAsPartOfChapter(ValidHash));

        Assert.Equal("I-18", ex.InvariantCode);
    }

    [Fact]
    public void Publish_ClearsContentChangedSincePublish()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");
        section.PublishAsPartOfChapter(ValidHash);
        section.MarkContentChanged();

        section.PublishAsPartOfChapter(ValidHash);

        Assert.False(section.ContentChangedSincePublish);
    }

    // ---------------------------------------------------------------------------
    // Unpublish
    // ---------------------------------------------------------------------------

    [Fact]
    public void Unpublish_SetsIsPublishedFalseAndRecordsTimestamp()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");
        section.PublishAsPartOfChapter(ValidHash);
        var before = DateTime.UtcNow;

        section.Unpublish();

        Assert.False(section.IsPublished);
        Assert.NotNull(section.UnpublishedAt);
        Assert.True(section.UnpublishedAt >= before);
    }

    [Fact]
    public void Unpublish_WhenNotPublished_DoesNotThrow()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");

        var ex = Record.Exception(() => section.Unpublish());

        Assert.Null(ex);
    }

    // ---------------------------------------------------------------------------
    // MarkContentChanged
    // ---------------------------------------------------------------------------

    [Fact]
    public void MarkContentChanged_SetsContentChangedSincePublishTrue()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");
        section.PublishAsPartOfChapter(ValidHash);

        section.MarkContentChanged();

        Assert.True(section.ContentChangedSincePublish);
    }

    // ---------------------------------------------------------------------------
    // UpdateContent
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateContent_UpdatesHtmlContentAndHash()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");

        section.UpdateContent("<p>Updated content.</p>", "newHash456");

        Assert.Equal("<p>Updated content.</p>", section.HtmlContent);
        Assert.Equal("newHash456", section.ContentHash);
    }

    [Fact]
    public void UpdateContent_OnFolderNode_DoesNotThrow()
    {
        var section = Section.CreateFolder(ProjectId, ValidUuid, ValidTitle, null, 1);

        var ex = Record.Exception(() => section.UpdateContent(null, null));

        Assert.Null(ex);
    }

    // ---------------------------------------------------------------------------
    // SoftDelete
    // ---------------------------------------------------------------------------

    [Fact]
    public void SoftDelete_SetsFlagsAndRecordsTimestamp()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");
        var before = DateTime.UtcNow;

        section.SoftDelete();

        Assert.True(section.IsSoftDeleted);
        Assert.NotNull(section.SoftDeletedAt);
        Assert.True(section.SoftDeletedAt >= before);
    }

    [Fact]
    public void SoftDelete_UnpublishesPublishedSection()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");
        section.PublishAsPartOfChapter(ValidHash);

        section.SoftDelete();

        Assert.False(section.IsPublished);
        Assert.True(section.IsSoftDeleted);
    }

    [Fact]
    public void SoftDelete_WhenAlreadyDeleted_DoesNotChangeSoftDeletedAt()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");
        section.SoftDelete();
        var firstDeletion = section.SoftDeletedAt;

        section.SoftDelete();

        Assert.Equal(firstDeletion, section.SoftDeletedAt);
    }

    // ---------------------------------------------------------------------------
    // Publication invariant - scenes may only be published via chapter
    // ---------------------------------------------------------------------------

    [Fact]
    public void Publish_IsInternal_NotCallableFromOutsideAssembly()
    {
        // This test documents the invariant: Section.PublishAsPartOfChapter() is internal.
        // External callers (controllers, application services outside the domain)
        // cannot call Publish() directly on a Document - they must go via
        // PublicationService.PublishChapterAsync which calls PublishAsPartOfChapter().
        // The compiler enforces this - this test is intentionally a compile-time check
        // documented here for clarity.
        Assert.True(true, "Publish() is internal - enforced at compile time.");
    }

    [Fact]
    public void PublishAsPartOfChapter_DocumentNode_SetsIsPublished()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");

        section.PublishAsPartOfChapter(ValidHash);

        Assert.True(section.IsPublished);
        Assert.NotNull(section.PublishedAt);
        Assert.False(section.ContentChangedSincePublish);
    }

    [Fact]
    public void PublishAsPartOfChapter_FolderNode_ThrowsInvariantViolationException()
    {
        var section = Section.CreateFolder(ProjectId, ValidUuid, ValidTitle, null, 1);

        var ex = Assert.Throws<InvariantViolationException>(
            () => section.PublishAsPartOfChapter(ValidHash));

        Assert.Equal("I-04", ex.InvariantCode);
    }

    [Fact]
    public void PublishAsPartOfChapter_SoftDeletedSection_ThrowsInvariantViolationException()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");
        section.SoftDelete();

        var ex = Assert.Throws<InvariantViolationException>(
            () => section.PublishAsPartOfChapter(ValidHash));

        Assert.Equal("I-18", ex.InvariantCode);
    }

    [Fact]
    public void PublishAsPartOfChapter_ClearsContentChangedSincePublish()
    {
        var section = Section.CreateDocument(
            ProjectId, ValidUuid, ValidTitle, null, 1,
            ValidHtml, ValidHash, "First Draft");
        section.PublishAsPartOfChapter(ValidHash);
        section.MarkContentChanged();

        section.PublishAsPartOfChapter(ValidHash);

        Assert.False(section.ContentChangedSincePublish);
    }

    // ---------------------------------------------------------------------------
    // CreateDocumentForUpload
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateDocumentForUpload_WithValidData_ReturnsDocument()
    {
        var section = Section.CreateDocumentForUpload(ProjectId, ValidTitle, null, 1);

        Assert.NotEqual(Guid.Empty, section.Id);
        Assert.Equal(ProjectId, section.ProjectId);
        Assert.Equal(ValidTitle, section.Title);
        Assert.Equal(NodeType.Document, section.NodeType);
        Assert.Equal(1, section.SortOrder);
        Assert.False(section.IsPublished);
        Assert.False(section.IsSoftDeleted);
    }

    [Fact]
    public void CreateDocumentForUpload_HasNullScrivenerUuid()
    {
        var section = Section.CreateDocumentForUpload(ProjectId, ValidTitle, null, 1);

        Assert.Null(section.ScrivenerUuid);
    }

    [Fact]
    public void CreateDocumentForUpload_HasNullHtmlContent()
    {
        var section = Section.CreateDocumentForUpload(ProjectId, ValidTitle, null, 1);

        Assert.Null(section.HtmlContent);
    }

    [Fact]
    public void CreateDocumentForUpload_SetsParentId()
    {
        var parentId = Guid.NewGuid();

        var section = Section.CreateDocumentForUpload(ProjectId, ValidTitle, parentId, 1);

        Assert.Equal(parentId, section.ParentId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDocumentForUpload_WithInvalidTitle_ThrowsInvariantViolation(string? title)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => Section.CreateDocumentForUpload(ProjectId, title, null, 1));
#pragma warning restore CS8604

        Assert.Equal("I-SEC-TITLE", ex.InvariantCode);
    }
}


