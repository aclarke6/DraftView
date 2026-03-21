using ScrivenerSync.Domain.Enumerations;
using ScrivenerSync.Domain.Exceptions;

namespace ScrivenerSync.Domain.Entities;

public sealed class Invitation
{
    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = default!;
    public ExpiryPolicy ExpiryPolicy { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    private Invitation() { }

    // ---------------------------------------------------------------------------
    // Factories
    // ---------------------------------------------------------------------------

    public static Invitation CreateAlwaysOpen(Guid userId)
    {
        return new Invitation
        {
            Id           = Guid.NewGuid(),
            UserId       = userId,
            Token        = GenerateToken(),
            ExpiryPolicy = ExpiryPolicy.AlwaysOpen,
            ExpiresAt    = null,
            Status       = InvitationStatus.Pending,
            IssuedAt     = DateTime.UtcNow
        };
    }

    public static Invitation CreateWithExpiry(Guid userId, DateTime expiresAt)
    {
        if (expiresAt <= DateTime.UtcNow)
            throw new InvariantViolationException("I-EXPIRY",
                "Invitation expiry date must be in the future.");

        return new Invitation
        {
            Id           = Guid.NewGuid(),
            UserId       = userId,
            Token        = GenerateToken(),
            ExpiryPolicy = ExpiryPolicy.ExpiresAt,
            ExpiresAt    = expiresAt,
            Status       = InvitationStatus.Pending,
            IssuedAt     = DateTime.UtcNow
        };
    }

    // ---------------------------------------------------------------------------
    // Behaviour
    // ---------------------------------------------------------------------------

    public void Accept()
    {
        EnforcePending();

        Status     = InvitationStatus.Accepted;
        AcceptedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == InvitationStatus.Cancelled)
            return;

        if (Status == InvitationStatus.Accepted)
            throw new InvariantViolationException("I-INVITE-STATE",
                "An accepted invitation cannot be cancelled.");

        if (Status == InvitationStatus.Expired)
            throw new InvariantViolationException("I-INVITE-STATE",
                "An expired invitation cannot be cancelled.");

        Status       = InvitationStatus.Cancelled;
        CancelledAt  = DateTime.UtcNow;
    }

    public void ForceExpire()
    {
        if (Status == InvitationStatus.Pending)
            Status = InvitationStatus.Expired;
    }

    public bool IsValid()
    {
        if (Status != InvitationStatus.Pending)
            return false;

        if (ExpiryPolicy == ExpiryPolicy.ExpiresAt && ExpiresAt <= DateTime.UtcNow)
            return false;

        return true;
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private void EnforcePending()
    {
        if (Status != InvitationStatus.Pending)
            throw new InvariantViolationException("I-INVITE-STATE",
                $"Invitation cannot be accepted in status {Status}.");
    }

    private static string GenerateToken() =>
        Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
}
