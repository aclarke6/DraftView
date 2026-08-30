using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Web.Controllers;

/// <summary>
/// Base controller for all DraftView controllers.
/// Provides current user resolution, role helpers, and ViewBag population
/// so layouts and views can check IsAuthor / IsReader without repeating logic.
/// </summary>
public abstract class BaseController(IUserRepository userRepo) : Controller
{
    // ---------------------------------------------------------------------------
    // Current user - resolved once per request via lazy async init
    // ---------------------------------------------------------------------------

    private User? CurrentUser;
    private bool  UserResolved;

    protected async Task<User?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        if (UserResolved) return CurrentUser;

        // Prefer the explicit email claim (added by DraftViewUserClaimsPrincipalFactory).
        // Fall back to Identity.Name for sessions that pre-date the factory deployment
        // and for users whose UserName already equals their email.
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? User.Identity?.Name;
        CurrentUser = email is null ? null : await userRepo.GetByEmailAsync(email, ct);
        UserResolved = true;
        return CurrentUser;
    }

    protected Task<User?> GetUserByIdAsync(Guid userId, CancellationToken ct = default) =>
        userRepo.GetByIdAsync(userId, ct);

    protected Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default) =>
        userRepo.GetByEmailAsync(email, ct);

    protected async Task<User?> TryGetCurrentAuthorAsync(CancellationToken ct = default)
    {
        var user = await GetCurrentUserAsync(ct);
        return user?.Role == Role.Author ? user : null;
    }

    // ---------------------------------------------------------------------------
    // Role helpers
    // ---------------------------------------------------------------------------

    protected async Task<bool> IsAuthorAsync(CancellationToken ct = default)
    {
        // Prefer claim-based role checks (Identity) for performance; fallback to domain user if claims absent
        if (User?.Identity?.IsAuthenticated == true)
            return User.IsInRole(Role.Author.ToString());

        var user = await GetCurrentUserAsync(ct);
        return user?.Role == Role.Author;
    }

    protected async Task<bool> IsReaderAsync(CancellationToken ct = default)
    {
        // Prefer claim-based role checks (Identity) for performance; fallback to domain user if claims absent
        if (User?.Identity?.IsAuthenticated == true)
            return User.IsInRole(Role.BetaReader.ToString());

        var user = await GetCurrentUserAsync(ct);
        return user?.Role == Role.BetaReader;
    }

    protected async Task<bool> IsSupportAsync(CancellationToken ct = default)
    {
        // Prefer claim-based role checks (Identity) for performance; fallback to domain user if claims absent
        if (User?.Identity?.IsAuthenticated == true)
            return User.IsInRole(Role.SystemSupport.ToString());

        var user = await GetCurrentUserAsync(ct);
        return user?.Role == Role.SystemSupport;
        
    }

    /// <summary>
    /// Attempts to get the current author user. If the user is not an author, returns a Forbid result.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A tuple containing the User if successful, or an error IActionResult if the user is not an author</returns>
    protected async Task<(User? User, IActionResult? Error)> RequireCurrentAuthorAsync(CancellationToken ct = default)
    {
        var author = await TryGetCurrentAuthorAsync(ct);
        if (author is null) return (null, Forbid());
        return (author, null);
    }
}
