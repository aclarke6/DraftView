using Microsoft.Extensions.Configuration;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class UserService(
    IUserRepository userRepo,
    IInvitationRepository invitationRepo,
    IUserNotificationPreferencesRepository prefsRepo,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    IConfiguration configuration) : IUserService
{
    public async Task<Invitation> IssueInvitationAsync(
        string email, ExpiryPolicy expiryPolicy, DateTime? expiresAt,
        Guid authorId, CancellationToken ct = default)
    {
        var actor = await userRepo.GetByIdAsync(authorId, ct)
            ?? throw new EntityNotFoundException(nameof(User), authorId);

        if (actor.Role != Role.Author)
            throw new UnauthorisedOperationException("Only the Author may issue invitations.");

        if (await userRepo.EmailExistsAsync(email, ct))
            throw new InvariantViolationException("I-EMAIL-EXISTS",
                $"A user with email {email} already exists.");

        var user = User.Create(email, "Pending", Role.BetaReader);

        if (expiryPolicy == ExpiryPolicy.ExpiresAt && !expiresAt.HasValue)
            throw new InvariantViolationException("I-INVITE-EXPIRY-REQUIRED",
                "An expiry date is required when the invitation is set to expire.");

        var invitation = expiryPolicy == ExpiryPolicy.AlwaysOpen
            ? Invitation.CreateAlwaysOpen(user.Id)
            : Invitation.CreateWithExpiry(
                user.Id,
                expiresAt ?? throw new InvariantViolationException(
                    "I-INVITE-EXPIRY-REQUIRED",
                    "An expiry date is required when the invitation is set to expire."));

        var prefs = UserNotificationPreferences.CreateForBetaReader(user.Id);

        await userRepo.AddAsync(user, ct);
        await invitationRepo.AddAsync(invitation, ct);
        await prefsRepo.AddAsync(prefs, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var baseUrl = configuration["App:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5078";
        var inviteUrl = $"{baseUrl}/Account/AcceptInvitation?token={invitation.Token}";

        var expiryLine = expiryPolicy == ExpiryPolicy.AlwaysOpen
            ? "<p>This invitation does not expire.</p>"
            : $"<p>This invitation expires on <strong>{expiresAt!.Value:d MMMM yyyy}</strong>.</p>";

        var toName = email.Contains('@') ? email.Split('@')[0] : email;

        await emailSender.SendAsync(
            email,
            toName,
            "You have been invited to review a manuscript on DraftView",
            $"<p>Click the link below to accept your invitation and create your account.</p>" +
            $"<p><a href=\"{inviteUrl}\">{inviteUrl}</a></p>" +
            expiryLine,
            ct);

        return invitation;
    }

    public async Task<User> AcceptInvitationAsync(
        string token,
        string displayName,
        CancellationToken ct = default)
    {
        var invitation = await invitationRepo.GetByTokenAsync(token, ct)
            ?? throw new EntityNotFoundException("Invitation", Guid.Empty);

        if (!invitation.IsValid())
            throw new InvariantViolationException("I-INVITE-STATE",
                "This invitation is no longer valid.");

        var user = await userRepo.GetByIdAsync(invitation.UserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), invitation.UserId);

        user.AcceptInvitation(displayName);
        invitation.Accept();

        await unitOfWork.SaveChangesAsync(ct);
        return user;
    }

    public async Task CancelInvitationAsync(
        Guid invitationId, Guid authorId, CancellationToken ct = default)
    {
        var actor = await userRepo.GetByIdAsync(authorId, ct)
            ?? throw new EntityNotFoundException(nameof(User), authorId);

        if (actor.Role != Role.Author)
            throw new UnauthorisedOperationException("Only the Author may cancel invitations.");

        var invitation = await invitationRepo.GetByIdAsync(invitationId, ct)
            ?? throw new EntityNotFoundException("Invitation", invitationId);

        invitation.Cancel();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeactivateUserAsync(
        Guid targetUserId, Guid authorId, CancellationToken ct = default)
    {
        await RequireAuthorAsync(authorId, ct);

        var target = await userRepo.GetByIdAsync(targetUserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), targetUserId);

        target.Deactivate();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ReactivateUserAsync(
        Guid targetUserId, Guid authorId, CancellationToken ct = default)
    {
        await RequireAuthorAsync(authorId, ct);

        var target = await userRepo.GetByIdAsync(targetUserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), targetUserId);

        target.Activate();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteUserAsync(
        Guid targetUserId, Guid authorId, CancellationToken ct = default)
    {
        await RequireAuthorAsync(authorId, ct);

        var target = await userRepo.GetByIdAsync(targetUserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), targetUserId);

        target.SoftDelete();
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task RequireAuthorAsync(Guid actorId, CancellationToken ct)
    {
        var actor = await userRepo.GetByIdAsync(actorId, ct)
            ?? throw new EntityNotFoundException(nameof(User), actorId);

        if (actor.Role != Role.Author)
            throw new UnauthorisedOperationException("Only the Author may perform this action.");
    }
}


