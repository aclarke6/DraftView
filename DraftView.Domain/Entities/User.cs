using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public Role Role { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsSoftDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ActivatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public DateTime? LastNotificationCheckAt { get; private set; }
    public DateTime? SoftDeletedAt { get; private set; }

    private User() { }

    public static User Create(string email, string displayName, Role role)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvariantViolationException("I-EMAIL",
                "User email must not be null or whitespace.");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvariantViolationException("I-DISPLAYNAME",
                "User display name must not be null or whitespace.");
        return new User
        {
            Id            = Guid.NewGuid(),
            Email         = email.Trim(),
            DisplayName   = displayName.Trim(),
            Role          = role,
            IsActive      = false,
            IsSoftDeleted = false,
            CreatedAt     = DateTime.UtcNow
        };
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive    = true;
        ActivatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        EnforceNotAuthor("Deactivate");
        IsActive = false;
    }

    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvariantViolationException("I-DISPLAYNAME",
                "Display name must not be null or whitespace.");
        DisplayName = displayName.Trim();
    }

    public void UpdateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvariantViolationException("I-EMAIL",
                "Email must not be null or whitespace.");
        Email = email.Trim();
    }

    public void SoftDelete()
    {
        EnforceNotAuthor("SoftDelete");
        if (IsSoftDeleted) return;
        IsSoftDeleted = true;
        SoftDeletedAt = DateTime.UtcNow;
    }

    public void RecordLogin()
    {
        if (!IsActive)
            throw new UnauthorisedOperationException("An inactive user cannot log in.");
        if (IsSoftDeleted)
            throw new UnauthorisedOperationException("A soft-deleted user cannot log in.");
        LastLoginAt = DateTime.UtcNow;
    }

    public void RecordNotificationCheck()
    {
        LastNotificationCheckAt = DateTime.UtcNow;
    }

    private void EnforceNotAuthor(string operation)
    {
        if (Role == Role.Author)
            throw new InvariantViolationException("I-16",
                $"The Author account may not be {operation}d.");
    }

    public void AcceptInvitation(string displayName)
    {
        if (IsSoftDeleted)
            throw new InvariantViolationException("I-USER-DELETED",
                "A soft-deleted user cannot accept an invitation.");

        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvariantViolationException("I-DISPLAYNAME",
                "User display name must not be null or whitespace.");

        DisplayName = displayName.Trim();
        Activate();
    }
}


