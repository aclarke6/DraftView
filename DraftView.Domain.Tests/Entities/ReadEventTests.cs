using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class ReadEventTests
{
    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid UserId    = Guid.NewGuid();

    // ---------------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidData_ReturnsReadEvent()
    {
        var before = DateTime.UtcNow;

        var readEvent = ReadEvent.Create(SectionId, UserId);

        Assert.NotEqual(Guid.Empty, readEvent.Id);
        Assert.Equal(SectionId, readEvent.SectionId);
        Assert.Equal(UserId, readEvent.UserId);
        Assert.Equal(1, readEvent.OpenCount);
        Assert.Null(readEvent.ResumeAnchorId);
        Assert.True(readEvent.FirstOpenedAt >= before);
        Assert.Equal(readEvent.FirstOpenedAt, readEvent.LastOpenedAt);
    }

    [Fact]
    public void Create_OpenCountIsOne()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        Assert.Equal(1, readEvent.OpenCount);
    }

    // ---------------------------------------------------------------------------
    // Resume anchor
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateResumeAnchor_WithValidAnchorId_SetsResumeAnchorId()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);
        var anchorId = Guid.NewGuid();

        readEvent.UpdateResumeAnchor(anchorId);

        Assert.Equal(anchorId, readEvent.ResumeAnchorId);
    }

    [Fact]
    public void UpdateResumeAnchor_WithEmptyAnchorId_ThrowsInvariantViolationException()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        var ex = Assert.Throws<InvariantViolationException>(() =>
            readEvent.UpdateResumeAnchor(Guid.Empty));

        Assert.Equal("I-READ-ANCHOR", ex.InvariantCode);
    }

    [Fact]
    public void ClearResumeAnchor_WithExistingAnchor_ClearsResumeAnchorId()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);
        readEvent.UpdateResumeAnchor(Guid.NewGuid());

        readEvent.ClearResumeAnchor();

        Assert.Null(readEvent.ResumeAnchorId);
    }

    // ---------------------------------------------------------------------------
    // RecordOpen
    // ---------------------------------------------------------------------------

    [Fact]
    public void RecordOpen_IncrementsOpenCount()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        readEvent.RecordOpen();

        Assert.Equal(2, readEvent.OpenCount);
    }

    [Fact]
    public void RecordOpen_UpdatesLastOpenedAt()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);
        var firstOpen = readEvent.LastOpenedAt;

        // Small delay to ensure timestamp differs
        System.Threading.Thread.Sleep(10);
        readEvent.RecordOpen();

        Assert.True(readEvent.LastOpenedAt > firstOpen);
    }

    [Fact]
    public void RecordOpen_NeverChangesFirstOpenedAt()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);
        var firstOpenedAt = readEvent.FirstOpenedAt;

        readEvent.RecordOpen();
        readEvent.RecordOpen();
        readEvent.RecordOpen();

        // I-12: FirstOpenedAt is immutable after creation
        Assert.Equal(firstOpenedAt, readEvent.FirstOpenedAt);
    }

    [Fact]
    public void RecordOpen_MultipleOpens_OpenCountAccumulates()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        readEvent.RecordOpen();
        readEvent.RecordOpen();
        readEvent.RecordOpen();

        // I-13: OpenCount always >= 1; started at 1, 3 more = 4
        Assert.Equal(4, readEvent.OpenCount);
    }

    [Fact]
    public void OpenCount_IsAlwaysAtLeastOne()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        // I-13
        Assert.True(readEvent.OpenCount >= 1);
    }

    // ---------------------------------------------------------------------------
    // UpdateLastReadVersion
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateLastReadVersion_SetsVersionNumber()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        readEvent.UpdateLastReadVersion(5);

        Assert.Equal(5, readEvent.LastReadVersionNumber);
    }

    [Fact]
    public void UpdateLastReadVersion_OverwritesPreviousValue()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        readEvent.UpdateLastReadVersion(3);
        readEvent.UpdateLastReadVersion(7);

        Assert.Equal(7, readEvent.LastReadVersionNumber);
    }

    [Fact]
    public void UpdateLastReadVersion_WithVersionNumberZero_ThrowsInvariantViolation()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        var ex = Assert.Throws<InvariantViolationException>(
            () => readEvent.UpdateLastReadVersion(0));

        Assert.Equal("I-READ-VER", ex.InvariantCode);
    }

    [Fact]
    public void UpdateLastReadVersion_WithNegativeVersionNumber_ThrowsInvariantViolation()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        var ex = Assert.Throws<InvariantViolationException>(
            () => readEvent.UpdateLastReadVersion(-1));

        Assert.Equal("I-READ-VER", ex.InvariantCode);
    }

    [Fact]
    public void Create_LastReadVersionNumberIsNull()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        Assert.Null(readEvent.LastReadVersionNumber);
    }

    // ---------------------------------------------------------------------------
    // DismissBannerAtVersion
    // ---------------------------------------------------------------------------

    [Fact]
    public void DismissBannerAtVersion_SetsBannerDismissedAtVersion()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        readEvent.DismissBannerAtVersion(3);

        Assert.Equal(3, readEvent.BannerDismissedAtVersion);
    }

    [Fact]
    public void DismissBannerAtVersion_OverwritesPreviousValue()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        readEvent.DismissBannerAtVersion(2);
        readEvent.DismissBannerAtVersion(7);

        Assert.Equal(7, readEvent.BannerDismissedAtVersion);
    }

    [Fact]
    public void DismissBannerAtVersion_WithVersionLessThanOne_ThrowsInvariantViolation()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        var ex = Assert.Throws<InvariantViolationException>(() =>
            readEvent.DismissBannerAtVersion(0));

        Assert.Equal("I-READ-BANNER", ex.InvariantCode);
    }

    [Fact]
    public void Create_HasNullBannerDismissedAtVersion()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        Assert.Null(readEvent.BannerDismissedAtVersion);
    }
}
