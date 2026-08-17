using System.Diagnostics;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Web.Helpers;
using DraftView.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DraftView.Web.Controllers;

public class HomeController(IUserRepository userRepo, IUserPreferencesRepository prefsRepo)
    : BaseController(userRepo)
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        if (User.IsInRole("SystemSupport"))
            return RedirectToAction("Dashboard", "Support");

        if (User.IsInRole("Author"))
            return RedirectToAction("Dashboard", "Author");

        return RedirectToAction("Dashboard", "Reader");
    }

    [HttpGet]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> NotFoundPage()
    {
        var theme = await GetActiveThemeAsync();
        Response.StatusCode = StatusCodes.Status404NotFound;
        ViewData["NotFoundMessage"] = ThemeMessages.Get404(theme);
        return View("NotFound");
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ForbiddenPage()
    {
        var theme = await GetActiveThemeAsync();
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("Error", CreateStatusCodeModel(
            StatusCodes.Status403Forbidden,
            "Access denied",
            ThemeMessages.Get403(theme)));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> MethodNotAllowedPage()
    {
        var theme = await GetActiveThemeAsync();
        Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        return View("Error", CreateStatusCodeModel(
            StatusCodes.Status405MethodNotAllowed,
            "Method not allowed",
            ThemeMessages.Get405(theme)));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> StatusCodeError(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status403Forbidden        => await ForbiddenPage(),
            StatusCodes.Status404NotFound         => await NotFoundPage(),
            StatusCodes.Status405MethodNotAllowed => await MethodNotAllowedPage(),
            _                                     => await Error()
        };
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Test403()
    {
        return StatusCode(StatusCodes.Status403Forbidden);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Test404()
    {
        return NotFound();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Test405()
    {
        return StatusCode(StatusCodes.Status405MethodNotAllowed);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Test500()
    {
        return StatusCode(StatusCodes.Status500InternalServerError);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Error()
    {
        var feature   = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var exception = feature?.Error;
        var isDevelopment = HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment();

        var showTechnicalDetails = isDevelopment || User.IsInRole("SystemSupport");
        var sourceArea  = GetSourceArea(exception);
        var systemName  = GetSystemName(exception);
        var errorReference = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var theme = await GetActiveThemeAsync();

        var model = new ErrorPageViewModel
        {
            Heading              = "Something went wrong",
            Message              = ThemeMessages.Get500(theme),
            StatusCode           = StatusCodes.Status500InternalServerError,
            ErrorReference       = errorReference,
            RequestPath          = feature?.Path ?? Request.Path.Value ?? string.Empty,
            SourceArea           = sourceArea,
            SystemName           = systemName,
            ExceptionType        = exception?.GetType().FullName,
            ExceptionMessage     = exception?.Message,
            StackTrace           = exception?.StackTrace,
            InnerException       = exception?.InnerException?.ToString(),
            ShowTechnicalDetails = showTechnicalDetails
        };

        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("Error", model);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<string> GetActiveThemeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
            return "Light";

        var user = await GetCurrentUserAsync();
        if (user is null) return "Light";

        var prefs = await prefsRepo.GetByUserIdAsync(user.Id);
        return prefs?.DisplayTheme.ToString() ?? "Light";
    }

    private static string GetSourceArea(Exception? exception)
    {
        var signature = string.Join(" | ",
            exception?.GetType().FullName,
            exception?.TargetSite?.DeclaringType?.FullName,
            exception?.StackTrace);

        if (string.IsNullOrWhiteSpace(signature)) return "Unknown";
        if (signature.Contains("DraftView.Domain",         StringComparison.Ordinal))           return "Domain";
        if (signature.Contains("DraftView.Application",    StringComparison.Ordinal))           return "Application";
        if (signature.Contains("DraftView.Infrastructure", StringComparison.Ordinal))           return "Infrastructure";
        if (signature.Contains("DraftView.Web",            StringComparison.Ordinal))           return "Web";
        if (signature.Contains("Dropbox",                  StringComparison.OrdinalIgnoreCase)) return "Integration";

        return "Unknown";
    }

    private static string? GetSystemName(Exception? exception)
    {
        var signature = string.Join(" | ",
            exception?.GetType().FullName,
            exception?.TargetSite?.DeclaringType?.FullName,
            exception?.StackTrace);

        if (string.IsNullOrWhiteSpace(signature)) return null;
        if (signature.Contains("Dropbox", StringComparison.OrdinalIgnoreCase)) return "Dropbox";

        return null;
    }

    private ErrorPageViewModel CreateStatusCodeModel(int statusCode, string heading, string message)
    {
        var errorReference = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        return new ErrorPageViewModel
        {
            Heading              = heading,
            Message              = message,
            StatusCode           = statusCode,
            ErrorReference       = errorReference,
            RequestPath          = Request.Path.Value ?? string.Empty,
            SourceArea           = "Web",
            SystemName           = "DraftView",
            ShowTechnicalDetails = false
        };
    }
}
