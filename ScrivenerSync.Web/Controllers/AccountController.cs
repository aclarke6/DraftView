using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ScrivenerSync.Domain.Interfaces.Repositories;
using ScrivenerSync.Domain.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using ScrivenerSync.Infrastructure.Persistence;
using ScrivenerSync.Web.Models;

namespace ScrivenerSync.Web.Controllers;

public class AccountController(
    ScrivenerSyncDbContext db,
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IUserService userService,
    IInvitationRepository invitationRepo,
    IUserRepository userRepo,
    IEmailSender emailSender,
    ILogger<AccountController> logger) : Controller
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
                logger.LogInformation("User logged in: {Email}", model.Email);
                if (Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                var domainUser = await userRepo.GetByEmailAsync(model.Email);
                return domainUser?.Role switch {
                    Domain.Enumerations.Role.Author => RedirectToAction("Dashboard", "Author"),
                    _ => RedirectToAction("Index", "Reader")
                };

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
            Token = token,
            Email = user.Email
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
            // Activate the domain user
            await userService.AcceptInvitationAsync(
                model.Token, model.DisplayName, model.Password);

            // Create the Identity user so they can log in
            var domainUser = await userRepo.GetByEmailAsync(model.Email);
            if (domainUser is null)
                throw new InvalidOperationException("User not found after activation.");

            var existingIdentity = await userManager.FindByEmailAsync(model.Email);
            if (existingIdentity is null)
            {
                var identityUser = new IdentityUser
                {
                    UserName       = model.Email,
                    Email          = model.Email,
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

            // Sign them in immediately
            await signInManager.PasswordSignInAsync(
                model.Email, model.Password, isPersistent: false, lockoutOnFailure: false);

            logger.LogInformation("Invitation accepted and user signed in: {Email}", model.Email);

            return RedirectToAction("Index", "Reader");
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
            var resetToken = ScrivenerSync.Domain.Entities.PasswordResetToken.Create(model.Email);
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
                <p>You requested a password reset for your ScrivenerSync account.</p>
                <p><a href="{resetLink}">Click here to reset your password</a></p>
                <p>Or copy this link: {resetLink}</p>
                <p>This link expires in 24 hours and can only be used once.</p>
                <p>If you did not request this, you can ignore this email.</p>
                """;

            await emailSender.SendAsync(
                model.Email,
                user.DisplayName ?? "Reader",
                "Reset your ScrivenerSync password",
                body);
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
        logger.LogWarning("Reset POST token: '{Token}'", model.Token);
        if (!ModelState.IsValid)
            return View(model);

        var resetToken = await db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.Token == model.Token.Replace(" ", "+"));

        logger.LogWarning("Token found: {Found}, IsUsed={IsUsed}, ExpiresAt={ExpiresAt}, Now={Now}",
    resetToken is not null, resetToken?.IsUsed, resetToken?.ExpiresAt, DateTime.UtcNow);


        if (resetToken is null || !resetToken.IsValid())
            return RedirectToAction("ResetPasswordInvalid");

        // Update Identity password
        var identityUser = await userManager.FindByEmailAsync(resetToken.Email);
        logger.LogWarning("Identity user lookup for {Email}: {Found}", resetToken.Email, identityUser is not null);
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

        logger.LogInformation("Password reset completed for {Email}", resetToken.Email);
        TempData["Success"] = "Password reset successfully. Please sign in.";
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation() => View();

    [HttpGet]
    public IActionResult ResetPasswordInvalid() => View();

    public IActionResult AccessDenied() => View();

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------
    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        var email = HttpContext.User.Identity?.Name ?? string.Empty;
        var domainUser = userRepo.GetByEmailAsync(email).GetAwaiter().GetResult();

        if (domainUser?.Role == ScrivenerSync.Domain.Enumerations.Role.Author)
            return RedirectToAction("Dashboard", "Author");

        return RedirectToAction("Index", "Reader");
    }
}


