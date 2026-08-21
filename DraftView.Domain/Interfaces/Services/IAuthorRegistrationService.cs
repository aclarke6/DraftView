using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Orchestrates atomic author self-registration: Account + Tenancy + TenancyMembership
/// + TenancySubscription are created together and committed as a single unit of work.
/// Web UI wires ASP.NET Identity registration separately; this service owns the
/// domain record bootstrap only.
/// </summary>
public interface IAuthorRegistrationService
{
    Task<AuthorRegistrationResult> RegisterAsync(
        string email,
        string displayName,
        string tenancyName,
        CancellationToken ct = default);
}

/// <summary>
/// Carries all four records created atomically during author registration.
/// </summary>
public record AuthorRegistrationResult(
    Account Account,
    Tenancy Tenancy,
    TenancyMembership Membership,
    TenancySubscription Subscription);
