using Moq;
using DraftView.Application.Services;
using DraftView.Application.Interfaces;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Application.Tests.Services;

public class ReaderSelfRegistrationServiceTests
{
    private readonly Mock<IUserRepository> _userRepo   = new();
    private readonly Mock<IUnitOfWork>    _unitOfWork = new();

    private ReaderSelfRegistrationService CreateSut() => new(
        _userRepo.Object,
        _unitOfWork.Object);

    [Fact]
    public async Task RegisterAsync_WithValidInputs_CreatesUserAndSavesOnce()
    {
        const string email       = "reader@example.com";
        const string displayName = "Bob";

        _userRepo.Setup(r => r.EmailExistsAsync(email, default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), default)).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var sut    = CreateSut();
        var result = await sut.RegisterAsync(email, displayName);

        Assert.NotNull(result.User);
        Assert.Equal(Role.BetaReader, result.User.Role);
        Assert.False(result.User.IsActive);
        Assert.Equal(email, result.User.Email);
        Assert.Equal(displayName, result.User.DisplayName);

        _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailExists_ThrowsInvariantViolationException()
    {
        const string email = "taken@example.com";
        _userRepo.Setup(r => r.EmailExistsAsync(email, default)).ReturnsAsync(true);

        var sut = CreateSut();
        var ex  = await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.RegisterAsync(email, "Bob"));

        Assert.Equal("I-SELF-REG-EMAIL-EXISTS", ex.InvariantCode);
        _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_TrimsEmail_BeforeChecking()
    {
        const string rawEmail     = "  reader@example.com  ";
        const string trimmedEmail = "reader@example.com";

        _userRepo.Setup(r => r.EmailExistsAsync(trimmedEmail, default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), default)).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var sut    = CreateSut();
        var result = await sut.RegisterAsync(rawEmail, "Bob");

        Assert.Equal(trimmedEmail, result.User.Email);
    }

    [Fact]
    public async Task RegisterAsync_DoesNotCreateAccountOrTenancy()
    {
        const string email = "reader@example.com";

        _userRepo.Setup(r => r.EmailExistsAsync(email, default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), default)).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var sut = CreateSut();
        await sut.RegisterAsync(email, "Bob");

        // Only SaveChanges once — no account, tenancy, membership, or subscription calls
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
        _userRepo.Verify(r => r.AddAsync(It.Is<User>(u => u.Role == Role.BetaReader), default), Times.Once);
    }
}
