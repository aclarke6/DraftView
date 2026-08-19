using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

/// <summary>
/// Represents an author-owned workspace containing projects, readers, notifications,
/// and integrations. The primary isolation boundary for all tenancy-scoped data.
/// MaxBetaReaderCount is seeded to 5 for all tenancies before billing is live.
/// </summary>
public sealed class Tenancy
{
    private const int DefaultMaxBetaReaderCount = 5;

    public Guid Id { get; private set; }
    public Guid OwnerAccountId { get; private set; }
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Operational limit on beta readers. Not user-editable; set by platform or billing.
    /// Pre-billing default is 5.
    /// </summary>
    public int MaxBetaReaderCount { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsSoftDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SoftDeletedAt { get; private set; }

    private Tenancy() { }

    /// <summary>
    /// Creates a new active Tenancy owned by the given Account.
    /// MaxBetaReaderCount defaults to 5 (pre-billing operational cap).
    /// </summary>
    public static Tenancy Create(Guid ownerAccountId, string name)
    {
        if (ownerAccountId == Guid.Empty)
            throw new InvariantViolationException("I-TEN-OWNER",
                "Tenancy must be associated with a valid owner account.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvariantViolationException("I-TEN-NAME",
                "Tenancy name must not be null or whitespace.");
        return new Tenancy
        {
            Id                 = Guid.NewGuid(),
            OwnerAccountId     = ownerAccountId,
            Name               = name.Trim(),
            MaxBetaReaderCount = DefaultMaxBetaReaderCount,
            IsActive           = true,
            IsSoftDeleted      = false,
            CreatedAt          = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates the tenancy name. The new value is trimmed.
    /// </summary>
    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvariantViolationException("I-TEN-NAME",
                "Tenancy name must not be null or whitespace.");
        Name = name.Trim();
    }

    /// <summary>
    /// Soft-deletes the tenancy. Idempotent.
    /// </summary>
    public void SoftDelete()
    {
        if (IsSoftDeleted) return;
        IsSoftDeleted = true;
        SoftDeletedAt = DateTime.UtcNow;
    }
}
