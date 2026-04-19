using System.Diagnostics;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Web.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DraftView.Web.Controllers;

public class HomeController(IUserRepository userRepo) : BaseController(userRepo)
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
    public IActionResult NotFoundPage()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    [HttpGet]
    public IActionResult StatusCodeError(int statusCode)
    {
        if (statusCode == 404)
            return NotFoundPage();

        var (heading, message) = statusCode switch
        {
            405 => ("Action not allowed",   "That request method is not permitted here."),
            403 => ("Access denied",         "You don't have permission to view that page."),
            _   => ("Something went wrong",  "An unexpected error occurred. Please try again.")
        };

        var model = new ErrorPageViewModel
        {
            Heading              = heading,
            Message              = message,
            StatusCode           = statusCode,
            ErrorReference       = HttpContext.TraceIdentifier,
            RequestPath          = Request.Path.Value ?? string.Empty,
            ShowTechnicalDetails = false
        };

        Response.StatusCode = statusCode;
        return View("Error", model);
    }

    [HttpGet]
    public IActionResult TestException()
    {
        throw new NotImplementedException("This is a Support test");
    }

    // ---------------------------------------------------------------------------
    // Test error pages (unauthenticated access permitted — for testing all states)
    // ---------------------------------------------------------------------------
    [HttpGet]
    public IActionResult Test404() => NotFoundPage();

    [HttpGet]
    public IActionResult Test405() => StatusCodeError(405);

    [HttpGet]
    public IActionResult Test403() => StatusCodeError(403);

    [HttpGet]
    public IActionResult Test500()
    {
        throw new InvalidOperationException("Deliberate 500 test.");
    }

    [HttpGet]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var exception = feature?.Error;
        var isDevelopment = HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment();

        var showTechnicalDetails = isDevelopment || User.IsInRole("SystemSupport");        
        var sourceArea = GetSourceArea(exception);
        var systemName = GetSystemName(exception);
        var errorReference = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        var model = new ErrorPageViewModel {
            Heading = "Something went wrong",
            Message = GetControlledMessage(sourceArea, systemName),
            ErrorReference = errorReference,
            RequestPath = feature?.Path ?? Request.Path.Value ?? string.Empty,
            SourceArea = sourceArea,
            SystemName = systemName,
            ExceptionType = exception?.GetType().FullName,
            ExceptionMessage = exception?.Message,
            StackTrace = exception?.StackTrace,
            InnerException = exception?.InnerException?.ToString(),
            ShowTechnicalDetails = showTechnicalDetails
        };

        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("Error", model);
    }

    private static string GetControlledMessage(string sourceArea, string? systemName)
    {
        if (!string.IsNullOrWhiteSpace(systemName))
            return $"A failure occurred in {systemName} while processing this request.";

        return sourceArea switch {
            "Domain" => "A business rule failed while processing this request.",
            "Application" => "The application could not complete this request.",
            "Infrastructure" => "A platform service failed while processing this request.",
            "Integration" => "An external integration failed while processing this request.",
            "Web" => "The web application could not complete this request.",
            _ => "The system could not complete this request."
        };
    }

    private static string GetSourceArea(Exception? exception)
    {
        var signature = string.Join(" | ",
            exception?.GetType().FullName,
            exception?.TargetSite?.DeclaringType?.FullName,
            exception?.StackTrace);

        if (string.IsNullOrWhiteSpace(signature))
            return "Unknown";

        if (signature.Contains("DraftView.Domain", StringComparison.Ordinal))
            return "Domain";

        if (signature.Contains("DraftView.Application", StringComparison.Ordinal))
            return "Application";

        if (signature.Contains("DraftView.Infrastructure", StringComparison.Ordinal))
            return "Infrastructure";

        if (signature.Contains("DraftView.Web", StringComparison.Ordinal))
            return "Web";

        if (signature.Contains("Dropbox", StringComparison.OrdinalIgnoreCase))
            return "Integration";

        return "Unknown";
    }

    private static string? GetSystemName(Exception? exception)
    {
        var signature = string.Join(" | ",
            exception?.GetType().FullName,
            exception?.TargetSite?.DeclaringType?.FullName,
            exception?.StackTrace);

        if (string.IsNullOrWhiteSpace(signature))
            return null;

        if (signature.Contains("Dropbox", StringComparison.OrdinalIgnoreCase))
            return "Dropbox";

        return null;
    }
}
