using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

/// <summary>
/// Tests for SectionVersion entity covering factory method Create,
/// property initialization, and invariant validations.
/// Covers change classification and AI summary one-time assignment invariants.
/// </summary>
public class SectionVersionTests
{
    private static readonly Guid ValidProjectId = Guid.NewGuid();
    private static readonly Guid ValidAuthorId = Guid.NewGuid();
    private const string TestHtmlContent = "<p>Test content</p>";
    private const string TestContentHash = "abc123";

    private static Section CreateValidDocumentSection()
    {
        return Section.CreateDocument(
            ValidProjectId,
            "test-uuid",
            "Test Document",
            null,
            1,
            TestHtmlContent,
            TestContentHash,
            null);
    }

    // ---------------------------------------------------------------------------
    // Create - Success cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithDocumentSection_ReturnsVersion()
    {
        var section = CreateValidDocumentSection();

        var version = SectionVersion.Create(section, ValidAuthorId, 1);

        Assert.NotEqual(Guid.Empty, version.Id);
        Assert.Equal(section.Id, version.SectionId);
    }

    [Fact]
    public void Create_WithDocumentSection_SnapshotsHtmlContent()
    {
        var section = CreateValidDocumentSection();

        var version = SectionVersion.Create(section, ValidAuthorId, 1);

        Assert.Equal(TestHtmlContent, version.HtmlContent);
    }

    [Fact]
    public void Create_WithDocumentSection_SnapshotsContentHash()
    {
        var section = CreateValidDocumentSection();

        var version = SectionVersion.Create(section, ValidAuthorId, 1);

        Assert.Equal(TestContentHash, version.ContentHash);
    }

    [Fact]
    public void Create_WithDocumentSection_SetsVersionNumber()
    {
        var section = CreateValidDocumentSection();

        var version = SectionVersion.Create(section, ValidAuthorId, 42);

        Assert.Equal(42, version.VersionNumber);
    }

    [Fact]
    public void Create_WithDocumentSection_SetsAuthorId()
    {
        var section = CreateValidDocumentSection();

        var version = SectionVersion.Create(section, ValidAuthorId, 1);

        Assert.Equal(ValidAuthorId, version.AuthorId);
    }

    [Fact]
    public void Create_WithDocumentSection_SetsCreatedAt()
    {
        var section = CreateValidDocumentSection();
        var before = DateTime.UtcNow;

        var version = SectionVersion.Create(section, ValidAuthorId, 1);

        Assert.True(version.CreatedAt >= before);
        Assert.True(version.CreatedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Create_WithDocumentSection_ChangeClassificationIsNull()
    {
        var section = CreateValidDocumentSection();

        var version = SectionVersion.Create(section, ValidAuthorId, 1);

        Assert.Null(version.ChangeClassification);
    }

    [Fact]
    public void Create_HasNullChangeClassification()
    {
        var section = CreateValidDocumentSection();

        var version = SectionVersion.Create(section, ValidAuthorId, 1);

        Assert.Null(version.ChangeClassification);
    }

    [Fact]
    public void Create_WithDocumentSection_AiSummaryIsNull()
    {
        var section = CreateValidDocumentSection();

        var version = SectionVersion.Create(section, ValidAuthorId, 1);

        Assert.Null(version.AiSummary);
    }

    // ---------------------------------------------------------------------------
    // Create - Invariant violations
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithFolderSection_ThrowsInvariantViolation()
    {
        var section = Section.CreateFolder(ValidProjectId, "folder-uuid", "Test Folder", null, 1);

        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, 1));

        Assert.Equal("I-VER-FOLDER", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithSoftDeletedSection_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();
        section.SoftDelete();

        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, 1));

        Assert.Equal("I-VER-DELETED", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithNullHtmlContent_ThrowsInvariantViolation()
    {
        var section = Section.CreateDocument(
            ValidProjectId,
            "test-uuid",
            "Test Document",
            null,
            1,
            null,
            TestContentHash,
            null);

        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, 1));

        Assert.Equal("I-VER-CONTENT", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithEmptyHtmlContent_ThrowsInvariantViolation()
    {
        var section = Section.CreateDocument(
            ValidProjectId,
            "test-uuid",
            "Test Document",
            null,
            1,
            "",
            TestContentHash,
            null);

        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, 1));

        Assert.Equal("I-VER-CONTENT", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithEmptyGuidAuthorId_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();

        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, Guid.Empty, 1));

        Assert.Equal("I-VER-AUTHOR", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithVersionNumberZero_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();

        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, 0));

        Assert.Equal("I-VER-NUMBER", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithVersionNumberNegative_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();

        var ex = Assert.Throws<InvariantViolationException>(
            () => SectionVersion.Create(section, ValidAuthorId, -1));

        Assert.Equal("I-VER-NUMBER", ex.InvariantCode);
    }

    [Fact]
    public void SetChangeClassification_SetsClassification()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1);

        version.SetChangeClassification(ChangeClassification.Revision);

        Assert.Equal(ChangeClassification.Revision, version.ChangeClassification);
    }

    [Fact]
    public void SetChangeClassification_WhenAlreadySet_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1);
        version.SetChangeClassification(ChangeClassification.Polish);

        var ex = Assert.Throws<InvariantViolationException>(() =>
            version.SetChangeClassification(ChangeClassification.Rewrite));

        Assert.Equal("I-VER-CLASS", ex.InvariantCode);
    }

    [Fact]
    public void SetAiSummary_SetsSummary()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1);

        version.SetAiSummary("Aria and Tomas finally confront the truth in Blackmere Chapel.");

        Assert.Equal("Aria and Tomas finally confront the truth in Blackmere Chapel.", version.AiSummary);
    }

    [Fact]
    public void SetAiSummary_WhenAlreadySet_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1);
        version.SetAiSummary("Initial summary.");

        var ex = Assert.Throws<InvariantViolationException>(() =>
            version.SetAiSummary("Updated summary."));

        Assert.Equal("I-VER-AISUMMARY", ex.InvariantCode);
    }

    [Fact]
    public void SetAiSummary_WithEmptySummary_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1);

        var ex = Assert.Throws<InvariantViolationException>(() =>
            version.SetAiSummary(string.Empty));

        Assert.Equal("I-VER-AISUMMARY-EMPTY", ex.InvariantCode);
    }

    [Fact]
    public void SetAiSummary_WithWhitespaceSummary_ThrowsInvariantViolation()
    {
        var section = CreateValidDocumentSection();
        var version = SectionVersion.Create(section, ValidAuthorId, 1);

        var ex = Assert.Throws<InvariantViolationException>(() =>
            version.SetAiSummary("   "));

        Assert.Equal("I-VER-AISUMMARY-EMPTY", ex.InvariantCode);
    }

    [Fact]
    public void Create_HasNullAiSummary()
    {
        var section = CreateValidDocumentSection();

        var version = SectionVersion.Create(section, ValidAuthorId, 1);

        Assert.Null(version.AiSummary);
    }
}
