using DraftView.Application.Contracts;
using DraftView.Application.Interfaces;
using DraftView.Application.Services;
using DraftView.Domain.Enumerations;
using Microsoft.Extensions.Logging;
using Moq;

namespace DraftView.Application.Tests.Services;

public class ControlledUserEmailServiceTests
{
    private readonly Mock<IUserEmailAccessService> userEmailAccessService = new();
    private readonly Mock<IUserEmailProtectionService> userEmailProtectionService = new();
    private readonly Mock<ILogger<ControlledUserEmailService>> logger = new();

    private ControlledUserEmailService CreateSut() => new(
        userEmailAccessService.Object,
        userEmailProtectionService.Object,
        logger.Object);

    [Fact]
    public async Task GetEmailAsync_WhenAccessAllowed_ResolvesEmail()
    {
        var userId = Guid.NewGuid();
        var request = new UserEmailAccessRequest(
            userId,
            Role.BetaReader,
            userId,
            UserEmailAccessPurpose.SelfServiceSettings);

        userEmailAccessService
            .Setup(s => s.EvaluateAccessAsync(request, default))
            .ReturnsAsync(new UserEmailAccessResult(true));
        userEmailProtectionService
            .Setup(s => s.GetEmailAsync(request.TargetUserId, default))
            .ReturnsAsync("reader@example.test");

        var sut = CreateSut();

        var result = await sut.GetEmailAsync(request);

        Assert.Equal("reader@example.test", result);
        VerifyLogged(LogLevel.Information, "Allowed", request, null);
    }

    [Fact]
    public async Task GetEmailAsync_WhenAccessDenied_DoesNotAttemptDecryption()
    {
        var request = new UserEmailAccessRequest(
            Guid.NewGuid(),
            Role.Author,
            Guid.NewGuid(),
            UserEmailAccessPurpose.SupportOperation);

        userEmailAccessService
            .Setup(s => s.EvaluateAccessAsync(request, default))
            .ReturnsAsync(new UserEmailAccessResult(false, "Email access denied."));

        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetEmailAsync(request));

        userEmailProtectionService.Verify(s => s.GetEmailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyLogged(LogLevel.Warning, "Denied", request, "Email access denied.");
    }

    [Fact]
    public async Task GetEmailAsync_WhenSystemSupportCrossUserAccessIsRequested_DeniesByDefault()
    {
        var request = new UserEmailAccessRequest(
            Guid.NewGuid(),
            Role.SystemSupport,
            Guid.NewGuid(),
            UserEmailAccessPurpose.SelfServiceSettings);

        userEmailAccessService
            .Setup(s => s.EvaluateAccessAsync(request, default))
            .ReturnsAsync(new UserEmailAccessResult(false, "Email access denied."));

        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetEmailAsync(request));

        userEmailProtectionService.Verify(s => s.GetEmailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyLogged(LogLevel.Warning, "Denied", request, "Email access denied.");
    }

    [Fact]
    public async Task GetEmailAsync_WhenSystemSupportSupportOperationIsRequestedWithoutExplicitPrivilege_DeniesByDefault()
    {
        var request = new UserEmailAccessRequest(
            Guid.NewGuid(),
            Role.SystemSupport,
            Guid.NewGuid(),
            UserEmailAccessPurpose.SupportOperation);

        userEmailAccessService
            .Setup(s => s.EvaluateAccessAsync(request, default))
            .ReturnsAsync(new UserEmailAccessResult(false, "Email access denied."));

        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetEmailAsync(request));

        userEmailProtectionService.Verify(s => s.GetEmailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyLogged(LogLevel.Warning, "Denied", request, "Email access denied.");
    }

    private void VerifyLogged(
        LogLevel expectedLevel,
        string expectedOutcome,
        UserEmailAccessRequest request,
        string? expectedReason)
    {
        var logInvocation = Assert.Single(logger.Invocations, i => i.Method.Name == nameof(ILogger.Log));
        Assert.Equal(expectedLevel, (LogLevel)logInvocation.Arguments[0]);

        var state = Assert.IsAssignableFrom<IReadOnlyList<KeyValuePair<string, object?>>>(logInvocation.Arguments[2]);
        AssertLogProperty(state, "AccessOutcome", expectedOutcome);
        AssertLogProperty(state, "RequestingUserId", request.RequestingUserId);
        AssertLogProperty(state, "TargetUserId", request.TargetUserId);
        AssertLogProperty(state, "RequestingUserRole", request.RequestingUserRole);
        AssertLogProperty(state, "Purpose", request.Purpose);

        var timestamp = GetLogPropertyValue(state, "AuditTimestampUtc");
        Assert.IsType<DateTimeOffset>(timestamp);

        if (expectedReason is null)
            Assert.Null(GetLogPropertyValue(state, "Reason"));
        else
            Assert.Equal(expectedReason, GetLogPropertyValue(state, "Reason"));
    }

    private static void AssertLogProperty(
        IReadOnlyList<KeyValuePair<string, object?>> state,
        string key,
        object expectedValue)
    {
        Assert.Equal(expectedValue, GetLogPropertyValue(state, key));
    }

    private static object? GetLogPropertyValue(
        IReadOnlyList<KeyValuePair<string, object?>> state,
        string key)
    {
        return state.Single(entry => entry.Key == key).Value;
    }
}
