using DraftView.Application.Contracts;

namespace DraftView.Application.Interfaces;

public interface IControlledUserEmailService
{
    Task<string> GetEmailAsync(UserEmailAccessRequest request, CancellationToken ct = default);
}
