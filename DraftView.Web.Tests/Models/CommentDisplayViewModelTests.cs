using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Web.Models;

namespace DraftView.Web.Tests.Models;

public class CommentDisplayViewModelTests
{
    [Fact]
    public void IsPassageAnchorOrphaned_WhenNoAnchorId_ReturnsFalse()
    {
        var vm = new CommentDisplayViewModel
        {
            Comment = MakeComment(passageAnchorId: null),
            PassageAnchor = null
        };

        Assert.False(vm.IsPassageAnchorOrphaned);
    }

    [Fact]
    public void IsPassageAnchorOrphaned_WhenAnchorIdSetButDtoIsNull_ReturnsFalse()
    {
        var vm = new CommentDisplayViewModel
        {
            Comment = MakeComment(passageAnchorId: Guid.NewGuid()),
            PassageAnchor = null
        };

        Assert.False(vm.IsPassageAnchorOrphaned);
    }

    [Fact]
    public void IsPassageAnchorOrphaned_WhenAnchorStatusIsOrphaned_ReturnsTrue()
    {
        var vm = new CommentDisplayViewModel
        {
            Comment = MakeComment(passageAnchorId: Guid.NewGuid()),
            PassageAnchor = MakeAnchorDto(PassageAnchorStatus.Orphaned)
        };

        Assert.True(vm.IsPassageAnchorOrphaned);
    }

    [Theory]
    [InlineData(PassageAnchorStatus.Original)]
    [InlineData(PassageAnchorStatus.Exact)]
    [InlineData(PassageAnchorStatus.Context)]
    [InlineData(PassageAnchorStatus.Fuzzy)]
    [InlineData(PassageAnchorStatus.UserRelinked)]
    [InlineData(PassageAnchorStatus.UserRejected)]
    public void IsPassageAnchorOrphaned_WhenAnchorStatusIsNotOrphaned_ReturnsFalse(PassageAnchorStatus status)
    {
        var vm = new CommentDisplayViewModel
        {
            Comment = MakeComment(passageAnchorId: Guid.NewGuid()),
            PassageAnchor = MakeAnchorDto(status)
        };

        Assert.False(vm.IsPassageAnchorOrphaned);
    }

    [Fact]
    public void HasActivePassageAnchor_WhenNoAnchorId_ReturnsFalse()
    {
        var vm = new CommentDisplayViewModel
        {
            Comment = MakeComment(passageAnchorId: null),
            PassageAnchor = null
        };

        Assert.False(vm.HasActivePassageAnchor);
    }

    [Fact]
    public void HasActivePassageAnchor_WhenAnchorOrphaned_ReturnsFalse()
    {
        var vm = new CommentDisplayViewModel
        {
            Comment = MakeComment(passageAnchorId: Guid.NewGuid()),
            PassageAnchor = MakeAnchorDto(PassageAnchorStatus.Orphaned)
        };

        Assert.False(vm.HasActivePassageAnchor);
    }

    [Fact]
    public void HasActivePassageAnchor_WhenAnchorDtoMissing_ReturnsFalse()
    {
        var vm = new CommentDisplayViewModel
        {
            Comment = MakeComment(passageAnchorId: Guid.NewGuid()),
            PassageAnchor = null
        };

        Assert.False(vm.HasActivePassageAnchor);
    }

    [Theory]
    [InlineData(PassageAnchorStatus.Exact)]
    [InlineData(PassageAnchorStatus.Context)]
    [InlineData(PassageAnchorStatus.Fuzzy)]
    [InlineData(PassageAnchorStatus.UserRelinked)]
    public void HasActivePassageAnchor_WhenAnchorLocated_ReturnsTrue(PassageAnchorStatus status)
    {
        var vm = new CommentDisplayViewModel
        {
            Comment = MakeComment(passageAnchorId: Guid.NewGuid()),
            PassageAnchor = MakeAnchorDto(status)
        };

        Assert.True(vm.HasActivePassageAnchor);
    }

    private static Comment MakeComment(Guid? passageAnchorId) =>
        Comment.CreateRoot(
            sectionId: Guid.NewGuid(),
            authorId: Guid.NewGuid(),
            body: "test body",
            visibility: Visibility.Public,
            isReaderComment: true,
            sectionVersionId: null,
            passageAnchorId: passageAnchorId);

    private static PassageAnchorDto MakeAnchorDto(PassageAnchorStatus status) => new(
        Id: Guid.NewGuid(),
        SectionId: Guid.NewGuid(),
        OriginalSectionVersionId: null,
        Purpose: PassageAnchorPurpose.Comment,
        CreatedByUserId: Guid.NewGuid(),
        CreatedAt: DateTime.UtcNow,
        Status: status,
        UpdatedAt: null,
        OriginalSnapshot: new PassageAnchorSnapshotDto(
            SelectedText: "test passage",
            NormalizedSelectedText: "test passage",
            SelectedTextHash: "abc123",
            PrefixContext: "before ",
            SuffixContext: " after",
            StartOffset: 7,
            EndOffset: 19,
            CanonicalContentHash: "def456",
            HtmlSelectorHint: null),
        CurrentMatch: null);
}
