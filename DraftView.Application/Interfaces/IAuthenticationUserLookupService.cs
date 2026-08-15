using DraftView.Domain.Entities;

namespace DraftView.Application.Interfaces;

public interface IAuthenticationUserLookupService
{
    Task<User?> FindByLoginEmailAsync(string emailInput, CancellationToken ct = default);
}
