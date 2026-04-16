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

        var email    = User.Identity?.Name;
        CurrentUser = email is null ? null : await userRepo.GetByEmailAsync(email, ct);
        UserResolved = true;
        return CurrentUser;
    }

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
}
