using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Web;

namespace DraftView.Web.Controllers;

[AllowAnonymous]
public class OnboardingController(
    IAuthorSelfRegistrationService registrationService,
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    IUserRepository userRepo,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    ILogger<OnboardingController> logger) : Controller
{
    // ---------------------------------------------------------------------------
    // GET /Join  — marketing landing page + registration form
    // ---------------------------------------------------------------------------

    [HttpGet("/Join")]
    public IActionResult Join()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new RegisterAuthorViewModel());
    }

    // ---------------------------------------------------------------------------
    // POST /Join  — process registration
    // ---------------------------------------------------------------------------

    [HttpPost("/Join")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(RegisterAuthorViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Check whether identity email is already taken (before creating domain entities)
        var existingIdentity = await userManager.FindByEmailAsync(model.Email);
        if (existingIdentity is not null)
        {
            ModelState.AddModelError(nameof(model.Email),
                "An account with this email address already exists.");
            return View(model);
        }

        try
        {
            // Create domain entities atomically
            var registration = await registrationService.RegisterAsync(
                model.Email, model.DisplayName, model.TenancyName, ct);

            // Create ASP.NET Identity record (unconfirmed)
            var identityUser = new IdentityUser
            {
                Id             = registration.User.Id.ToString(),
                UserName       = model.Email,
                Email          = model.Email,
                EmailConfirmed = false
            };

            var createResult = await userManager.CreateAsync(identityUser);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            await userManager.AddToRoleAsync(identityUser, "Author");

            // Generate and send confirmation email
            var token     = await userManager.GenerateEmailConfirmationTokenAsync(identityUser);
            var encoded   = HttpUtility.UrlEncode(token);
            var baseUrl   = $"{Request.Scheme}://{Request.Host}";
            var link      = $"{baseUrl}/Join/ConfirmEmail?userId={identityUser.Id}&token={encoded}";
            var body      = $"""
                <p>Welcome to DraftView, {model.DisplayName}!</p>
                <p>Please confirm your email address to get started:</p>
                <p><a href="{link}">Confirm my email</a></p>
                <p>Or copy this link: {link}</p>
                <p>If you did not sign up for DraftView, you can ignore this email.</p>
                """;

            try
            {
                await emailSender.SendAsync(model.Email, model.DisplayName,
                    "Confirm your DraftView account", body, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send confirmation email to {Email}", model.Email);
            }

            return RedirectToAction(nameof(EmailSent));
        }
        catch (InvariantViolationException ex) when (ex.InvariantCode == "I-SELF-REG-EMAIL-EXISTS")
        {
            ModelState.AddModelError(nameof(model.Email),
                "An account with this email address already exists.");
            return View(model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Author self-registration failed for {Email}", model.Email);
            ModelState.AddModelError(string.Empty,
                "Something went wrong. Please try again.");
            return View(model);
        }
    }

    // ---------------------------------------------------------------------------
    // GET /Join/EmailSent
    // ---------------------------------------------------------------------------

    [HttpGet("/Join/EmailSent")]
    public IActionResult EmailSent() => View();

    // ---------------------------------------------------------------------------
    // GET /Join/ConfirmEmail
    // ---------------------------------------------------------------------------

    [HttpGet("/Join/ConfirmEmail")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            return View("ConfirmEmailResult", new ConfirmEmailResultViewModel
            {
                Success = false,
                Message = "The confirmation link is invalid or has expired."
            });

        var identityUser = await userManager.FindByIdAsync(userId);
        if (identityUser is null)
            return View("ConfirmEmailResult", new ConfirmEmailResultViewModel
            {
                Success = false,
                Message = "The confirmation link is invalid or has expired."
            });

        var decoded       = HttpUtility.UrlDecode(token);
        var confirmResult = await userManager.ConfirmEmailAsync(identityUser, decoded);
        if (!confirmResult.Succeeded)
            return View("ConfirmEmailResult", new ConfirmEmailResultViewModel
            {
                Success = false,
                Message = "The confirmation link is invalid or has expired."
            });

        // Activate domain User
        var domainUser = await userRepo.GetByEmailAsync(identityUser.Email!, ct);
        if (domainUser is not null && !domainUser.IsActive)
        {
            domainUser.Activate();
            await unitOfWork.SaveChangesAsync(ct);
        }

        // Sign in the new author
        await signInManager.SignInAsync(identityUser, isPersistent: false);

        return View("ConfirmEmailResult", new ConfirmEmailResultViewModel
        {
            Success  = true,
            Message  = "Your email has been confirmed. Welcome to DraftView!",
            Redirect = Url.Action("FirstProject", "Onboarding")
        });
    }

    // ---------------------------------------------------------------------------
    // GET /Join/FirstProject — welcome page for newly confirmed authors
    // ---------------------------------------------------------------------------

    [HttpGet("/Join/FirstProject")]
    [Authorize(Policy = "RequireAuthorPolicy")]
    public IActionResult FirstProject() => View();

    // ---------------------------------------------------------------------------
    // GET /Join/FAQ
    // ---------------------------------------------------------------------------

    [HttpGet("/Join/FAQ")]
    public IActionResult FAQ() => View();
}
