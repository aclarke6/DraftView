using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

public class ChangeNotificationServiceTests
{
    private static readonly Guid SectionId = Guid.NewGuid();

    private readonly Mock<IReadEventRepository>          _readEventRepo      = new();
    private readonly Mock<IUserPreferencesRepository>    _prefsRepo          = new();
    private readonly Mock<IChangeStateService>           _changeStateService = new();
    private readonly Mock<INotificationService>          _notificationService = new();

    private ChangeNotificationService CreateSut() => new(
        _readEventRepo.Object,
        _prefsRepo.Object,
        _changeStateService.Object,
        _notificationService.Object);

    private static UserPreferences MakePrefs(
        Guid userId,
        bool notifyOnSectionChanged = true,
        ReadingStyle style = ReadingStyle.StoryReader)
    {
        var prefs = UserPreferences.CreateForBetaReader(userId);
        prefs.UpdateBetaReaderPreferences(
            notifyOnNewSection: false,
            notifyOnSectionChanged: notifyOnSectionChanged,
            notifyOnReply: NotifyOnReply.Never);
        prefs.UpdateDiffPreferences(showDiffOnRevisit: false, style, diffCooldownHours: 24);
        return prefs;
    }

    [Fact]
    public async Task SendChangeNotificationsAsync_NoReadEvents_DoesNotSend()
    {
        _readEventRepo.Setup(r => r.GetBySectionIdAsync(SectionId, default))
            .ReturnsAsync([]);

        await CreateSut().SendChangeNotificationsAsync(SectionId);

        _notificationService.Verify(s => s.SendImmediateAsync(
            It.IsAny<EmailType>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendChangeNotificationsAsync_ReaderNotifyOff_DoesNotSend()
    {
        var userId = Guid.NewGuid();
        var ev = ReadEvent.Create(SectionId, userId);
        _readEventRepo.Setup(r => r.GetBySectionIdAsync(SectionId, default))
            .ReturnsAsync([ev]);
        _prefsRepo.Setup(r => r.GetByUserIdAsync(userId, default))
            .ReturnsAsync(MakePrefs(userId, notifyOnSectionChanged: false));

        await CreateSut().SendChangeNotificationsAsync(SectionId);

        _notificationService.Verify(s => s.SendImmediateAsync(
            It.IsAny<EmailType>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendChangeNotificationsAsync_ReaderUpToDate_DoesNotSend()
    {
        var userId = Guid.NewGuid();
        var ev = ReadEvent.Create(SectionId, userId);
        _readEventRepo.Setup(r => r.GetBySectionIdAsync(SectionId, default))
            .ReturnsAsync([ev]);
        _prefsRepo.Setup(r => r.GetByUserIdAsync(userId, default))
            .ReturnsAsync(MakePrefs(userId));
        _changeStateService.Setup(s => s.GetChangeStateAsync(SectionId, userId, default))
            .ReturnsAsync((ChangeClassification?)null);

        await CreateSut().SendChangeNotificationsAsync(SectionId);

        _notificationService.Verify(s => s.SendImmediateAsync(
            It.IsAny<EmailType>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendChangeNotificationsAsync_ReaderNew_NoSnapshot_DoesNotSend()
    {
        var userId = Guid.NewGuid();
        var ev = ReadEvent.Create(SectionId, userId);
        _readEventRepo.Setup(r => r.GetBySectionIdAsync(SectionId, default))
            .ReturnsAsync([ev]);
        _prefsRepo.Setup(r => r.GetByUserIdAsync(userId, default))
            .ReturnsAsync(MakePrefs(userId));
        _changeStateService.Setup(s => s.GetChangeStateAsync(SectionId, userId, default))
            .ReturnsAsync(ChangeClassification.New);

        await CreateSut().SendChangeNotificationsAsync(SectionId);

        _notificationService.Verify(s => s.SendImmediateAsync(
            It.IsAny<EmailType>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendChangeNotificationsAsync_ClassificationMeetsThreshold_Sends()
    {
        // StoryReader threshold = Polish; Revision >= Polish → send
        var userId = Guid.NewGuid();
        var ev = ReadEvent.Create(SectionId, userId);
        _readEventRepo.Setup(r => r.GetBySectionIdAsync(SectionId, default))
            .ReturnsAsync([ev]);
        _prefsRepo.Setup(r => r.GetByUserIdAsync(userId, default))
            .ReturnsAsync(MakePrefs(userId, style: ReadingStyle.StoryReader));
        _changeStateService.Setup(s => s.GetChangeStateAsync(SectionId, userId, default))
            .ReturnsAsync(ChangeClassification.Revision);

        await CreateSut().SendChangeNotificationsAsync(SectionId);

        _notificationService.Verify(s => s.SendImmediateAsync(
            EmailType.SectionChangedNotification, userId, SectionId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendChangeNotificationsAsync_ClassificationBelowThreshold_DoesNotSend()
    {
        // AlphaReader threshold = Revision; Polish < Revision → no send
        var userId = Guid.NewGuid();
        var ev = ReadEvent.Create(SectionId, userId);
        _readEventRepo.Setup(r => r.GetBySectionIdAsync(SectionId, default))
            .ReturnsAsync([ev]);
        _prefsRepo.Setup(r => r.GetByUserIdAsync(userId, default))
            .ReturnsAsync(MakePrefs(userId, style: ReadingStyle.AlphaReader));
        _changeStateService.Setup(s => s.GetChangeStateAsync(SectionId, userId, default))
            .ReturnsAsync(ChangeClassification.Polish);

        await CreateSut().SendChangeNotificationsAsync(SectionId);

        _notificationService.Verify(s => s.SendImmediateAsync(
            It.IsAny<EmailType>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendChangeNotificationsAsync_MultipleReaders_OnlySendToEligible()
    {
        var reader1Id = Guid.NewGuid();
        var reader2Id = Guid.NewGuid();
        var ev1 = ReadEvent.Create(SectionId, reader1Id);
        var ev2 = ReadEvent.Create(SectionId, reader2Id);

        _readEventRepo.Setup(r => r.GetBySectionIdAsync(SectionId, default))
            .ReturnsAsync([ev1, ev2]);

        // reader1: notify on, Revision >= Polish threshold → send
        _prefsRepo.Setup(r => r.GetByUserIdAsync(reader1Id, default))
            .ReturnsAsync(MakePrefs(reader1Id, notifyOnSectionChanged: true, style: ReadingStyle.StoryReader));
        _changeStateService.Setup(s => s.GetChangeStateAsync(SectionId, reader1Id, default))
            .ReturnsAsync(ChangeClassification.Revision);

        // reader2: notify off → no send
        _prefsRepo.Setup(r => r.GetByUserIdAsync(reader2Id, default))
            .ReturnsAsync(MakePrefs(reader2Id, notifyOnSectionChanged: false));

        await CreateSut().SendChangeNotificationsAsync(SectionId);

        _notificationService.Verify(s => s.SendImmediateAsync(
            EmailType.SectionChangedNotification, reader1Id, SectionId,
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationService.Verify(s => s.SendImmediateAsync(
            It.IsAny<EmailType>(), reader2Id, It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
