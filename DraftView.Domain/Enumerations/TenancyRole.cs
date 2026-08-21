namespace DraftView.Domain.Enumerations;

/// <summary>
/// The role an Account holds within a Tenancy.
/// TenancyMembership is reserved for the Author-Tenancy 1:1 link only.
/// Reader access (including authors reading other authors' projects) is granted
/// at the project level via ReaderAccess, not via TenancyMembership.
/// </summary>
public enum TenancyRole
{
    Author
}
