using DraftView.Application.Interfaces;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Atomic beta reader self-registration.
/// Creates User (BetaReader, inactive) in a single SaveChanges call.
/// The web layer creates the ASP.NET Identity record and sends the confirmation email.
/// </summary>
public class ReaderSelfRegistrationService(
    IUserRepository userRepo,
    IUnitOfWork unitOfWork) : IReaderSelfRegistrationService
{
    public async Task<ReaderSelfRegistrationResult> RegisterAsync(
        string email,
        string displayName,
        CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim();

        if (await userRepo.EmailExistsAsync(normalizedEmail, ct))
            throw new InvariantViolationException("I-SELF-REG-EMAIL-EXISTS",
                "An account with this email address already exists.");

        var user = User.Create(normalizedEmail, displayName, Role.BetaReader);

        await userRepo.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ReaderSelfRegistrationResult(user);
    }
}
