using Microsoft.Extensions.Configuration;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Services;

public class UserService(
    IUserRepository userRepo,
    IInvitationRepository invitationRepo,
    IUserPreferencesRepository prefsRepo,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    IConfiguration configuration,
    IReaderAccessRepository readerAccessRepo,
    IAuthorizationFacade authFacade,
    IAuthorNotificationRepository notificationRepo) : IUserService

{
    public async Task<Invitation> IssueInvitationAsync(
        string email, ExpiryPolicy expiryPolicy, DateTime? expiresAt,
        Guid authorId, CancellationToken ct = default)
    {
        if (!authFacade.IsAuthor())
            throw new UnauthorisedOperationException("Only the Author may issue invitations.");

        var user = await userRepo.GetByEmailAsync(email, ct);
        var isExistingPendingInvitee = user is not null
            && user.Role == Role.BetaReader
            && !user.IsActive
            && !user.IsSoftDeleted;

        if (user is not null && !isExistingPendingInvitee)
            throw new InvariantViolationException("I-EMAIL-EXISTS",
                $"A user with email {email} already exists.");

        if (user is null)
        {
            user = User.Create(email, "Pending", Role.BetaReader);
        }

        if (expiryPolicy == ExpiryPolicy.ExpiresAt && !expiresAt.HasValue)
            throw new InvariantViolationException("I-INVITE-EXPIRY-REQUIRED",
                "An expiry date is required when the invitation is set to expire.");

        foreach (var pendingInvitation in await invitationRepo.GetPendingByUserIdAsync(user.Id, ct))
        {
            pendingInvitation.Cancel();
        }

        var invitation = expiryPolicy == ExpiryPolicy.AlwaysOpen
            ? Invitation.CreateAlwaysOpen(user.Id)
            : Invitation.CreateWithExpiry(
                user.Id,
                expiresAt ?? throw new InvariantViolationException(
                    "I-INVITE-EXPIRY-REQUIRED",
                    "An expiry date is required when the invitation is set to expire."));

        if (!isExistingPendingInvitee)
        {
            var prefs = UserPreferences.CreateForBetaReader(user.Id);
            await userRepo.AddAsync(user, ct);
            await prefsRepo.AddAsync(prefs, ct);
        }

        await invitationRepo.AddAsync(invitation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var baseUrl = GetConfiguredAppBaseUrl();
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

        var author = await userRepo.GetAuthorAsync(ct);
        if (author is not null)
        {
            var notification = AuthorNotification.Create(
                author.Id,
                NotificationEventType.ReaderJoined,
                $"{user.DisplayName} accepted their invitation",
                null,
                "/Author/Readers",
                DateTime.UtcNow);
            await notificationRepo.AddAsync(notification, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return user;
    }

    public async Task CancelInvitationAsync(
        Guid invitationId, Guid authorId, CancellationToken ct = default)
    {
        if (!authFacade.IsAuthor())
            throw new UnauthorisedOperationException("Only the Author may cancel invitations.");

        var invitation = await invitationRepo.GetByIdAsync(invitationId, ct)
            ?? throw new EntityNotFoundException("Invitation", invitationId);

        invitation.Cancel();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeactivateUserAsync(
        Guid targetUserId, Guid authorId, CancellationToken ct = default)
    {
        OnlyAllowAuthorOrSystemSupport();

        var target = await userRepo.GetByIdAsync(targetUserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), targetUserId);

        await readerAccessRepo.RevokeAllForReaderAsync(targetUserId, authorId, ct);
        target.Deactivate();
        await unitOfWork.SaveChangesAsync(ct);
    }


    public async Task ReactivateUserAsync(
        Guid targetUserId, Guid authorId, CancellationToken ct = default)
    {
        OnlyAllowAuthorOrSystemSupport();

        var target = await userRepo.GetByIdAsync(targetUserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), targetUserId);

        target.Activate();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteUserAsync(
        Guid targetUserId, Guid authorId, CancellationToken ct = default)
    {
        OnlyAllowAuthorOrSystemSupport();

        var target = await userRepo.GetByIdAsync(targetUserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), targetUserId);

        target.SoftDelete();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken ct = default)
    {
        var user = await userRepo.GetByIdAsync(userId, ct)
            ?? throw new EntityNotFoundException(nameof(User), userId);
        user.UpdateDisplayName(displayName);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task UpdateDisplayThemeAsync(Guid userId, DisplayTheme displayTheme, CancellationToken ct = default)
    {
        var prefs = await prefsRepo.GetByUserIdAsync(userId, ct)
            ?? throw new EntityNotFoundException(nameof(UserPreferences), userId);

        prefs.UpdateDisplayTheme(displayTheme);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task UpdateProseFontPreferencesAsync(Guid userId, ProseFont proseFont, ProseFontSize proseFontSize, CancellationToken ct = default)
    {
        var prefs = await prefsRepo.GetByUserIdAsync(userId, ct)
            ?? throw new EntityNotFoundException(nameof(UserPreferences), userId);

        prefs.UpdateProseFontPreferences(proseFont, proseFontSize);

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task UpdateEmailAsync(Guid userId, string email, CancellationToken ct = default)
    {
        var user = await userRepo.GetByIdAsync(userId, ct)
            ?? throw new EntityNotFoundException(nameof(User), userId);
        if (await userRepo.EmailExistsAsync(email, ct))
            throw new InvariantViolationException("I-EMAIL-EXISTS",
                $"A user with email {email} already exists.");
        user.UpdateEmail(email);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Only Allow the Author or SystemSupport to perform the action.
    /// This is used for actions that may be performed by SystemSupport on behalf of the Author, such as
    /// deactivating a user who has requested support with account access issues.
    /// </summary>
    /// <exception cref="UnauthorisedOperationException"></exception>
    private void OnlyAllowAuthorOrSystemSupport()
    {
        if (!authFacade.IsAuthor() && !authFacade.IsSystemSupport())
            throw new UnauthorisedOperationException("Only the Author or SystemSupport may perform this action.");
    }

    private string GetConfiguredAppBaseUrl()
    {
        var configuredBaseUrl = configuration["App:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
            throw new InvalidOperationException(
                "Missing required configuration value 'App:BaseUrl'. Invitation emails require an absolute live base URL.");

        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Scheme) ||
            string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException(
                "Configuration value 'App:BaseUrl' must be a valid absolute URL.");

        return configuredBaseUrl;
    }
}



