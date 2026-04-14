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
    private readonly Mock<IAuthenticationUserLookupService> authenticationUserLookupService = new();
    private readonly Mock<IInvitationRepository> invitationRepo = new();
    private readonly Mock<IUserRepository> userRepo = new();
    private readonly Mock<IUserPreferencesRepository> prefsRepo = new();
    private readonly Mock<IControlledUserEmailService> controlledUserEmailService = new();
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
            authenticationUserLookupService.Object,
            invitationRepo.Object,
            userRepo.Object,
            prefsRepo.Object,
            controlledUserEmailService.Object,
            emailSender.Object,
            logger.Object
        );

        controller.ControllerContext = new ControllerContext {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext {
                User = user ?? new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity())
            }
        };

        var urlHelper = new Mock<Microsoft.AspNetCore.Mvc.IUrlHelper>();
        urlHelper.Setup(u => u.IsLocalUrl(It.IsAny<string?>())).Returns(false);
        controller.Url = urlHelper.Object;
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
    // Login
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Login_ValidAuthorCredentials_UsesAuthenticationLookupSeamForRoleRedirect()
    {
        var author = Domain.Entities.User.Create("author@example.test", "Author", Domain.Enumerations.Role.Author);
        var sut = CreateSut();

        signInManager.Setup(m => m.PasswordSignInAsync("author@example.test", "Password1!", false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        authenticationUserLookupService
            .Setup(s => s.FindByLoginEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        var result = await sut.Login(new LoginViewModel
        {
            Email = "author@example.test",
            Password = "Password1!",
            RememberMe = false
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirect.ActionName);
        Assert.Equal("Author", redirect.ControllerName);
        authenticationUserLookupService.Verify(
            s => s.FindByLoginEmailAsync("author@example.test", It.IsAny<CancellationToken>()),
            Times.Once);
        userRepo.Verify(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task Settings_AuthenticatedUser_UsesControlledEmailServiceForViewModelEmail()
    {
        var user = Domain.Entities.User.Create("test@test.com", "Test", Domain.Enumerations.Role.BetaReader);
        var sut = CreateSut(AuthenticatedUser("test@test.com"));

        userRepo.Setup(r => r.GetByEmailAsync("test@test.com"))
            .ReturnsAsync(user);
        controlledUserEmailService
            .Setup(s => s.GetEmailAsync(
                It.Is<DraftView.Application.Contracts.UserEmailAccessRequest>(r =>
                    r.RequestingUserId == user.Id &&
                    r.TargetUserId == user.Id &&
                    r.RequestingUserRole == Domain.Enumerations.Role.BetaReader &&
                    r.Purpose == DraftView.Application.Contracts.UserEmailAccessPurpose.SelfServiceSettings),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("resolved@example.test");

        var result = await sut.Settings();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SettingsViewModel>(view.Model);
        Assert.Equal("resolved@example.test", model.Email);
    }

    [Fact]
    public async Task Settings_AuthenticatedUser_DoesNotFallbackToDirectUserEmail()
    {
        var user = Domain.Entities.User.Create("direct@example.test", "Test", Domain.Enumerations.Role.BetaReader);
        var sut = CreateSut(AuthenticatedUser("direct@example.test"));

        userRepo.Setup(r => r.GetByEmailAsync("direct@example.test"))
            .ReturnsAsync(user);
        controlledUserEmailService
            .Setup(s => s.GetEmailAsync(It.IsAny<DraftView.Application.Contracts.UserEmailAccessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("resolved@example.test");

        var result = await sut.Settings();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SettingsViewModel>(view.Model);
        Assert.Equal("resolved@example.test", model.Email);
        Assert.NotEqual(user.Email, model.Email);
    }

    [Fact]
    public async Task ChangeEmail_AuthenticatedUser_UsesControlledEmailServiceForCurrentStoredEmail()
    {
        var user = Domain.Entities.User.Create("claim@example.test", "Test", Domain.Enumerations.Role.BetaReader);
        var identityUser = new IdentityUser { Email = "stored@example.test", UserName = "stored@example.test" };
        var sut = CreateSut(AuthenticatedUser("claim@example.test"));

        userRepo.Setup(r => r.GetByEmailAsync("claim@example.test"))
            .ReturnsAsync(user);
        controlledUserEmailService
            .Setup(s => s.GetEmailAsync(
                It.Is<DraftView.Application.Contracts.UserEmailAccessRequest>(r =>
                    r.RequestingUserId == user.Id &&
                    r.TargetUserId == user.Id &&
                    r.RequestingUserRole == Domain.Enumerations.Role.BetaReader &&
                    r.Purpose == DraftView.Application.Contracts.UserEmailAccessPurpose.SelfServiceSettings),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("stored@example.test");
        userManager.Setup(m => m.FindByEmailAsync("stored@example.test"))
            .ReturnsAsync(identityUser);
        signInManager.Setup(m => m.CheckPasswordSignInAsync(identityUser, "CurrentPassword1!", false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        userManager.Setup(m => m.SetEmailAsync(identityUser, "new@example.test"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.SetUserNameAsync(identityUser, "new@example.test"))
            .ReturnsAsync(IdentityResult.Success);

        var result = await sut.ChangeEmail(new ChangeEmailViewModel
        {
            Email = "new@example.test",
            CurrentPassword = "CurrentPassword1!"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
        userManager.Verify(m => m.FindByEmailAsync("stored@example.test"), Times.Exactly(2));
        userManager.Verify(m => m.FindByEmailAsync("claim@example.test"), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_AuthenticatedUser_UsesControlledEmailServiceForCurrentStoredEmail()
    {
        var user = Domain.Entities.User.Create("claim@example.test", "Test", Domain.Enumerations.Role.BetaReader);
        var identityUser = new IdentityUser { Email = "stored@example.test", UserName = "stored@example.test" };
        var sut = CreateSut(AuthenticatedUser("claim@example.test"));

        userRepo.Setup(r => r.GetByEmailAsync("claim@example.test"))
            .ReturnsAsync(user);
        controlledUserEmailService
            .Setup(s => s.GetEmailAsync(
                It.Is<DraftView.Application.Contracts.UserEmailAccessRequest>(r =>
                    r.RequestingUserId == user.Id &&
                    r.TargetUserId == user.Id &&
                    r.RequestingUserRole == Domain.Enumerations.Role.BetaReader &&
                    r.Purpose == DraftView.Application.Contracts.UserEmailAccessPurpose.SelfServiceSettings),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("stored@example.test");
        userManager.Setup(m => m.FindByEmailAsync("stored@example.test"))
            .ReturnsAsync(identityUser);
        userManager.Setup(m => m.ChangePasswordAsync(identityUser, "CurrentPassword1!", "NewPassword1!"))
            .ReturnsAsync(IdentityResult.Success);

        var result = await sut.ChangePassword(new ChangePasswordViewModel
        {
            CurrentPassword = "CurrentPassword1!",
            NewPassword = "NewPassword1!",
            ConfirmPassword = "NewPassword1!"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Settings", redirect.ActionName);
        userManager.Verify(m => m.FindByEmailAsync("stored@example.test"), Times.Once);
        userManager.Verify(m => m.FindByEmailAsync("claim@example.test"), Times.Never);
        signInManager.Verify(m => m.RefreshSignInAsync(identityUser), Times.Once);
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
