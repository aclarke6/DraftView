using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ScrivenerSync.Domain.Interfaces.Repositories;
using ScrivenerSync.Domain.Interfaces.Services;
using ScrivenerSync.Web.Models;

namespace ScrivenerSync.Web.Controllers;

public class AccountController(
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IUserService userService,
    IInvitationRepository invitationRepo,
    IUserRepository userRepo,
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

        if (result.Succeeded)
        {
            logger.LogInformation("User logged in: {Email}", model.Email);
            return RedirectToLocal(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Account locked out. Please try again later.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
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

    public IActionResult AccessDenied() => View();

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------
    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Dashboard", "Author");
    }
}
