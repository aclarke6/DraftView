using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using DraftView.Infrastructure.Persistence;
using DraftView.Web.Models;

namespace DraftView.Web.Controllers;

#pragma warning disable CS9107
public class AccountController(
    DraftViewDbContext db,
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IUserService userService,
    DraftView.Application.Interfaces.IAuthenticationUserLookupService authenticationUserLookupService,
    IInvitationRepository invitationRepo,
    IUserRepository userRepo,
    IUserPreferencesRepository prefsRepo,
    DraftView.Application.Interfaces.IControlledUserEmailService controlledUserEmailService,
    IEmailSender emailSender,
    ILogger<AccountController> logger) : BaseController(userRepo)
{
    // ---------------------------------------------------------------------------
    // Login
    // ---------------------------------------------------------------------------
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var result = await signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        switch (result)
        {
            case { Succeeded: true }:
                logger.LogInformation("User login succeeded.");
                if (Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                try
                {
                    var domainUser = await authenticationUserLookupService.FindByLoginEmailAsync(model.Email);
                    if (domainUser?.Role == Domain.Enumerations.Role.Author)
                        return RedirectToAction("Dashboard", "Author");
                    if (domainUser?.Role == Domain.Enumerations.Role.SystemSupport)
                        return RedirectToAction("Dashboard", "Support");
                    return RedirectToAction("Dashboard", "Reader");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to resolve role redirect after successful login.");
                    return RedirectToAction("Index", "Home");
                }

            case { IsLockedOut: true }:
                ModelState.AddModelError(string.Empty, "Account locked out. Please try again later.");
                return View(model);

            default:
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
        }
    }

    // ---------------------------------------------------------------------------
    // Logout
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    // ---------------------------------------------------------------------------
    // Accept invitation - GET
    // ---------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> AcceptInvitation(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return RedirectToAction("InvitationInvalid",
                new { reason = "No invitation token was provided." });

        var invitation = await invitationRepo.GetByTokenAsync(token);

        if (invitation is null)
            return RedirectToAction("InvitationInvalid",
                new { reason = "This invitation link is not recognised." });

        if (!invitation.IsValid())
            return RedirectToAction("InvitationInvalid",
                new { reason = invitation.Status == Domain.Enumerations.InvitationStatus.Expired
                    ? "This invitation has expired. Please ask the author to send a new one."
                    : "This invitation has been cancelled. Please ask the author to send a new one." });

        // Find the user this invitation is for
        var user = await userRepo.GetByIdAsync(invitation.UserId);
        if (user is null)
            return RedirectToAction("InvitationInvalid",
                new { reason = "Invitation account not found." });

        var vm = new AcceptInvitationViewModel
        {
            Token = token
        };

        return View(vm);
    }

    // ---------------------------------------------------------------------------
    // Accept invitation - POST
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptInvitation(AcceptInvitationViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var invitation = await invitationRepo.GetByTokenAsync(model.Token);
            if (invitation is null)
                return RedirectToAction("InvitationInvalid",
                    new { reason = "This invitation link is not recognised." });

            if (!invitation.IsValid())
                return RedirectToAction("InvitationInvalid",
                    new { reason = invitation.Status == Domain.Enumerations.InvitationStatus.Expired
                        ? "This invitation has expired. Please ask the author to send a new one."
                        : "This invitation has been cancelled. Please ask the author to send a new one." });

            var user = await userRepo.GetByIdAsync(invitation.UserId);
            if (user is null)
                return RedirectToAction("InvitationInvalid",
                    new { reason = "Invitation account not found." });

            var inviteeEmail = user.Email;

            // Ensure Identity user exists FIRST
            var existingIdentity = await userManager.FindByEmailAsync(inviteeEmail);
            if (existingIdentity is null)
            {
                var identityUser = new IdentityUser
                {
                    UserName       = inviteeEmail,
                    Email          = inviteeEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(identityUser, model.Password);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return View(model);
                }
            }

            // Now activate the domain user + invitation
            await userService.AcceptInvitationAsync(
                model.Token, model.DisplayName);

            // Sign them in immediately
            await signInManager.PasswordSignInAsync(
                inviteeEmail, model.Password, isPersistent: false, lockoutOnFailure: false);

            logger.LogInformation("Invitation accepted and user signed in for user {UserId}", user.Id);

            return RedirectToAction("Dashboard", "Reader");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to accept invitation for token {Token}", model.Token);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    // ---------------------------------------------------------------------------
    // Invalid / expired invitation
    // ---------------------------------------------------------------------------
    [HttpGet]
    public IActionResult InvitationInvalid(string reason = "This invitation is no longer valid.")
    {
        return View(new InvitationExpiredViewModel { Reason = reason });
    }

    // ---------------------------------------------------------------------------
    // Forgot password
    // ---------------------------------------------------------------------------
    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        // Always show the same message regardless of whether email exists
        if (!ModelState.IsValid)
            return View(model);

        var user = await userRepo.GetByEmailAsync(model.Email);
        if (user is not null && user.IsActive && !user.IsSoftDeleted)
        {
            // Generate reset token
            var resetToken = DraftView.Domain.Entities.PasswordResetToken.Create(user.Id);
            db.PasswordResetTokens.Add(resetToken);
            await db.SaveChangesAsync();

            // Ensure Identity user exists (may be missing if invitation flow had issues)
            var existingIdentity = await userManager.FindByEmailAsync(model.Email);
            if (existingIdentity is null)
            {
                var identityUser = new IdentityUser {
                    UserName = model.Email,
                    Email = model.Email,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(identityUser);
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var resetLink = $"{baseUrl}/Account/ResetPassword?token={resetToken.Token}";
            var body = $"""
                <p>You requested a password reset for your DraftView account.</p>
                <p><a href="{resetLink}">Click here to reset your password</a></p>
                <p>Or copy this link: {resetLink}</p>
                <p>This link expires in 24 hours and can only be used once.</p>
                <p>If you did not request this, you can ignore this email.</p>
                """;

            try
            {

                await emailSender.SendAsync(
                    model.Email,
                    user.DisplayName ?? "Reader",
                    "Reset your DraftView password",
                    body);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send password reset email for user {UserId}", user.Id);
                // Don't reveal email sending failure to user - just log it
            }
        }

        // Always redirect to confirmation - never reveal if email exists
        return RedirectToAction("ForgotPasswordConfirmation");
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    // ---------------------------------------------------------------------------
    // Reset password
    // ---------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> ResetPassword(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return RedirectToAction("Login");

        var resetToken = await db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.Token == token.Replace(" ", "+"));

        if (resetToken is null || !resetToken.IsValid())
            return RedirectToAction("ResetPasswordInvalid");

        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var resetToken = await db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.Token == model.Token.Replace(" ", "+"));

        if (resetToken is null || !resetToken.IsValid())
            return RedirectToAction("ResetPasswordInvalid");

        // Update Identity password
        var identityUser = await userManager.FindByIdAsync(resetToken.UserId.ToString());
        if (identityUser is null)
            return RedirectToAction("ResetPasswordInvalid");

        var token = await userManager.GeneratePasswordResetTokenAsync(identityUser);
        var result = await userManager.ResetPasswordAsync(identityUser, token, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                logger.LogWarning("Password reset error: {Code} - {Description}", error.Code, error.Description);
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        // Mark token as used
        resetToken.MarkUsed();
        await db.SaveChangesAsync();

        logger.LogInformation("Password reset completed for user {UserId}", resetToken.UserId);
        TempData["Success"] = "Password reset successfully. Please sign in.";
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation() => View();

    [HttpGet]
    public IActionResult ResetPasswordInvalid() => View();

    [HttpGet]
    public IActionResult AccessDenied()
    {
        var model = new AccessDeniedViewModel {
            Heading = "Access denied",
            Message = "You do not have permission to view that page.",
            ActionText = "Go home",
            ActionController = "Home",
            ActionAction = "Index"
        };

        if (User.Identity?.IsAuthenticated != true)
        {
            model = new AccessDeniedViewModel {
                Heading = "Sign in required",
                Message = "You need to sign in to view that page.",
                ActionText = "Go to sign in",
                ActionController = "Account",
                ActionAction = "Login"
            };
        }
        else if (User.IsInRole("SystemSupport"))
        {
            model = new AccessDeniedViewModel {
                Heading = "Access denied",
                Message = "That page is not available in the System Support role.",
                ActionText = "Go to System Dashboard",
                ActionController = "Support",
                ActionAction = "Dashboard"
            };
        }
        else if (User.IsInRole("Author"))
        {
            model = new AccessDeniedViewModel {
                Heading = "Access denied",
                Message = "That page is not available in the Author role.",
                ActionText = "Go to Author Dashboard",
                ActionController = "Author",
                ActionAction = "Dashboard"
            };
        }
        else
        {
            model = new AccessDeniedViewModel {
                Heading = "Access denied",
                Message = "That page is not available in the Reader role.",
                ActionText = "Go to My Reading",
                ActionController = "Reader",
                ActionAction = "Dashboard"
            };
        }

        return View(model);
    }

    // ---------------------------------------------------------------------------
    // Settings
    // ---------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return RedirectToAction("Login");

        var prefs = await prefsRepo.GetByUserIdAsync(user.Id);
        var resolvedEmail = await controlledUserEmailService.GetEmailAsync(new DraftView.Application.Contracts.UserEmailAccessRequest(
            user.Id,
            user.Role,
            user.Id,
            DraftView.Application.Contracts.UserEmailAccessPurpose.SelfServiceSettings));

        var vm = new SettingsViewModel {
            DisplayName = user.DisplayName,
            Email = resolvedEmail,
            IsAuthor = User.IsInRole("Author"),
            IsReader = User.IsInRole("Reader"),
            DisplayTheme = prefs?.DisplayTheme.ToString() ?? "Light",
            ProseFont = prefs?.ProseFont.ToString() ?? "SystemSerif",
            ProseFontSize = prefs?.ProseFontSize.ToString() ?? "Medium"
        };

        if (vm.IsAuthor)
        {
            var connection = await GetDropboxConnectionAsync(user.Id);
            vm.DropboxStatus = connection?.Status.ToString();
            vm.DropboxAuthorisedAt = connection?.AuthorisedAt;
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeDisplayTheme(ChangeDisplayThemeViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return RedirectToAction("Login");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please select a valid theme.";
            return RedirectToAction("Settings");
        }

        if (!Enum.TryParse<DraftView.Domain.Enumerations.DisplayTheme>(model.DisplayTheme, true, out var displayTheme))
        {
            TempData["Error"] = "Please select a valid theme.";
            return RedirectToAction("Settings");
        }

        try
        {
            await userService.UpdateDisplayThemeAsync(user.Id, displayTheme);
            TempData["Success"] = "Theme updated.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeProseFontPreferences(ChangeProseFontPreferencesViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return RedirectToAction("Login");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please select valid prose preferences.";
            return RedirectToAction("Settings");
        }

        if (!Enum.TryParse<DraftView.Domain.Enumerations.ProseFont>(model.ProseFont, true, out var proseFont) ||
            !Enum.TryParse<DraftView.Domain.Enumerations.ProseFontSize>(model.ProseFontSize, true, out var proseFontSize))
        {
            TempData["Error"] = "Invalid prose font selection.";
            return RedirectToAction("Settings");
        }

        try
        {
            await userService.UpdateProseFontPreferencesAsync(user.Id, proseFont, proseFontSize);
            TempData["Success"] = "Reading preferences updated.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeDisplayName(ChangeDisplayNameViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return RedirectToAction("Login");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Display name is invalid.";
            return RedirectToAction("Settings");
        }

        try
        {
            await userService.UpdateDisplayNameAsync(user.Id, model.DisplayName);
            TempData["Success"] = "Display name updated.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeEmail(ChangeEmailViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return RedirectToAction("Login");

        var currentStoredEmail = await controlledUserEmailService.GetEmailAsync(new DraftView.Application.Contracts.UserEmailAccessRequest(
            user.Id,
            user.Role,
            user.Id,
            DraftView.Application.Contracts.UserEmailAccessPurpose.SelfServiceSettings));

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please correct the errors and try again.";
            return RedirectToAction("Settings");
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            await userManager.FindByEmailAsync(currentStoredEmail) ?? new IdentityUser(),
            model.CurrentPassword, false);

        if (!signInResult.Succeeded)
        {
            TempData["Error"] = "Current password is incorrect.";
            return RedirectToAction("Settings");
        }

        try
        {
            await userService.UpdateEmailAsync(user.Id, model.Email);
            var identityUser = await userManager.FindByEmailAsync(currentStoredEmail);
            if (identityUser is not null)
            {
                await userManager.SetEmailAsync(identityUser, model.Email);
                await userManager.SetUserNameAsync(identityUser, model.Email);
            }
            await signInManager.SignOutAsync();
            TempData["Success"] = "Email updated. Please sign in with your new email.";
            return RedirectToAction("Login");
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Settings");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return RedirectToAction("Login");

        var currentStoredEmail = await controlledUserEmailService.GetEmailAsync(new DraftView.Application.Contracts.UserEmailAccessRequest(
            user.Id,
            user.Role,
            user.Id,
            DraftView.Application.Contracts.UserEmailAccessPurpose.SelfServiceSettings));

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please correct the errors and try again.";
            return RedirectToAction("Settings");
        }

        var identityUser = await userManager.FindByEmailAsync(currentStoredEmail);
        if (identityUser is null) return RedirectToAction("Login");

        var result = await userManager.ChangePasswordAsync(
            identityUser, model.CurrentPassword, model.NewPassword);

        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction("Settings");
        }

        await signInManager.RefreshSignInAsync(identityUser);
        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction("Settings");
    }

    private async Task<DraftView.Domain.Entities.DropboxConnection?> GetDropboxConnectionAsync(Guid userId)
    {
        var connectionRepo = HttpContext.RequestServices
            .GetRequiredService<DraftView.Domain.Interfaces.Repositories.IDropboxConnectionRepository>();
        return await connectionRepo.GetByUserIdAsync(userId);
    }
}






