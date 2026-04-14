using DraftView.Application.Interfaces;
using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Application.Services;

public sealed class AuthenticationUserLookupService(IUserRepository userRepository) : IAuthenticationUserLookupService
{
    public async Task<User?> FindByLoginEmailAsync(string emailInput, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(emailInput))
            return null;

        return await userRepository.GetByEmailAsync(emailInput, ct);
    }
}
