using Moq;
using DraftView.Application.Interfaces;
using DraftView.Application.Services;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for AuthorRegistrationService.
/// Covers: happy-path registration, duplicate email rejection, entity linkage,
/// role and tier defaults, SaveChanges coordination.
/// </summary>
public class AuthorRegistrationServiceTests
{
    private const string ValidEmail       = "author@example.com";
    private const string ValidDisplayName = "Author Name";
    private const string ValidTenancyName = "My Workspace";
    private const string FakeHmac         = "hmac_test_value";

    private readonly Mock<IAccountRepository>            _accountRepo      = new();
    private readonly Mock<ITenancyRepository>            _tenancyRepo      = new();
    private readonly Mock<ITenancyMembershipRepository>  _membershipRepo   = new();
    private readonly Mock<ITenancySubscriptionRepository> _subscriptionRepo = new();
    private readonly Mock<IUserEmailLookupHmacService>   _hmacService      = new();
    private readonly Mock<IUnitOfWork>                   _unitOfWork       = new();

    public AuthorRegistrationServiceTests()
    {
        _hmacService.Setup(h => h.Compute(It.IsAny<string>())).Returns(FakeHmac);
        _accountRepo.Setup(r => r.EmailExistsAsync(FakeHmac, default)).ReturnsAsync(false);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
    }

    private IAuthorRegistrationService CreateSut() => new AuthorRegistrationService(
        _accountRepo.Object,
        _tenancyRepo.Object,
        _membershipRepo.Object,
        _subscriptionRepo.Object,
        _hmacService.Object,
        _unitOfWork.Object);

    // ---------------------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_WithValidData_ReturnsLinkedResult()
    {
        var result = await CreateSut().RegisterAsync(ValidEmail, ValidDisplayName, ValidTenancyName);

        Assert.Equal(ValidDisplayName, result.Account.DisplayName);
        Assert.Equal(result.Account.Id, result.Tenancy.OwnerAccountId);
        Assert.Equal(result.Tenancy.Id, result.Membership.TenancyId);
        Assert.Equal(result.Account.Id, result.Membership.AccountId);
        Assert.Equal(result.Tenancy.Id, result.Subscription.TenancyId);
    }

    [Fact]
    public async Task RegisterAsync_SetsAuthorRole()
    {
        var result = await CreateSut().RegisterAsync(ValidEmail, ValidDisplayName, ValidTenancyName);

        Assert.Equal(TenancyRole.Author, result.Membership.Role);
    }

    [Fact]
    public async Task RegisterAsync_SetsFreeTierSubscription()
    {
        var result = await CreateSut().RegisterAsync(ValidEmail, ValidDisplayName, ValidTenancyName);

        Assert.Equal(SubscriptionTier.Free, result.Subscription.Tier);
        Assert.True(result.Subscription.IsActive);
    }

    [Fact]
    public async Task RegisterAsync_CallsAddAsyncOnAllRepositories()
    {
        await CreateSut().RegisterAsync(ValidEmail, ValidDisplayName, ValidTenancyName);

        _accountRepo.Verify(r => r.AddAsync(It.IsAny<DraftView.Domain.Entities.Account>(), default), Times.Once);
        _tenancyRepo.Verify(r => r.AddAsync(It.IsAny<DraftView.Domain.Entities.Tenancy>(), default), Times.Once);
        _membershipRepo.Verify(r => r.AddAsync(It.IsAny<DraftView.Domain.Entities.TenancyMembership>(), default), Times.Once);
        _subscriptionRepo.Verify(r => r.AddAsync(It.IsAny<DraftView.Domain.Entities.TenancySubscription>(), default), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_CallsSaveChangesOnce()
    {
        await CreateSut().RegisterAsync(ValidEmail, ValidDisplayName, ValidTenancyName);

        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_TrimsEmailBeforeHmacComputation()
    {
        await CreateSut().RegisterAsync("  author@example.com  ", ValidDisplayName, ValidTenancyName);

        _hmacService.Verify(h => h.Compute("author@example.com"), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Duplicate email
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ThrowsWithCorrectInvariantCode()
    {
        _accountRepo.Setup(r => r.EmailExistsAsync(FakeHmac, default)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateSut().RegisterAsync(ValidEmail, ValidDisplayName, ValidTenancyName));

        Assert.Equal("I-REG-EMAIL-EXISTS", ex.InvariantCode);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_DoesNotCallAddOrSave()
    {
        _accountRepo.Setup(r => r.EmailExistsAsync(FakeHmac, default)).ReturnsAsync(true);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateSut().RegisterAsync(ValidEmail, ValidDisplayName, ValidTenancyName));

        _accountRepo.Verify(r => r.AddAsync(It.IsAny<DraftView.Domain.Entities.Account>(), default), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
