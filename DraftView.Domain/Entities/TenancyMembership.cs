using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

/// <summary>
/// Represents the Author-Tenancy link: exactly one membership per tenancy, always Role=Author.
/// Reader access (including authors reading other authors' projects) is granted at the project
/// level via ReaderAccess and does not create a TenancyMembership in the host tenancy.
/// </summary>
public sealed class TenancyMembership
{
    public Guid Id { get; private set; }
    public Guid TenancyId { get; private set; }
    public Guid AccountId { get; private set; }
    public TenancyRole Role { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public bool IsSoftDeleted { get; private set; }
    public DateTime? SoftDeletedAt { get; private set; }

    private TenancyMembership() { }

    /// <summary>
    /// Creates a new TenancyMembership linking an Account to a Tenancy with the given role.
    /// </summary>
    public static TenancyMembership Create(Guid tenancyId, Guid accountId, TenancyRole role)
    {
        if (tenancyId == Guid.Empty)
            throw new InvariantViolationException("I-TMEM-TENANCY",
                "TenancyMembership must reference a valid tenancy.");
        if (accountId == Guid.Empty)
            throw new InvariantViolationException("I-TMEM-ACCOUNT",
                "TenancyMembership must reference a valid account.");
        return new TenancyMembership
        {
            Id            = Guid.NewGuid(),
            TenancyId     = tenancyId,
            AccountId     = accountId,
            Role          = role,
            JoinedAt      = DateTime.UtcNow,
            IsSoftDeleted = false
        };
    }

    /// <summary>
    /// Soft-deletes the membership, effectively removing access. Idempotent.
    /// </summary>
    public void SoftDelete()
    {
        if (IsSoftDeleted) return;
        IsSoftDeleted = true;
        SoftDeletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Restores a soft-deleted membership, reinstating access.
    /// </summary>
    public void Restore()
    {
        IsSoftDeleted = false;
        SoftDeletedAt = null;
    }
}
