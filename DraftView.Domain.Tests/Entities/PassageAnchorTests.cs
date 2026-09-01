using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.ValueObjects;

namespace DraftView.Domain.Tests.Entities;

/// <summary>
/// Tests for PassageAnchor, PassageAnchorSnapshot, and PassageAnchorMatch.
/// Covers anchor creation, immutable original snapshot data, confidence validation,
/// orphaning, human rejection audit metadata, manual relink, and human-authority precedence.
/// Excludes persistence and application authorization.
/// </summary>
public class PassageAnchorTests
{
    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid UserId    = Guid.NewGuid();

    [Fact]
    public void Create_WithValidSnapshot_ReturnsAnchor()
    {
        var snapshot = CreateSnapshot();

        var anchor = PassageAnchor.Create(
            SectionId,
            PassageAnchorPurpose.Comment,
            UserId,
            snapshot);

        Assert.NotEqual(Guid.Empty, anchor.Id);
        Assert.Equal(SectionId, anchor.SectionId);
        Assert.Equal(PassageAnchorPurpose.Comment, anchor.Purpose);
        Assert.Equal(UserId, anchor.CreatedByUserId);
        Assert.Same(snapshot, anchor.OriginalSnapshot);
        Assert.Equal(PassageAnchorStatus.Original, anchor.Status);
        Assert.Null(anchor.CurrentMatch);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SnapshotCreate_WithEmptySelectedText_ThrowsInvariantViolationException(string? selectedText)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(() =>
            PassageAnchorSnapshot.Create(
                selectedText,
                "selected text",
                "hash",
                "prefix",
                "suffix",
                10,
                23,
                "content-hash"));
#pragma warning restore CS8604

        Assert.Equal("I-ANCHOR-TEXT", ex.InvariantCode);
    }

    [Fact]
    public void SnapshotCreate_WithEndOffsetBeforeStartOffset_ThrowsInvariantViolationException()
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            PassageAnchorSnapshot.Create(
                "Selected text",
                "selected text",
                "hash",
                "prefix",
                "suffix",
                23,
                10,
                "content-hash"));

        Assert.Equal("I-ANCHOR-OFFSET", ex.InvariantCode);
    }

    [Fact]
    public void SnapshotCreate_WithValidData_SetsProperties()
    {
        var snapshot = CreateSnapshot();

        Assert.Equal("Selected text", snapshot.SelectedText);
        Assert.Equal("selected text", snapshot.NormalizedSelectedText);
        Assert.Equal(10, snapshot.StartOffset);
        Assert.Equal(23, snapshot.EndOffset);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void MatchCreate_WithConfidenceOutsideRange_ThrowsInvariantViolationException(int confidence)
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            PassageAnchorMatch.Create(
                10,
                23,
                "selected text",
                confidence,
                PassageAnchorMatchMethod.Exact));

        Assert.Equal("I-ANCHOR-CONFIDENCE", ex.InvariantCode);
    }

    [Fact]
    public void MarkOrphaned_ClearsActiveCurrentMatch()
    {
        var anchor = CreateAnchor();
        anchor.UpdateCurrentMatch(CreateMatch(PassageAnchorMatchMethod.Exact));

        anchor.MarkOrphaned();

        Assert.Equal(PassageAnchorStatus.Orphaned, anchor.Status);
        Assert.Null(anchor.CurrentMatch);
    }

    [Fact]
    public void Reject_WithEmptyActorId_ThrowsInvariantViolationException()
    {
        var anchor = CreateAnchor();
        var match = CreateMatch(PassageAnchorMatchMethod.Exact);

        var ex = Assert.Throws<InvariantViolationException>(() =>
            anchor.Reject(match, Guid.Empty, "wrong location"));

        Assert.Equal("I-ANCHOR-ACTOR", ex.InvariantCode);
    }

    [Fact]
    public void Reject_WithValidMatch_SetsRejectedStatusAndAuditMetadata()
    {
        var anchor = CreateAnchor();
        var match = CreateMatch(PassageAnchorMatchMethod.Exact);

        anchor.Reject(match, UserId, "wrong location");

        Assert.Equal(PassageAnchorStatus.UserRejected, anchor.Status);
        Assert.Null(anchor.CurrentMatch);
        Assert.NotNull(anchor.Rejection);
        Assert.Equal(UserId, anchor.Rejection!.RejectedByUserId);
        Assert.Equal("wrong location", anchor.Rejection.Reason);
    }

    [Fact]
    public void Relink_WithEmptyActorId_ThrowsInvariantViolationException()
    {
        var anchor = CreateAnchor();
        var match = CreateMatch(PassageAnchorMatchMethod.ManualRelink, UserId);

        var ex = Assert.Throws<InvariantViolationException>(() =>
            anchor.Relink(match, Guid.Empty));

        Assert.Equal("I-ANCHOR-ACTOR", ex.InvariantCode);
    }

    [Fact]
    public void Relink_WithNonManualMatch_ThrowsInvariantViolationException()
    {
        var anchor = CreateAnchor();
        var match = CreateMatch(PassageAnchorMatchMethod.Exact);

        var ex = Assert.Throws<InvariantViolationException>(() =>
            anchor.Relink(match, UserId));

        Assert.Equal("I-ANCHOR-MANUAL", ex.InvariantCode);
    }

    [Fact]
    public void UpdateCurrentMatch_AutomatedMatchCannotOverwriteManualRelink()
    {
        var anchor = CreateAnchor();
        anchor.Relink(CreateMatch(PassageAnchorMatchMethod.ManualRelink, UserId), UserId);

        var ex = Assert.Throws<InvariantViolationException>(() =>
            anchor.UpdateCurrentMatch(CreateMatch(PassageAnchorMatchMethod.Exact)));

        Assert.Equal("I-ANCHOR-MANUAL", ex.InvariantCode);
    }

    [Fact]
    public void UpdateCurrentMatch_AutomatedMatchCannotOverwriteRejectedMatch()
    {
        var anchor = CreateAnchor();
        var rejectedMatch = CreateMatch(PassageAnchorMatchMethod.Exact);
        anchor.Reject(rejectedMatch, UserId, "wrong location");

        var ex = Assert.Throws<InvariantViolationException>(() =>
            anchor.UpdateCurrentMatch(CreateMatch(PassageAnchorMatchMethod.Context)));

        Assert.Equal("I-ANCHOR-REJECTED", ex.InvariantCode);
    }

    private static PassageAnchor CreateAnchor() =>
        PassageAnchor.Create(SectionId, PassageAnchorPurpose.Comment, UserId, CreateSnapshot());

    private static PassageAnchorSnapshot CreateSnapshot() =>
        PassageAnchorSnapshot.Create(
            "Selected text", "selected text", "hash",
            "prefix", "suffix", 10, 23, "content-hash");

    private static PassageAnchorMatch CreateMatch(
        PassageAnchorMatchMethod method,
        Guid? resolvedByUserId = null) =>
        PassageAnchorMatch.Create(11, 24, "selected text", 95, method, resolvedByUserId);
}
