using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Services;

public interface IUserService
{
    Task<Invitation> IssueInvitationAsync(string email, string displayName, ExpiryPolicy expiryPolicy, DateTime? expiresAt, Guid authorId, CancellationToken ct = default);
    Task<User> AcceptInvitationAsync(string token, string displayName, CancellationToken ct = default);
    Task CancelInvitationAsync(Guid invitationId, Guid authorId, CancellationToken ct = default);
    Task DeactivateUserAsync(Guid targetUserId, Guid authorId, CancellationToken ct = default);
    Task ReactivateUserAsync(Guid targetUserId, Guid authorId, CancellationToken ct = default);
    Task SoftDeleteUserAsync(Guid targetUserId, Guid authorId, CancellationToken ct = default);
    Task UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken ct = default);
    Task UpdateDisplayThemeAsync(Guid userId, DisplayTheme displayTheme, CancellationToken ct = default);
    Task UpdateProseFontPreferencesAsync(Guid userId, ProseFont proseFont, ProseFontSize proseFontSize, CancellationToken ct = default);
    Task UpdateEmailAsync(Guid userId, string email, CancellationToken ct = default);
    Task UpdateReaderProfileAsync(Guid userId, string? bio, string? genreInterests, DraftView.Domain.Enumerations.ReaderPace? pace, CancellationToken ct = default);
    Task ResendInvitationAsync(Guid userId, Guid authorId, CancellationToken ct = default);
}



