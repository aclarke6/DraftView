using System.ComponentModel.DataAnnotations.Schema;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

/// <summary>
/// Represents a platform identity: the login credential owner and display-name owner.
/// An Account may participate in multiple Tenancies via TenancyMembership.
/// Replaces the single-tenant User identity model under multi-tenancy.
/// </summary>
public sealed class Account
{
    public Guid Id { get; private set; }
    [NotMapped]
    public string Email { get; private set; } = default!;
    public string EmailCiphertext { get; private set; } = string.Empty;
    public string EmailLookupHmac { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public bool IsSoftDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ActivatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public DateTime? SoftDeletedAt { get; private set; }

    private Account() { }

    /// <summary>
    /// Creates a new Account in inactive state. Email and display name are trimmed.
    /// </summary>
    public static Account Create(string email, string displayName)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvariantViolationException("I-ACC-EMAIL",
                "Account email must not be null or whitespace.");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvariantViolationException("I-ACC-DISPLAYNAME",
                "Account display name must not be null or whitespace.");
        return new Account
        {
            Id            = Guid.NewGuid(),
            Email         = email.Trim(),
            DisplayName   = displayName.Trim(),
            IsActive      = false,
            IsSoftDeleted = false,
            CreatedAt     = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks the account as active and records the activation time.
    /// Idempotent — calling when already active is a no-op.
    /// </summary>
    public void Activate()
    {
        if (IsActive) return;
        IsActive    = true;
        ActivatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft-deletes the account, recording the deletion time.
    /// Idempotent — calling when already soft-deleted is a no-op.
    /// </summary>
    public void SoftDelete()
    {
        if (IsSoftDeleted) return;
        IsSoftDeleted = true;
        SoftDeletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a successful login. Throws if the account is inactive or soft-deleted.
    /// </summary>
    public void RecordLogin()
    {
        if (!IsActive)
            throw new UnauthorisedOperationException("An inactive account cannot log in.");
        if (IsSoftDeleted)
            throw new UnauthorisedOperationException("A soft-deleted account cannot log in.");
        LastLoginAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the display name. The new value is trimmed.
    /// </summary>
    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvariantViolationException("I-ACC-DISPLAYNAME",
                "Display name must not be null or whitespace.");
        DisplayName = displayName.Trim();
    }

    /// <summary>
    /// Updates the runtime (decrypted) Email property. Does not affect stored ciphertext.
    /// </summary>
    public void UpdateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvariantViolationException("I-ACC-EMAIL",
                "Email must not be null or whitespace.");
        Email = email.Trim();
    }

    /// <summary>
    /// Stores the encrypted email representation used for persistence and lookup.
    /// </summary>
    public void SetProtectedEmail(string emailCiphertext, string emailLookupHmac)
    {
        if (string.IsNullOrWhiteSpace(emailCiphertext))
            throw new InvariantViolationException("I-ACC-EMAIL-CIPHERTEXT",
                "Email ciphertext must not be null or whitespace.");
        if (string.IsNullOrWhiteSpace(emailLookupHmac))
            throw new InvariantViolationException("I-ACC-EMAIL-HMAC",
                "Email lookup HMAC must not be null or whitespace.");
        EmailCiphertext = emailCiphertext;
        EmailLookupHmac = emailLookupHmac;
    }

    /// <summary>
    /// Loads the decrypted email into the runtime-only Email property after reading from storage.
    /// </summary>
    public void LoadEmailForRuntime(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvariantViolationException("I-ACC-EMAIL",
                "Email must not be null or whitespace.");
        Email = email.Trim();
    }
}
