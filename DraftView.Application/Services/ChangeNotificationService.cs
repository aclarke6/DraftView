using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class ChangeNotificationService(
    IReadEventRepository readEventRepo,
    IUserPreferencesRepository prefsRepo,
    IChangeStateService changeStateService,
    INotificationService notificationService) : IChangeNotificationService
{
    public async Task SendChangeNotificationsAsync(Guid sectionId, CancellationToken ct = default)
    {
        var events = await readEventRepo.GetBySectionIdAsync(sectionId, ct);

        foreach (var ev in events)
        {
            var prefs = await prefsRepo.GetByUserIdAsync(ev.UserId, ct);
            if (prefs is null || !prefs.NotifyOnSectionChanged) continue;

            var classification = await changeStateService.GetChangeStateAsync(sectionId, ev.UserId, ct);
            if (classification is null || classification == ChangeClassification.New) continue;

            if (classification < MinimumTier(prefs.ReadingStyle)) continue;

            await notificationService.SendImmediateAsync(
                EmailType.SectionChangedNotification, ev.UserId, sectionId, ct);
        }
    }

    private static ChangeClassification MinimumTier(ReadingStyle style) => style switch
    {
        ReadingStyle.BetaReader    => ChangeClassification.Trivial,
        ReadingStyle.StoryReader   => ChangeClassification.Polish,
        ReadingStyle.AlphaReader   => ChangeClassification.Revision,
        ReadingStyle.StructureOnly => ChangeClassification.Rewrite,
        _                          => ChangeClassification.Polish
    };
}
