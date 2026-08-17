using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

public sealed class SystemStateMessage
{
    public Guid                        Id                { get; private set; }
    public string                      Message           { get; private set; } = default!;
    public bool                        IsActive          { get; private set; }
    public DateTime                    CreatedAt         { get; private set; }
    public Guid                        CreatedByUserId   { get; private set; }
    public DateTime?                   DeactivatedAt     { get; private set; }
    public SystemStateMessageSeverity  Severity          { get; private set; }

    private SystemStateMessage() { }

    public static SystemStateMessage Create(
        string message,
        Guid createdByUserId,
        SystemStateMessageSeverity severity = SystemStateMessageSeverity.Info)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new InvariantViolationException("I-SSM-MESSAGE",
                "System state message must not be null or whitespace.");

        return new SystemStateMessage
        {
            Id              = Guid.NewGuid(),
            Message         = message.Trim(),
            IsActive        = true,
            CreatedAt       = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
            Severity        = severity
        };
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive      = false;
        DeactivatedAt = DateTime.UtcNow;
    }
}
