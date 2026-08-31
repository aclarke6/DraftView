using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class ReaderSnapshotTests
{
    private static readonly Guid SectionId   = Guid.NewGuid();
    private static readonly Guid UserId      = Guid.NewGuid();
    private const string         HtmlContent = "<p>Some prose.</p>";

    // ---------------------------------------------------------------------------
    // Create — happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidData_ReturnsReaderSnapshot()
    {
        var before = DateTime.UtcNow;

        var snapshot = ReaderSnapshot.Create(SectionId, UserId, HtmlContent);

        Assert.NotEqual(Guid.Empty, snapshot.Id);
        Assert.Equal(SectionId,   snapshot.SectionId);
        Assert.Equal(UserId,      snapshot.UserId);
        Assert.Equal(HtmlContent, snapshot.HtmlContent);
        Assert.True(snapshot.SnapshotAt >= before);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var a = ReaderSnapshot.Create(SectionId, UserId, HtmlContent);
        var b = ReaderSnapshot.Create(SectionId, UserId, HtmlContent);

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Create_SetsSnapshotAtToUtcNow()
    {
        var before = DateTime.UtcNow;

        var snapshot = ReaderSnapshot.Create(SectionId, UserId, HtmlContent);

        Assert.True(snapshot.SnapshotAt >= before);
        Assert.Equal(DateTimeKind.Utc, snapshot.SnapshotAt.Kind);
    }

    // ---------------------------------------------------------------------------
    // Create — guard invariants
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithEmptySectionId_Throws()
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            ReaderSnapshot.Create(Guid.Empty, UserId, HtmlContent));

        Assert.Equal("I-SNAP-SECTION", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithEmptyUserId_Throws()
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            ReaderSnapshot.Create(SectionId, Guid.Empty, HtmlContent));

        Assert.Equal("I-SNAP-USER", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithNullHtmlContent_Throws()
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            ReaderSnapshot.Create(SectionId, UserId, null!));

        Assert.Equal("I-SNAP-HTML", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithEmptyHtmlContent_Throws()
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            ReaderSnapshot.Create(SectionId, UserId, string.Empty));

        Assert.Equal("I-SNAP-HTML", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithWhitespaceHtmlContent_Throws()
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            ReaderSnapshot.Create(SectionId, UserId, "   "));

        Assert.Equal("I-SNAP-HTML", ex.InvariantCode);
    }
}
