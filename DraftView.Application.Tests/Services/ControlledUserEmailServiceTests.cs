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
        VerifyLogged(LogLevel.Information, "Controlled email access allowed", request, null);
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
        VerifyLogged(LogLevel.Warning, "Controlled email access denied", request, "Email access denied.");
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
        VerifyLogged(LogLevel.Warning, "Controlled email access denied", request, "Email access denied.");
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
        VerifyLogged(LogLevel.Warning, "Controlled email access denied", request, "Email access denied.");
    }

    private void VerifyLogged(
        LogLevel expectedLevel,
        string expectedPrefix,
        UserEmailAccessRequest request,
        string? expectedReason)
    {
        logger.Verify(
            x => x.Log(
                expectedLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains(expectedPrefix) &&
                    state.ToString()!.Contains(request.RequestingUserId.ToString()) &&
                    state.ToString()!.Contains(request.TargetUserId.ToString()) &&
                    state.ToString()!.Contains(request.RequestingUserRole.ToString()) &&
                    state.ToString()!.Contains(request.Purpose.ToString()) &&
                    (expectedReason == null || state.ToString()!.Contains(expectedReason))),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
