using Microsoft.Extensions.Configuration;
using ScrivenerSync.Domain.Entities;
using ScrivenerSync.Domain.Enumerations;
using ScrivenerSync.Domain.Exceptions;
using ScrivenerSync.Domain.Interfaces.Repositories;
using ScrivenerSync.Domain.Interfaces.Services;

namespace ScrivenerSync.Application.Services;

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

        var invitation = expiryPolicy == ExpiryPolicy.AlwaysOpen
            ? Invitation.CreateAlwaysOpen(user.Id)
            : Invitation.CreateWithExpiry(user.Id, expiresAt!.Value);

        var prefs = UserNotificationPreferences.CreateForBetaReader(user.Id);

        await userRepo.AddAsync(user, ct);
        await invitationRepo.AddAsync(invitation, ct);
        await prefsRepo.AddAsync(prefs, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var baseUrl = configuration["App:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5078";
        var inviteUrl = $"{baseUrl}/Account/AcceptInvitation?token={invitation.Token}";

        await emailSender.SendAsync(
            email, "Invited Reader",
            "You have been invited to review a manuscript on ScrivenerSync",
            $"Click the link to accept your invitation: <a href=\"{inviteUrl}\">{inviteUrl}</a>",
            ct);

        return invitation;
    }

    public async Task<User> AcceptInvitationAsync(
        string token, string displayName, string passwordHash,
        CancellationToken ct = default)
    {
        var invitation = await invitationRepo.GetByTokenAsync(token, ct)
            ?? throw new EntityNotFoundException("Invitation", Guid.Empty);

        if (!invitation.IsValid())
            throw new InvariantViolationException("I-INVITE-STATE",
                "This invitation is no longer valid.");

        var user = await userRepo.GetByIdAsync(invitation.UserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), invitation.UserId);

        invitation.Accept();
        user.Activate();

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

