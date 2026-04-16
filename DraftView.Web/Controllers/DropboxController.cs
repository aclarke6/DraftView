using System.Text;
using System.Text.Json;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Infrastructure.Dropbox;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DraftView.Web.Controllers;

[Authorize(Policy = "RequireAuthorPolicy")]
public class DropboxController(
    IUserRepository userRepo,
    IDropboxConnectionRepository connectionRepo,
    IUnitOfWork unitOfWork,
    DropboxClientSettings dropboxSettings,
    ILogger<DropboxController> logger) : BaseController(userRepo)
{
    private static readonly HttpClient Http = new();

    // ---------------------------------------------------------------------------
    // GET /dropbox/connect â€” build OAuth URL and redirect to Dropbox
    // ---------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Connect()
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        // Generate a state token to prevent CSRF
        var state = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{author.Id}:{Guid.NewGuid()}"));

        HttpContext.Session.SetString("dropbox_oauth_state", state);

        var redirectUri = BuildRedirectUri();
        var authUrl = $"https://www.dropbox.com/oauth2/authorize" +
            $"?client_id={Uri.EscapeDataString(dropboxSettings.AppKey)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type=code" +
            $"&token_access_type=offline" +
            $"&state={Uri.EscapeDataString(state)}";

        return Redirect(authUrl);
    }

    // ---------------------------------------------------------------------------
    // GET /dropbox/callback â€” exchange code for tokens, save connection
    // ---------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Callback(string? code, string? state, string? error)
    {
        var (author, err) = await RequireCurrentAuthorAsync();
        if (err is not null || author is null) return err ?? Forbid();

        if (!string.IsNullOrWhiteSpace(error))
        {
            logger.LogWarning("Dropbox OAuth error for user {UserId}: {Error}", author.Id, error);
            TempData["Error"] = "Dropbox connection was cancelled or denied.";
            return RedirectToAction("Settings", "Account");
        }

        // Validate state to prevent CSRF
        var expectedState = HttpContext.Session.GetString("dropbox_oauth_state");
        if (string.IsNullOrWhiteSpace(state) || state != expectedState)
        {
            logger.LogWarning("Dropbox OAuth state mismatch for user {UserId}", author.Id);
            TempData["Error"] = "Invalid OAuth state. Please try connecting again.";
            return RedirectToAction("Settings", "Account");
        }

        HttpContext.Session.Remove("dropbox_oauth_state");

        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "No authorisation code received from Dropbox.";
            return RedirectToAction("Settings", "Account");
        }

        try
        {
            var redirectUri = BuildRedirectUri();
            var (accessToken, refreshToken, expiresAt) =
                await ExchangeCodeAsync(code, redirectUri);

            var connection = await connectionRepo.GetByUserIdAsync(author.Id);
            if (connection is null)
            {
                connection = DropboxConnection.CreateStub(author.Id);
                await connectionRepo.AddAsync(connection);
            }

            connection.Authorise(accessToken, refreshToken, expiresAt);
            await unitOfWork.SaveChangesAsync();

            logger.LogInformation("Dropbox connected for user {UserId}", author.Id);
            TempData["Success"] = "Dropbox connected successfully.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to exchange Dropbox OAuth code for user {UserId}", author.Id);
            TempData["Error"] = "Failed to connect Dropbox. Please try again.";
        }

        return RedirectToAction("Settings", "Account");
    }

    // ---------------------------------------------------------------------------
    // POST /dropbox/disconnect
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disconnect()
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        var connection = await connectionRepo.GetByUserIdAsync(author.Id);
        if (connection is not null)
        {
            connection.Disconnect();
            await unitOfWork.SaveChangesAsync();
        }

        TempData["Success"] = "Dropbox disconnected.";
        return RedirectToAction("Settings", "Account");
    }

    // ---------------------------------------------------------------------------
    // GET /dropbox/status â€” JSON status for dashboard widget
    // ---------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        var connection = await connectionRepo.GetByUserIdAsync(author.Id);

        return Json(new
        {
            status       = connection?.Status.ToString() ?? DropboxConnectionStatus.NotConnected.ToString(),
            authorisedAt = connection?.AuthorisedAt?.ToString("o"),
            isConnected  = connection?.Status == DropboxConnectionStatus.Connected
        });
    }

    // ---------------------------------------------------------------------------
    // GET /dropbox/settings â€” Dropbox connection settings page
    // ---------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        var connection = await connectionRepo.GetByUserIdAsync(author.Id);
        ViewBag.Connection = connection;
        return View();
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------
    private string BuildRedirectUri()
    {
        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}/dropbox/callback";
    }

    private async Task<(string AccessToken, string RefreshToken, DateTime ExpiresAt)>
        ExchangeCodeAsync(string code, string redirectUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            "https://api.dropboxapi.com/oauth2/token");

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"]          = code,
            ["grant_type"]    = "authorization_code",
            ["client_id"]     = dropboxSettings.AppKey,
            ["client_secret"] = dropboxSettings.AppSecret,
            ["redirect_uri"]  = redirectUri
        });

        var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var accessToken  = doc.RootElement.GetProperty("access_token").GetString()!;
        var refreshToken = doc.RootElement.GetProperty("refresh_token").GetString()!;
        var expiresIn    = doc.RootElement.GetProperty("expires_in").GetInt32();
        var expiresAt    = DateTime.UtcNow.AddSeconds(expiresIn);

        return (accessToken, refreshToken, expiresAt);
    }
}

