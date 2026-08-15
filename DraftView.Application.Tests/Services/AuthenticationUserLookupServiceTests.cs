using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using Moq;

namespace DraftView.Application.Tests.Services;

public class AuthenticationUserLookupServiceTests
{
    private readonly Mock<IUserRepository> userRepository = new();

    private AuthenticationUserLookupService CreateSut() => new(userRepository.Object);

    [Fact]
    public async Task FindByDisplayNameAsync_ExactlyOneMatch_ReturnsUser()
    {
        var user = User.Create("reader@example.test", "Alastair Dunlop", Role.BetaReader);
        userRepository
            .Setup(r => r.FindByDisplayNameAsync("Alastair Dunlop", default))
            .ReturnsAsync([user]);

        var result = await CreateSut().FindByDisplayNameAsync("Alastair Dunlop");

        Assert.Same(user, result);
    }

    [Fact]
    public async Task FindByDisplayNameAsync_NoMatch_ReturnsNull()
    {
        userRepository
            .Setup(r => r.FindByDisplayNameAsync("Unknown", default))
            .ReturnsAsync([]);

        var result = await CreateSut().FindByDisplayNameAsync("Unknown");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByDisplayNameAsync_MultipleMatches_ReturnsNull()
    {
        var u1 = User.Create("a@example.test", "Alex", Role.BetaReader);
        var u2 = User.Create("b@example.test", "Alex", Role.BetaReader);
        userRepository
            .Setup(r => r.FindByDisplayNameAsync("Alex", default))
            .ReturnsAsync([u1, u2]);

        var result = await CreateSut().FindByDisplayNameAsync("Alex");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByDisplayNameAsync_EmptyInput_ReturnsNull()
    {
        var result = await CreateSut().FindByDisplayNameAsync("   ");

        Assert.Null(result);
        userRepository.Verify(r => r.FindByDisplayNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindByLoginEmailAsync_KnownLoginEmail_ReturnsMatchingDomainUser()
    {
        var expectedUser = User.Create("reader@example.test", "Reader", Role.BetaReader);
        userRepository
            .Setup(r => r.GetByEmailAsync("reader@example.test", default))
            .ReturnsAsync(expectedUser);

        var sut = CreateSut();

        var result = await sut.FindByLoginEmailAsync("reader@example.test");

        Assert.Same(expectedUser, result);
    }
}
