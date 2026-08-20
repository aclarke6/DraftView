using DraftView.Application.Interfaces;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Atomic author self-registration bootstrap.
/// Creates User (Author, inactive) → Account → Tenancy → TenancyMembership (Author)
/// → TenancySubscription (Free) in a single SaveChanges call.
/// The web layer is responsible for creating the ASP.NET Identity record and sending
/// the email confirmation link.
/// </summary>
public class AuthorSelfRegistrationService(
    IUserRepository userRepo,
    IAccountRepository accountRepo,
    ITenancyRepository tenancyRepo,
    ITenancyMembershipRepository membershipRepo,
    ITenancySubscriptionRepository subscriptionRepo,
    IUserEmailLookupHmacService hmacService,
    IUnitOfWork unitOfWork) : IAuthorSelfRegistrationService
{
    public async Task<AuthorSelfRegistrationResult> RegisterAsync(
        string email,
        string displayName,
        string tenancyName,
        CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim();
        var hmac = hmacService.Compute(normalizedEmail);

        if (await accountRepo.EmailExistsAsync(hmac, ct))
            throw new InvariantViolationException("I-SELF-REG-EMAIL-EXISTS",
                "An account with this email address already exists.");

        if (await userRepo.EmailExistsAsync(normalizedEmail, ct))
            throw new InvariantViolationException("I-SELF-REG-EMAIL-EXISTS",
                "A user with this email address already exists.");

        var user         = User.Create(normalizedEmail, displayName, Role.Author);
        var account      = Account.Create(normalizedEmail, displayName);
        var tenancy      = Tenancy.Create(account.Id, tenancyName);
        var membership   = TenancyMembership.Create(tenancy.Id, account.Id, TenancyRole.Author);
        var subscription = TenancySubscription.Create(tenancy.Id, SubscriptionTier.Free);

        await userRepo.AddAsync(user, ct);
        await accountRepo.AddAsync(account, ct);
        await tenancyRepo.AddAsync(tenancy, ct);
        await membershipRepo.AddAsync(membership, ct);
        await subscriptionRepo.AddAsync(subscription, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new AuthorSelfRegistrationResult(user, account, tenancy, membership, subscription);
    }
}
