using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

public interface IReaderSelfRegistrationService
{
    Task<ReaderSelfRegistrationResult> RegisterAsync(
        string email, string displayName, CancellationToken ct = default);
}

public record ReaderSelfRegistrationResult(User User);
