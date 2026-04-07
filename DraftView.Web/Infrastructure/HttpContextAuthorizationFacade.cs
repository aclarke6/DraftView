using DraftView.Domain.Interfaces.Services;

namespace DraftView.Web.Infrastructure;

/// <summary>
/// Implements IAuthorizationFacade using ASP.NET Identity claims
/// from the current HTTP context.
/// </summary>
public class HttpContextAuthorizationFacade(IHttpContextAccessor httpContextAccessor)
    : IAuthorizationFacade
{
    private System.Security.Claims.ClaimsPrincipal? User =>
        httpContextAccessor.HttpContext?.User;

    public bool IsAuthor() =>
        User?.IsInRole("Author") ?? false;

    public bool IsSystemSupport() =>
        User?.IsInRole("SystemSupport") ?? false;

    public bool IsBetaReader() =>
        User?.IsInRole("BetaReader") ?? false;

    public string? GetCurrentUserEmail() =>
        User?.Identity?.Name;
}