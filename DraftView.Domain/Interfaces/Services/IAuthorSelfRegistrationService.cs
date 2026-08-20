using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Orchestrates atomic author self-registration: User + Account + Tenancy +
/// TenancyMembership + TenancySubscription created and saved in one unit of work.
/// ASP.NET Identity record creation and email confirmation are handled by the web layer.
/// </summary>
public interface IAuthorSelfRegistrationService
{
    Task<AuthorSelfRegistrationResult> RegisterAsync(
        string email,
        string displayName,
        string tenancyName,
        CancellationToken ct = default);
}

public record AuthorSelfRegistrationResult(
    User User,
    Account Account,
    Tenancy Tenancy,
    TenancyMembership Membership,
    TenancySubscription Subscription);
