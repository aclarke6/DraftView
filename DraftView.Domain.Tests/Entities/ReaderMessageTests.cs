using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

/// <summary>
/// Tests for ReaderMessage.Create.
/// Covers: valid creation, blank subject invariant, blank body invariant.
/// Excludes: persistence and email dispatch (Application layer concerns).
/// </summary>
public class ReaderMessageTests
{
    private static readonly Guid AuthorId    = Guid.NewGuid();
    private static readonly Guid RecipientId = Guid.NewGuid();
    private static readonly DateTime SentAt  = DateTime.UtcNow;

    [Fact]
    public void Create_ValidArguments_ReturnsMessageWithAllFieldsSet()
    {
        var msg = ReaderMessage.Create(AuthorId, RecipientId, "Hello", "Great work!", SentAt);

        Assert.NotEqual(Guid.Empty, msg.Id);
        Assert.Equal(AuthorId,    msg.AuthorId);
        Assert.Equal(RecipientId, msg.RecipientId);
        Assert.Equal("Hello",     msg.Subject);
        Assert.Equal("Great work!", msg.Body);
        Assert.Equal(SentAt,      msg.SentAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankSubject_ThrowsInvariantViolation(string? subject)
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => ReaderMessage.Create(AuthorId, RecipientId, subject!, "Body text", SentAt));

        Assert.Equal("I-MSG-SUBJECT", ex.InvariantCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankBody_ThrowsInvariantViolation(string? body)
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => ReaderMessage.Create(AuthorId, RecipientId, "Subject", body!, SentAt));

        Assert.Equal("I-MSG-BODY", ex.InvariantCode);
    }

    [Fact]
    public void Create_TrimsSubjectAndBody()
    {
        var msg = ReaderMessage.Create(AuthorId, RecipientId, "  Hi  ", "  Text  ", SentAt);

        Assert.Equal("Hi",   msg.Subject);
        Assert.Equal("Text", msg.Body);
    }

    [Fact]
    public void Create_EachCallProducesUniqueId()
    {
        var a = ReaderMessage.Create(AuthorId, RecipientId, "S", "B", SentAt);
        var b = ReaderMessage.Create(AuthorId, RecipientId, "S", "B", SentAt);

        Assert.NotEqual(a.Id, b.Id);
    }
}
