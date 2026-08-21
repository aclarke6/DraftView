using Moq;
using DraftView.Application.Services;
using DraftView.Application.Interfaces;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

public class AuthorSelfRegistrationServiceTests
{
    private readonly Mock<IUserRepository>                _userRepo         = new();
    private readonly Mock<IAccountRepository>             _accountRepo      = new();
    private readonly Mock<ITenancyRepository>             _tenancyRepo      = new();
    private readonly Mock<ITenancyMembershipRepository>   _membershipRepo   = new();
    private readonly Mock<ITenancySubscriptionRepository> _subscriptionRepo = new();
    private readonly Mock<IUserEmailLookupHmacService>    _hmacService      = new();
    private readonly Mock<IUnitOfWork>                    _unitOfWork       = new();

    private AuthorSelfRegistrationService CreateSut() => new(
        _userRepo.Object,
        _accountRepo.Object,
        _tenancyRepo.Object,
        _membershipRepo.Object,
        _subscriptionRepo.Object,
        _hmacService.Object,
        _unitOfWork.Object);

    [Fact]
    public async Task RegisterAsync_WithValidInputs_CreatesAllEntitiesAndSavesOnce()
    {
        const string email       = "author@example.com";
        const string displayName = "Alice";
        const string tenancyName = "Alice's Workspace";
        const string hmac        = "FAKE-HMAC";

        _hmacService.Setup(s => s.Compute(It.IsAny<string>())).Returns(hmac);
        _accountRepo.Setup(r => r.EmailExistsAsync(hmac, default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.EmailExistsAsync(email, default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), default)).Returns(Task.CompletedTask);
        _accountRepo.Setup(r => r.AddAsync(It.IsAny<Account>(), default)).Returns(Task.CompletedTask);
        _tenancyRepo.Setup(r => r.AddAsync(It.IsAny<Tenancy>(), default)).Returns(Task.CompletedTask);
        _membershipRepo.Setup(r => r.AddAsync(It.IsAny<TenancyMembership>(), default)).Returns(Task.CompletedTask);
        _subscriptionRepo.Setup(r => r.AddAsync(It.IsAny<TenancySubscription>(), default)).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(5);

        var sut    = CreateSut();
        var result = await sut.RegisterAsync(email, displayName, tenancyName);

        Assert.NotNull(result.User);
        Assert.Equal(Role.Author, result.User.Role);
        Assert.False(result.User.IsActive);
        Assert.NotNull(result.Account);
        Assert.NotNull(result.Tenancy);
        Assert.NotNull(result.Membership);
        Assert.NotNull(result.Subscription);
        Assert.Equal(TenancyRole.Author, result.Membership.Role);
        Assert.Equal(SubscriptionTier.Free, result.Subscription.Tier);

        _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Once);
        _accountRepo.Verify(r => r.AddAsync(It.IsAny<Account>(), default), Times.Once);
        _tenancyRepo.Verify(r => r.AddAsync(It.IsAny<Tenancy>(), default), Times.Once);
        _membershipRepo.Verify(r => r.AddAsync(It.IsAny<TenancyMembership>(), default), Times.Once);
        _subscriptionRepo.Verify(r => r.AddAsync(It.IsAny<TenancySubscription>(), default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenAccountEmailExists_ThrowsInvariantViolationException()
    {
        const string hmac = "FAKE-HMAC";
        _hmacService.Setup(s => s.Compute(It.IsAny<string>())).Returns(hmac);
        _accountRepo.Setup(r => r.EmailExistsAsync(hmac, default)).ReturnsAsync(true);

        var sut = CreateSut();
        var ex  = await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.RegisterAsync("taken@example.com", "Alice", "Workspace"));

        Assert.Equal("I-SELF-REG-EMAIL-EXISTS", ex.InvariantCode);
    }

    [Fact]
    public async Task RegisterAsync_WhenUserEmailExists_ThrowsInvariantViolationException()
    {
        const string email = "user@example.com";
        const string hmac  = "FAKE-HMAC";

        _hmacService.Setup(s => s.Compute(It.IsAny<string>())).Returns(hmac);
        _accountRepo.Setup(r => r.EmailExistsAsync(hmac, default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.EmailExistsAsync(email, default)).ReturnsAsync(true);

        var sut = CreateSut();
        var ex  = await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.RegisterAsync(email, "Alice", "Workspace"));

        Assert.Equal("I-SELF-REG-EMAIL-EXISTS", ex.InvariantCode);
    }

    [Fact]
    public async Task RegisterAsync_TrimsEmail_BeforeChecking()
    {
        const string rawEmail    = "  author@example.com  ";
        const string trimmedEmail = "author@example.com";
        const string hmac        = "FAKE-HMAC";

        _hmacService.Setup(s => s.Compute(It.IsAny<string>())).Returns(hmac);
        _accountRepo.Setup(r => r.EmailExistsAsync(hmac, default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.EmailExistsAsync(trimmedEmail, default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), default)).Returns(Task.CompletedTask);
        _accountRepo.Setup(r => r.AddAsync(It.IsAny<Account>(), default)).Returns(Task.CompletedTask);
        _tenancyRepo.Setup(r => r.AddAsync(It.IsAny<Tenancy>(), default)).Returns(Task.CompletedTask);
        _membershipRepo.Setup(r => r.AddAsync(It.IsAny<TenancyMembership>(), default)).Returns(Task.CompletedTask);
        _subscriptionRepo.Setup(r => r.AddAsync(It.IsAny<TenancySubscription>(), default)).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(5);

        var sut = CreateSut();
        var result = await sut.RegisterAsync(rawEmail, "Alice", "Workspace");

        Assert.Equal(trimmedEmail, result.User.Email);
    }
}
