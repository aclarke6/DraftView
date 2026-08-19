using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

/// <summary>
/// Tests for ManualChapter.Create and ManualChapterVersion.Create factory invariants.
/// Covers: valid construction, title validation, sort-order validation, content validation,
/// version number validation, and immutable-version construction.
/// Excludes: persistence, repository behaviour, application service orchestration.
/// </summary>
public class ManualChapterTests
{
    private static readonly Guid ValidAuthorId  = Guid.NewGuid();
    private static readonly Guid ValidProjectId = Guid.NewGuid();
    private static readonly DateTime ValidUploadedAt =
        new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

    // ── ManualChapter.Create ──────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_CreatesManualChapter()
    {
        var chapter = ManualChapter.Create(
            ValidAuthorId,
            ValidProjectId,
            "Chapter One",
            0,
            "Once upon a time…",
            "chapter-one.txt",
            ValidUploadedAt);

        Assert.NotEqual(Guid.Empty, chapter.Id);
        Assert.Equal(ValidAuthorId,  chapter.AuthorId);
        Assert.Equal(ValidProjectId, chapter.ProjectId);
        Assert.Equal("Chapter One",  chapter.Title);
        Assert.Equal(0,              chapter.SortOrder);
        Assert.Equal("Once upon a time…", chapter.RawContent);
        Assert.Equal("chapter-one.txt",   chapter.OriginalFileName);
        Assert.Equal(ValidUploadedAt,     chapter.UploadedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyTitle_ThrowsInvariantViolation(string? title)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(() =>
            ManualChapter.Create(
                ValidAuthorId, ValidProjectId,
                title, 0, "content", null, ValidUploadedAt));
#pragma warning restore CS8604

        Assert.Equal("I-MC-01", ex.InvariantCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithNegativeSortOrder_ThrowsInvariantViolation(int sortOrder)
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            ManualChapter.Create(
                ValidAuthorId, ValidProjectId,
                "Title", sortOrder, "content", null, ValidUploadedAt));

        Assert.Equal("I-MC-02", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithNullContent_ThrowsInvariantViolation()
    {
#pragma warning disable CS8625
        var ex = Assert.Throws<InvariantViolationException>(() =>
            ManualChapter.Create(
                ValidAuthorId, ValidProjectId,
                "Title", 0, null, null, ValidUploadedAt));
#pragma warning restore CS8625

        Assert.Equal("I-MC-03", ex.InvariantCode);
    }

    [Fact]
    public void Create_AllowsNullOriginalFileName()
    {
        var chapter = ManualChapter.Create(
            ValidAuthorId, ValidProjectId,
            "Pasted Chapter", 1, "pasted content", null, ValidUploadedAt);

        Assert.Null(chapter.OriginalFileName);
    }

    [Fact]
    public void Create_WithAuthorIdEmpty_ThrowsInvariantViolation()
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            ManualChapter.Create(
                Guid.Empty, ValidProjectId,
                "Title", 0, "content", null, ValidUploadedAt));

        Assert.Equal("I-MC-04", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithProjectIdEmpty_ThrowsInvariantViolation()
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            ManualChapter.Create(
                ValidAuthorId, Guid.Empty,
                "Title", 0, "content", null, ValidUploadedAt));

        Assert.Equal("I-MC-05", ex.InvariantCode);
    }

    // ── ManualChapterVersion.Create ───────────────────────────────────────────

    [Fact]
    public void CreateVersion_WithValidData_CreatesManualChapterVersion()
    {
        var chapterId = Guid.NewGuid();
        var snapshotAt = new DateTime(2026, 8, 18, 11, 0, 0, DateTimeKind.Utc);

        var version = ManualChapterVersion.Create(
            chapterId,
            ValidAuthorId,
            1,
            "original content",
            SnapshotReason.FileUpload,
            snapshotAt);

        Assert.NotEqual(Guid.Empty, version.Id);
        Assert.Equal(chapterId,              version.ManualChapterId);
        Assert.Equal(ValidAuthorId,          version.AuthorId);
        Assert.Equal(1,                      version.VersionNumber);
        Assert.Equal("original content",     version.RawContent);
        Assert.Equal(SnapshotReason.FileUpload, version.SnapshotReason);
        Assert.Equal(snapshotAt,             version.SnapshotAt);
    }

    [Fact]
    public void CreateVersion_WithNullContent_ThrowsInvariantViolation()
    {
#pragma warning disable CS8625
        var ex = Assert.Throws<InvariantViolationException>(() =>
            ManualChapterVersion.Create(
                Guid.NewGuid(), ValidAuthorId,
                1, null, SnapshotReason.FileUpload,
                DateTime.UtcNow));
#pragma warning restore CS8625

        Assert.Equal("I-MCV-01", ex.InvariantCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateVersion_WithInvalidVersionNumber_ThrowsInvariantViolation(int versionNumber)
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            ManualChapterVersion.Create(
                Guid.NewGuid(), ValidAuthorId,
                versionNumber, "content", SnapshotReason.InlineEdit,
                DateTime.UtcNow));

        Assert.Equal("I-MCV-02", ex.InvariantCode);
    }
}
