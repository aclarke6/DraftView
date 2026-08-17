using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class SystemStateMessageTests
{
    // ---------------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_ValidMessage_SetsPropertiesCorrectly()
    {
        var userId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var msg = SystemStateMessage.Create("System maintenance tonight", userId);

        Assert.NotEqual(Guid.Empty, msg.Id);
        Assert.Equal("System maintenance tonight", msg.Message);
        Assert.True(msg.IsActive);
        Assert.True(msg.CreatedAt >= before && msg.CreatedAt <= DateTime.UtcNow);
        Assert.Equal(userId, msg.CreatedByUserId);
        Assert.Null(msg.DeactivatedAt);
    }

    [Fact]
    public void Create_NullMessage_ThrowsInvariantViolationException()
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => SystemStateMessage.Create(null!, Guid.NewGuid()));

        Assert.Equal("I-SSM-MESSAGE", ex.InvariantCode);
    }

    [Fact]
    public void Create_WhitespaceMessage_ThrowsInvariantViolationException()
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => SystemStateMessage.Create("   ", Guid.NewGuid()));

        Assert.Equal("I-SSM-MESSAGE", ex.InvariantCode);
    }

    [Fact]
    public void Create_TrimsMessage()
    {
        var msg = SystemStateMessage.Create("  hello  ", Guid.NewGuid());

        Assert.Equal("hello", msg.Message);
    }

    [Fact]
    public void Create_DefaultSeverity_IsInfo()
    {
        var msg = SystemStateMessage.Create("msg", Guid.NewGuid());

        Assert.Equal(SystemStateMessageSeverity.Info, msg.Severity);
    }

    [Fact]
    public void Create_WithWarningSeverity_SetsSeverity()
    {
        var msg = SystemStateMessage.Create("msg", Guid.NewGuid(), SystemStateMessageSeverity.Warning);

        Assert.Equal(SystemStateMessageSeverity.Warning, msg.Severity);
    }

    [Fact]
    public void Create_WithCriticalSeverity_SetsSeverity()
    {
        var msg = SystemStateMessage.Create("msg", Guid.NewGuid(), SystemStateMessageSeverity.Critical);

        Assert.Equal(SystemStateMessageSeverity.Critical, msg.Severity);
    }

    // ---------------------------------------------------------------------------
    // Deactivate
    // ---------------------------------------------------------------------------

    [Fact]
    public void Deactivate_WhenActive_SetsIsActiveFalse()
    {
        var msg = SystemStateMessage.Create("System maintenance tonight", Guid.NewGuid());

        msg.Deactivate();

        Assert.False(msg.IsActive);
        Assert.NotNull(msg.DeactivatedAt);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_IsIdempotent()
    {
        var msg = SystemStateMessage.Create("System maintenance tonight", Guid.NewGuid());
        msg.Deactivate();

        var ex = Record.Exception(() => msg.Deactivate());

        Assert.Null(ex);
        Assert.False(msg.IsActive);
    }
}
