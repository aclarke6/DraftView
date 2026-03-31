using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

public class NotificationServiceTests
{
    private readonly Mock<IEmailDeliveryLogRepository> _logRepo     = new();
    private readonly Mock<IUserRepository>             _userRepo    = new();
    private readonly Mock<IEmailSender>                _emailSender = new();
    private readonly Mock<IUnitOfWork>                 _unitOfWork  = new();

    private NotificationService CreateSut() => new(
        _logRepo.Object,
        _userRepo.Object,
        _emailSender.Object,
        _unitOfWork.Object);

    private static User MakeActiveReader()
    {
        var u = User.Create("reader@example.com", "Reader", Role.BetaReader);
        u.Activate();
        return u;
    }

    // ---------------------------------------------------------------------------
    // SendImmediate
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SendImmediateAsync_ValidRecipient_SendsEmailAndLogsSuccess()
    {
        var recipient = MakeActiveReader();
        var sut       = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(recipient.Id, default)).ReturnsAsync(recipient);

        EmailDeliveryLog? logged = null;
        _logRepo.Setup(r => r.AddAsync(It.IsAny<EmailDeliveryLog>(), default))
            .Callback<EmailDeliveryLog, CancellationToken>((l, _) => logged = l);

        _emailSender.Setup(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        await sut.SendImmediateAsync(EmailType.ReplyNotification, recipient.Id, null);

        Assert.NotNull(logged);
        _emailSender.Verify(e => e.SendAsync(
            recipient.Email, recipient.DisplayName,
            It.IsAny<string>(), It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task SendImmediateAsync_EmailFails_LogsFailure()
    {
        var recipient = MakeActiveReader();
        var sut       = CreateSut();

        _userRepo.Setup(r => r.GetByIdAsync(recipient.Id, default)).ReturnsAsync(recipient);

        EmailDeliveryLog? logged = null;
        _logRepo.Setup(r => r.AddAsync(It.IsAny<EmailDeliveryLog>(), default))
            .Callback<EmailDeliveryLog, CancellationToken>((l, _) => logged = l);

        _emailSender.Setup(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), default))
            .ThrowsAsync(new Exception("SMTP error."));

        await sut.SendImmediateAsync(EmailType.ReplyNotification, recipient.Id, null);

        Assert.NotNull(logged);
        Assert.Equal(EmailStatus.Retrying, logged!.Status);
    }

    // ---------------------------------------------------------------------------
    // RetryFailed
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RetryFailedAsync_SuccessfulRetry_SetsSentStatus()
    {
        var recipient = MakeActiveReader();
        var log       = EmailDeliveryLog.Create(recipient.Id, recipient.Email, EmailType.Invitation, null);
        log.RecordAttempt(false, "Timeout.");
        var sut = CreateSut();

        _logRepo.Setup(r => r.GetRetryingAsync(default))
            .ReturnsAsync(new List<EmailDeliveryLog> { log });
        _userRepo.Setup(r => r.GetByIdAsync(recipient.Id, default)).ReturnsAsync(recipient);
        _emailSender.Setup(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        await sut.RetryFailedAsync();

        Assert.Equal(EmailStatus.Sent, log.Status);
    }
}
