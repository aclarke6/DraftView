using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Infrastructure.Persistence;
using DraftView.Web.Controllers;
using DraftView.Web.Models;
using DraftView.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DraftView.Web.Tests.Controllers;

public class AccountControllerTests
{
    private readonly Mock<DraftViewDbContext> db;
    private readonly Mock<UserManager<IdentityUser>> userManager;
    private readonly Mock<SignInManager<IdentityUser>> signInManager;
    private readonly Mock<IUserService> userService = new();
    private readonly Mock<IInvitationRepository> invitationRepo = new();
    private readonly Mock<IUserRepository> userRepo = new();
    private readonly Mock<IUserPreferencesRepository> prefsRepo = new();
    private readonly Mock<IEmailSender> emailSender = new();
    private readonly Mock<ILogger<AccountController>> logger = new();
    private readonly Mock<IUserEmailEncryptionService> emailEncryptionService = new();
    private readonly Mock<IUserEmailLookupHmacService> emailLookupHmacService = new();

    public AccountControllerTests()
    {
        var dbOptions = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<DraftViewDbContext>()
            .Options;

        db = new Mock<DraftViewDbContext>(
            dbOptions,
            emailEncryptionService.Object,
            emailLookupHmacService.Object);

        var userStore = new Mock<IUserStore<IdentityUser>>();
        userManager = new Mock<UserManager<IdentityUser>>(
            userStore.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var httpContextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var claimsFactory = new Mock<Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory<IdentityUser>>();

        signInManager = new Mock<SignInManager<IdentityUser>>(
            userManager.Object,
            httpContextAccessor.Object,
            claimsFactory.Object,
            null!,
            null!,
            null!,
            null!);
    }

    private AccountController CreateSut(System.Security.Claims.ClaimsPrincipal? user = null)
    {
        var controller = new AccountController(
            db.Object,
            signInManager.Object,
            userManager.Object,
            userService.Object,
            invitationRepo.Object,
            userRepo.Object,
            prefsRepo.Object,
            emailSender.Object,
            logger.Object
        );

        controller.ControllerContext = new ControllerContext {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext {
                User = user ?? new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity())
            }
        };

        controller.TempData = new Mock<ITempDataDictionary>().Object;

        return controller;
    }

    private static System.Security.Claims.ClaimsPrincipal AuthenticatedUser(string email = "test@test.com")
    {
        return new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, email)],
                "TestAuth"));
    }

    // ---------------------------------------------------------------------------
    // Settings
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Settings_UserNotAuthenticated_RedirectsToLogin()
    {
        var sut = CreateSut();

        var result = await sut.Settings();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }

    // ---------------------------------------------------------------------------
    // ChangeDisplayTheme
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ChangeDisplayTheme_UnauthenticatedUser_RedirectsToLogin()
    {
        var sut = CreateSut();
        sut.ModelState.AddModelError("DisplayTheme", "Invalid");

        var result = await sut.ChangeDisplayTheme(new ChangeDisplayThemeViewModel());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }

    [Fact]
    public async Task ChangeDisplayTheme_InvalidEnum_ForAuthenticatedUser_RedirectsToSettings()
    {
        var user = Domain.Entities.User.Create("test@test.com", "Test", Domain.Enumerations.Role.BetaReader);
        var sut = CreateSut(AuthenticatedUser("test@test.com"));

        userRepo.Setup(r => r.GetByEmailAsync("test@test.com"))
            .ReturnsAsync(user);

        var model = new ChangeDisplayThemeViewModel {
            DisplayTheme = "InvalidTheme"
        };

        var result = await sut.ChangeDisplayTheme(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Settings", redirect.ActionName);
    }

    [Fact]
    public async Task ChangeDisplayTheme_InvalidModel_ForAuthenticatedUser_RedirectsToSettings()
    {
        var user = Domain.Entities.User.Create("test@test.com", "Test", Domain.Enumerations.Role.BetaReader);
        var sut = CreateSut(AuthenticatedUser("test@test.com"));
        sut.ModelState.AddModelError("DisplayTheme", "Invalid");

        userRepo.Setup(r => r.GetByEmailAsync("test@test.com"))
            .ReturnsAsync(user);

        var result = await sut.ChangeDisplayTheme(new ChangeDisplayThemeViewModel());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Settings", redirect.ActionName);
    }

    [Fact]
    public async Task ChangeDisplayName_InvalidModel_ForAuthenticatedUser_RedirectsToSettings()
    {
        var user = Domain.Entities.User.Create("test@test.com", "Test", Domain.Enumerations.Role.BetaReader);
        var sut = CreateSut(AuthenticatedUser("test@test.com"));
        sut.ModelState.AddModelError("DisplayName", "Invalid");

        userRepo.Setup(r => r.GetByEmailAsync("test@test.com"))
            .ReturnsAsync(user);

        var result = await sut.ChangeDisplayName(new ChangeDisplayNameViewModel());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Settings", redirect.ActionName);
    }


    // ---------------------------------------------------------------------------
    // ChangeDisplayName
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ChangeDisplayName_UnauthenticatedUser_RedirectsToLogin()
    {
        var sut = CreateSut();
        sut.ModelState.AddModelError("DisplayName", "Invalid");

        var result = await sut.ChangeDisplayName(new ChangeDisplayNameViewModel());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }

    [Fact]
    public async Task AcceptInvitation_Post_UsesInvitationUserEmail_ForIdentityCreationAndSignIn()
    {
        var sut = CreateSut();
        var invitedUser = User.Create("reader@example.test", "Pending", Role.BetaReader);
        var invitation = Invitation.CreateAlwaysOpen(invitedUser.Id);

        invitationRepo.Setup(r => r.GetByTokenAsync(invitation.Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        userRepo.Setup(r => r.GetByIdAsync(invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitedUser);
        userManager.Setup(m => m.FindByEmailAsync(invitedUser.Email))
            .ReturnsAsync((IdentityUser?)null);
        userManager.Setup(m => m.CreateAsync(
                It.Is<IdentityUser>(u => u.Email == invitedUser.Email && u.UserName == invitedUser.Email),
                "Password1!"))
            .ReturnsAsync(IdentityResult.Success);
        userService.Setup(s => s.AcceptInvitationAsync(invitation.Token, "Reader Name", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitedUser);
        signInManager.Setup(m => m.PasswordSignInAsync(invitedUser.Email, "Password1!", false, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var result = await sut.AcceptInvitation(new AcceptInvitationViewModel
        {
            Token = invitation.Token,
            DisplayName = "Reader Name",
            Password = "Password1!",
            ConfirmPassword = "Password1!"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirect.ActionName);
        Assert.Equal("Reader", redirect.ControllerName);
        userManager.Verify(m => m.FindByEmailAsync(invitedUser.Email), Times.Once);
        signInManager.Verify(m => m.PasswordSignInAsync(invitedUser.Email, "Password1!", false, false), Times.Once);
    }

    [Fact]
    public async Task AcceptInvitation_Post_InvalidToken_RedirectsToInvitationInvalid()
    {
        var sut = CreateSut();

        invitationRepo.Setup(r => r.GetByTokenAsync("bad-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invitation?)null);

        var result = await sut.AcceptInvitation(new AcceptInvitationViewModel
        {
            Token = "bad-token",
            DisplayName = "Reader Name",
            Password = "Password1!",
            ConfirmPassword = "Password1!"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("InvitationInvalid", redirect.ActionName);
    }


}
