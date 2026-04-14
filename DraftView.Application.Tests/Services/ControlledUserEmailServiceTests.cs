using DraftView.Application.Contracts;
using DraftView.Application.Interfaces;
using DraftView.Application.Services;
using DraftView.Domain.Enumerations;
using Moq;

namespace DraftView.Application.Tests.Services;

public class ControlledUserEmailServiceTests
{
    private readonly Mock<IUserEmailAccessService> userEmailAccessService = new();
    private readonly Mock<IUserEmailProtectionService> userEmailProtectionService = new();

    private ControlledUserEmailService CreateSut() => new(
        userEmailAccessService.Object,
        userEmailProtectionService.Object);

    [Fact]
    public async Task GetEmailAsync_WhenAccessAllowed_ResolvesEmail()
    {
        var request = new UserEmailAccessRequest(
            Guid.NewGuid(),
            Role.SystemSupport,
            Guid.NewGuid(),
            UserEmailAccessPurpose.SupportOperation);

        userEmailAccessService
            .Setup(s => s.EvaluateAccessAsync(request, default))
            .ReturnsAsync(new UserEmailAccessResult(true));
        userEmailProtectionService
            .Setup(s => s.GetEmailAsync(request.TargetUserId, default))
            .ReturnsAsync("reader@example.test");

        var sut = CreateSut();

        var result = await sut.GetEmailAsync(request);

        Assert.Equal("reader@example.test", result);
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
    }

    [Fact]
    public async Task GetEmailAsync_WhenPrivilegedRoleIsNotExplicitlyAllowed_DeniesByDefault()
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
    }
}
