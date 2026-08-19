using DraftView.Application.Interfaces;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Atomic author registration bootstrap.
/// Creates Account → Tenancy → TenancyMembership (Author) → TenancySubscription (Free)
/// in a single SaveChanges call. Fails as a unit if any step throws.
/// </summary>
public class AuthorRegistrationService(
    IAccountRepository accountRepo,
    ITenancyRepository tenancyRepo,
    ITenancyMembershipRepository membershipRepo,
    ITenancySubscriptionRepository subscriptionRepo,
    IUserEmailLookupHmacService hmacService,
    IUnitOfWork unitOfWork) : IAuthorRegistrationService
{
    public async Task<AuthorRegistrationResult> RegisterAsync(
        string email,
        string displayName,
        string tenancyName,
        CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim();
        var hmac = hmacService.Compute(normalizedEmail);

        if (await accountRepo.EmailExistsAsync(hmac, ct))
            throw new InvariantViolationException("I-REG-EMAIL-EXISTS",
                "An account with this email address already exists.");

        var account     = Account.Create(normalizedEmail, displayName);
        var tenancy     = Tenancy.Create(account.Id, tenancyName);
        var membership  = TenancyMembership.Create(tenancy.Id, account.Id, TenancyRole.Author);
        var subscription = TenancySubscription.Create(tenancy.Id, SubscriptionTier.Free);

        await accountRepo.AddAsync(account, ct);
        await tenancyRepo.AddAsync(tenancy, ct);
        await membershipRepo.AddAsync(membership, ct);
        await subscriptionRepo.AddAsync(subscription, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new AuthorRegistrationResult(account, tenancy, membership, subscription);
    }
}
