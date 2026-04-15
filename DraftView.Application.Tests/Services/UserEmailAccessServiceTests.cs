using DraftView.Application.Contracts;
using DraftView.Application.Services;
using DraftView.Domain.Enumerations;

namespace DraftView.Application.Tests.Services;

public class UserEmailAccessServiceTests
{
    private static UserEmailAccessService CreateSut() => new();

    [Fact]
    public async Task EvaluateAccessAsync_SelfAccess_AllowsAccess()
    {
        var userId = Guid.NewGuid();
        var sut = CreateSut();

        var result = await sut.EvaluateAccessAsync(new UserEmailAccessRequest(
            userId,
            Role.BetaReader,
            userId,
            UserEmailAccessPurpose.SelfServiceSettings));

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task EvaluateAccessAsync_SystemSupportSupportOperation_DeniesAccessWithoutExplicitPrivilege()
    {
        var sut = CreateSut();

        var result = await sut.EvaluateAccessAsync(new UserEmailAccessRequest(
            Guid.NewGuid(),
            Role.SystemSupport,
            Guid.NewGuid(),
            UserEmailAccessPurpose.SupportOperation));

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task EvaluateAccessAsync_AuthorAccessToReaderEmail_DeniesAccess()
    {
        var sut = CreateSut();

        var result = await sut.EvaluateAccessAsync(new UserEmailAccessRequest(
            Guid.NewGuid(),
            Role.Author,
            Guid.NewGuid(),
            UserEmailAccessPurpose.SupportOperation));

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task EvaluateAccessAsync_BetaReaderCrossUserAccess_DeniesAccess()
    {
        var sut = CreateSut();

        var result = await sut.EvaluateAccessAsync(new UserEmailAccessRequest(
            Guid.NewGuid(),
            Role.BetaReader,
            Guid.NewGuid(),
            UserEmailAccessPurpose.SupportOperation));

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task EvaluateAccessAsync_SystemSupportCrossUserWrongPurpose_DeniesAccessByDefault()
    {
        var sut = CreateSut();

        var result = await sut.EvaluateAccessAsync(new UserEmailAccessRequest(
            Guid.NewGuid(),
            Role.SystemSupport,
            Guid.NewGuid(),
            UserEmailAccessPurpose.SelfServiceSettings));

        Assert.False(result.IsAllowed);
    }
}
