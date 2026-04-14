using DraftView.Application.Interfaces;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using Moq;

namespace DraftView.Application.Tests.Services;

public class UserEmailProtectionServiceTests
{
    private readonly Mock<IUserRepository> userRepository = new();
    private readonly Mock<IUserEmailEncryptionService> emailEncryptionService = new();

    private UserEmailProtectionService CreateSut() => new(
        userRepository.Object,
        emailEncryptionService.Object);

    [Fact]
    public async Task GetEmailAsync_KnownProtectedUser_ReturnsResolvedEmail()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.SetProtectedEmail("ciphertext", "lookup");

        userRepository.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        emailEncryptionService.Setup(s => s.Decrypt("ciphertext")).Returns("reader@example.test");

        var sut = CreateSut();

        var result = await sut.GetEmailAsync(user.Id);

        Assert.Equal("reader@example.test", result);
    }

    [Fact]
    public async Task GetEmailAsync_MissingUser_FailsSafely()
    {
        userRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((User?)null);
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetEmailAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetEmailAsync_MissingCiphertext_FailsSafely()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        userRepository.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetEmailAsync(user.Id));
    }

    [Fact]
    public async Task GetEmailAsync_InvalidCiphertext_FailsSafely()
    {
        var user = User.Create("reader@example.test", "Reader", Role.BetaReader);
        user.SetProtectedEmail("ciphertext", "lookup");

        userRepository.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        emailEncryptionService
            .Setup(s => s.Decrypt("ciphertext"))
            .Throws(new InvalidOperationException("bad cipher"));

        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetEmailAsync(user.Id));
    }
}
