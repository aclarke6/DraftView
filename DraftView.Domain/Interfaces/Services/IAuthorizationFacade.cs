namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Provides role and identity information about the current caller.
/// Implemented in the Web layer using ASP.NET Identity claims.
/// Injected into application services to avoid domain/infrastructure coupling.
/// </summary>
public interface IAuthorizationFacade
{
    bool IsAuthor();
    bool IsSystemSupport();
    bool IsBetaReader();
    string? GetCurrentUserEmail();
}