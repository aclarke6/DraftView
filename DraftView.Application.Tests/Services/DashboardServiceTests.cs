using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Tests.Services;

public class DashboardServiceTests
{
    private static readonly Guid AuthorId = Guid.NewGuid();

    private readonly Mock<ISectionRepository>              _sectionRepo      = new();
    private readonly Mock<IUserRepository>                 _userRepo         = new();
    private readonly Mock<IEmailDeliveryLogRepository>     _logRepo          = new();
    private readonly Mock<IAuthorNotificationRepository>   _notificationRepo = new();
    private readonly Mock<IUnitOfWork>                     _unitOfWork       = new();

    private DashboardService CreateSut() => new(
        _sectionRepo.Object,
        _userRepo.Object,
        _logRepo.Object,
        _notificationRepo.Object,
        _unitOfWork.Object);

    // -----------------------------------------------------------------------
    // Existing tests — must remain GREEN
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetProjectOverviewAsync_ReturnsSections()
    {
        var projectId = Guid.NewGuid();
        var section   = Section.CreateDocument(projectId, "UUID-1", "Scene 1",
            null, 0, "<p>x</p>", "h", "First Draft");
        section.PublishAsPartOfChapter("h");
        var sut = CreateSut();

        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(projectId, default))
            .ReturnsAsync(new List<Section> { section });

        var result = await sut.GetProjectOverviewAsync(projectId);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetReaderSummaryAsync_ReturnsBetaReaders()
    {
        var reader = User.Create("reader@example.com", "Reader", Role.BetaReader);
        reader.Activate();
        var sut = CreateSut();

        _userRepo.Setup(r => r.GetAllBetaReadersAsync(default))
            .ReturnsAsync(new List<User> { reader });

        var result = await sut.GetReaderSummaryAsync();

        Assert.Single(result);
    }

    [Fact]
    public async Task GetEmailHealthSummaryAsync_ReturnsFailedLogs()
    {
        var log = EmailDeliveryLog.Create(Guid.NewGuid(), "test@example.com",
            EmailType.Invitation, null);
        log.RecordAttempt(false, "Timeout.");
        log.MarkFailed();
        var sut = CreateSut();

        _logRepo.Setup(r => r.GetFailedAsync(default))
            .ReturnsAsync(new List<EmailDeliveryLog> { log });

        var result = await sut.GetEmailHealthSummaryAsync();

        Assert.Single(result);
        Assert.Equal(EmailStatus.Failed, result[0].Status);
    }

    // -----------------------------------------------------------------------
    // GetNotificationsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetNotificationsAsync_ReturnsNotificationsForAuthor()
    {
        var n = AuthorNotification.Create(
            AuthorId, NotificationEventType.NewComment, "Alice commented", null, null, DateTime.UtcNow);
        var sut = CreateSut();

        _notificationRepo
            .Setup(r => r.PruneOlderThanAsync(AuthorId, It.IsAny<DateTime>(), default))
            .Returns(Task.CompletedTask);
        _notificationRepo
            .Setup(r => r.GetByAuthorIdAsync(AuthorId, default))
            .ReturnsAsync(new List<AuthorNotification> { n });

        var result = await sut.GetNotificationsAsync(AuthorId);

        Assert.Single(result);
        Assert.Equal("Alice commented", result[0].Title);
    }

    [Fact]
    public async Task GetNotificationsAsync_CallsPruneBeforeReturning()
    {
        var sut = CreateSut();

        _notificationRepo
            .Setup(r => r.PruneOlderThanAsync(AuthorId, It.IsAny<DateTime>(), default))
            .Returns(Task.CompletedTask);
        _notificationRepo
            .Setup(r => r.GetByAuthorIdAsync(AuthorId, default))
            .ReturnsAsync(new List<AuthorNotification>());

        await sut.GetNotificationsAsync(AuthorId);

        _notificationRepo.Verify(
            r => r.PruneOlderThanAsync(AuthorId, It.IsAny<DateTime>(), default),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // DismissNotificationAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DismissNotificationAsync_CallsDeleteOnRepo()
    {
        var notifId = Guid.NewGuid();
        var sut = CreateSut();

        _notificationRepo
            .Setup(r => r.DeleteAsync(notifId, default))
            .Returns(Task.CompletedTask);

        await sut.DismissNotificationAsync(notifId);

        _notificationRepo.Verify(r => r.DeleteAsync(notifId, default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // -----------------------------------------------------------------------
    // DismissAllNotificationsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DismissAllNotificationsAsync_CallsDeleteAllOnRepo()
    {
        var sut = CreateSut();

        _notificationRepo
            .Setup(r => r.DeleteAllByAuthorIdAsync(AuthorId, default))
            .Returns(Task.CompletedTask);

        await sut.DismissAllNotificationsAsync(AuthorId);

        _notificationRepo.Verify(r => r.DeleteAllByAuthorIdAsync(AuthorId, default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
