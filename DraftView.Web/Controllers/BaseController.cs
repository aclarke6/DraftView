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

    private User? _currentUser;
    private bool  _userResolved;

    protected async Task<User?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        if (_userResolved) return _currentUser;

        var email    = User.Identity?.Name;
        _currentUser = email is null ? null : await userRepo.GetByEmailAsync(email, ct);
        _userResolved = true;
        return _currentUser;
    }

    // ---------------------------------------------------------------------------
    // Role helpers
    // ---------------------------------------------------------------------------

    protected async Task<bool> IsAuthorAsync(CancellationToken ct = default)
    {
        var user = await GetCurrentUserAsync(ct);
        return user?.Role == Role.Author;
    }

    protected async Task<bool> IsReaderAsync(CancellationToken ct = default)
    {
        var user = await GetCurrentUserAsync(ct);
        return user?.Role == Role.BetaReader;
    }

    /// <summary>
    /// Returns Forbid() if the current user is not an Author.
    /// Use at the top of Author-only actions as a guard.
    /// </summary>
    protected async Task<IActionResult?> RequireAuthorAsync(CancellationToken ct = default)
    {
        if (!await IsAuthorAsync(ct)) return Forbid();
        return null;
    }

    /// <summary>
    /// Returns the current domain user or Forbid() if not found.
    /// Use at the top of actions that require an authenticated domain user.
    /// </summary>
    protected async Task<(User? User, IActionResult? Error)> RequireUserAsync(
        CancellationToken ct = default)
    {
        var user = await GetCurrentUserAsync(ct);
        if (user is null) return (null, Forbid());
        return (user, null);
    }

    // ---------------------------------------------------------------------------
    // Populate ViewBag before every action so layouts can check role
    // ---------------------------------------------------------------------------

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await GetCurrentUserAsync();
            ViewBag.IsAuthor = user?.Role == Role.Author;
            ViewBag.IsReader = user?.Role == Role.BetaReader;
            ViewBag.CurrentUser = user;
        }
        else
        {
            ViewBag.IsAuthor = false;
            ViewBag.IsReader = false;
            ViewBag.CurrentUser = null;
        }

        await next();
    }
}
