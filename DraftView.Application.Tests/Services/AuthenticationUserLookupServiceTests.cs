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
