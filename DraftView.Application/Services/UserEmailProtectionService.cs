using DraftView.Application.Interfaces;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Application.Services;

public sealed class UserEmailProtectionService(
    IUserRepository userRepository,
    IUserEmailEncryptionService emailEncryptionService) : IUserEmailProtectionService
{
    public async Task<string> GetEmailAsync(Guid targetUserId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(targetUserId, ct);
        if (user is null)
            throw new InvalidOperationException("User not found.");

        if (string.IsNullOrWhiteSpace(user.EmailCiphertext))
            throw new InvalidOperationException("Protected email is not available for the target user.");

        return emailEncryptionService.Decrypt(user.EmailCiphertext);
    }
}
