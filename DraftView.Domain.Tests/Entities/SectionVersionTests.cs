using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

/// <summary>
/// Tests for SectionVersion entity covering factory method Create,
/// property initialization, invariant validations, and semantic versioning.
/// Covers change classification, semantic version label, and ScrivenerStatus capture.
/// Excludes: reader content resolution and UI integration.
/// </summary>
public class SectionVersionTests
{
    private static readonly Guid ValidProjectId = Guid.NewGuid();
    private static readonly Guid ValidAuthorId = Guid.NewGuid();
    private const string TestHtmlContent = "<p>Test content</p>";
    private const string TestContentHash = "abc123";

    private static Section CreateValidDocumentSection(string? scrivenerStatus = null)
    {
        return Section.CreateDocument(
            ValidProjectId, "test-uuid", "Test Document",
            null, 1, TestHtmlContent, TestContentHash, scrivenerStatus);
    }

    // ---------------------------------------------------------------------------
    // Create — Success cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithDocumentSection_ReturnsVersion()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1, 1, 0);
        Assert.NotEqual(Guid.Empty, version.Id);
        Assert.Equal(section.Id, version.SectionId);
    }

    [Fact]
    public void Create_WithDocumentSection_SnapshotsHtmlContent()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1, 1, 0);
        Assert.Equal(TestHtmlContent, version.HtmlContent);
    }

    [Fact]
    public void Create_WithDocumentSection_SnapshotsContentHash()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1, 1, 0);
        Assert.Equal(TestContentHash, version.ContentHash);
    }

    [Fact]
    public void Create_WithDocumentSection_SetsVersionNumber()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 42, 1, 0);
        Assert.Equal(42, version.VersionNumber);
    }

    [Fact]
    public void Create_WithDocumentSection_SetsAuthorId()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1, 1, 0);
        Assert.Equal(ValidAuthorId, version.AuthorId);
    }

    [Fact]
    public void Create_WithDocumentSection_SetsCreatedAt()
    {
        var section = CreateValidDocumentSection();
        var before = DateTime.UtcNow;
        var version = SectionVersion.Create(section, ValidAuthorId, 1, 1, 0);
        Assert.True(version.CreatedAt >= before);
        Assert.True(version.CreatedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Create_WithDocumentSection_ChangeClassificationIsNull()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1, 1, 0);
        Assert.Null(version.ChangeClassification);
    }

    [Fact]
    public void Create_HasNullChangeClassification()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1, 1, 0);
        Assert.Null(version.ChangeClassification);
    }

    // ---------------------------------------------------------------------------
    // Create — Invariant violations
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithFolderSection_ThrowsInvariantViolation()
    {
        var section = Section.CreateFolder(ValidProjectId, "folder-uuid", "Test Folder", null, 1);
        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, 1, 1, 0));
        Assert.Equal("I-VER-FOLDER", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithSoftDeletedSection_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();
        section.SoftDelete();
        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, 1, 1, 0));
        Assert.Equal("I-VER-DELETED", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithNullHtmlContent_ThrowsInvariantViolation()
    {
        var section = Section.CreateDocument(
            ValidProjectId, "test-uuid", "Test Document", null, 1, null, TestContentHash, null);
        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, 1, 1, 0));
        Assert.Equal("I-VER-CONTENT", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithEmptyHtmlContent_ThrowsInvariantViolation()
    {
        var section = Section.CreateDocument(
            ValidProjectId, "test-uuid", "Test Document", null, 1, "", TestContentHash, null);
        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, 1, 1, 0));
        Assert.Equal("I-VER-CONTENT", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithEmptyGuidAuthorId_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();
        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, Guid.Empty, 1, 1, 0));
        Assert.Equal("I-VER-AUTHOR", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithVersionNumberZero_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();
        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, 0, 1, 0));
        Assert.Equal("I-VER-NUMBER", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithVersionNumberNegative_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();
        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, -1, 1, 0));
        Assert.Equal("I-VER-NUMBER", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // SetChangeClassification
    // ---------------------------------------------------------------------------

    [Fact]
    public void SetChangeClassification_SetsClassification()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1, 1, 0);
        version.SetChangeClassification(ChangeClassification.Revision);
        Assert.Equal(ChangeClassification.Revision, version.ChangeClassification);
    }

    [Fact]
    public void SetChangeClassification_WhenAlreadySet_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1, 1, 0);
        version.SetChangeClassification(ChangeClassification.Polish);
        var ex = Assert.Throws<InvariantViolationException>(
            () => version.SetChangeClassification(ChangeClassification.Rewrite));
        Assert.Equal("I-VER-CLASS", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // Semantic versioning — MajorVersion, MinorVersion, ScrivenerStatus
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_FirstPublish_SetsMajor1Minor0()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1, 1, 0);
        Assert.Equal(1, version.MajorVersion);
        Assert.Equal(0, version.MinorVersion);
    }

    [Fact]
    public void Create_CapturesScrivenerStatusFromSection()
    {
        var section = CreateValidDocumentSection("Revised Draft");
        var version = SectionVersion.Create(section, ValidAuthorId, 1, 1, 0);
        Assert.Equal("Revised Draft", version.ScrivenerStatus);
    }

    [Fact]
    public void Create_NullScrivenerStatus_StoredAsNull()
    {
        var section = CreateValidDocumentSection(); // ScrivenerStatus = null
        var version = SectionVersion.Create(section, ValidAuthorId, 1, 1, 0);
        Assert.Null(version.ScrivenerStatus);
    }

    [Fact]
    public void VersionLabel_WithClassification_FormatsCorrectly()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 4, 1, 3);
        version.SetChangeClassification(ChangeClassification.Polish);
        Assert.Equal("Version 1.03 — Polish", version.VersionLabel);
    }

    [Fact]
    public void VersionLabel_WithRewrite_FormatsCorrectly()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 3, 2, 0);
        version.SetChangeClassification(ChangeClassification.Rewrite);
        Assert.Equal("Version 2.00 — Rewrite", version.VersionLabel);
    }

    [Fact]
    public void VersionLabel_WithoutClassification_OmitsLabel()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1, 1, 0);
        Assert.Equal("Version 1.00", version.VersionLabel);
    }

    [Fact]
    public void Create_WithMajorVersionZero_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();
        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, 1, 0, 0));
        Assert.Equal("I-VER-MAJOR", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithNegativeMinorVersion_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();
        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, 1, 1, -1));
        Assert.Equal("I-VER-MINOR", ex.InvariantCode);
    }
}
