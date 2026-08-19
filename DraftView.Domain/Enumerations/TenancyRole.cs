namespace DraftView.Domain.Enumerations;

/// <summary>
/// The role an Account holds within a specific Tenancy.
/// Author represents the tenancy owner. BetaReader represents an invited reader.
/// </summary>
public enum TenancyRole
{
    Author,
    BetaReader
}
