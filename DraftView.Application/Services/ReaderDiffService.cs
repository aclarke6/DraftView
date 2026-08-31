using DraftView.Domain.Contracts;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Orchestrates reader-facing diff operations: get diff with preference filtering,
/// mark as read at the current version, and mark as unread to restore the prior baseline.
/// </summary>
public class ReaderDiffService(
    IReadEventRepository readEventRepo,
    IUserPreferencesRepository userPreferencesRepo,
    ISectionVersionRepository sectionVersionRepo,
    ISectionDiffService sectionDiffService,
    IUnitOfWork unitOfWork) : IReaderDiffService
{
    /// <summary>
    /// Returns the diff for the section, applying the reader's ShowDiffOnRevisit preference,
    /// cooldown, and ReadingStyle threshold. Returns null when the feature is off or suppressed.
    /// </summary>
    public async Task<SectionDiffResult?> GetDiffAsync(
        Guid sectionId, Guid userId, CancellationToken ct = default)
    {
        var prefs = await userPreferencesRepo.GetByUserIdAsync(userId, ct);

        if (prefs is null || !prefs.ShowDiffOnRevisit)
            return null;

        var readEvent = await readEventRepo.GetAsync(sectionId, userId, ct);

        return await sectionDiffService.GetDiffForReaderAsync(
            sectionId,
            readEvent?.LastReadVersionNumber,
            readEvent?.LastMarkedReadAt,
            prefs.DiffCooldownHours,
            prefs.ReadingStyle,
            ct);
    }

    /// <summary>
    /// Marks the section as read at the current latest published version.
    /// No-op when no ReadEvent or no published version exists for the section.
    /// </summary>
    public async Task MarkAsReadAsync(
        Guid sectionId, Guid userId, CancellationToken ct = default)
    {
        var readEvent = await readEventRepo.GetAsync(sectionId, userId, ct);
        if (readEvent is null) return;

        var latestVersion = await sectionVersionRepo.GetLatestAsync(sectionId, ct);
        if (latestVersion is null) return;

        readEvent.MarkAsRead(latestVersion.VersionNumber);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Reverses the last MarkAsRead, restoring the previous diff baseline.
    /// No-op when no ReadEvent exists.
    /// </summary>
    public async Task MarkAsUnreadAsync(
        Guid sectionId, Guid userId, CancellationToken ct = default)
    {
        var readEvent = await readEventRepo.GetAsync(sectionId, userId, ct);
        if (readEvent is null) return;

        readEvent.MarkAsUnread();
        await unitOfWork.SaveChangesAsync(ct);
    }
}
