using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class ReadingProgressService(
    IReadEventRepository readEventRepo,
    ISectionRepository sectionRepo,
    IPassageAnchorService passageAnchorService,
    IUnitOfWork unitOfWork) : IReadingProgressService
{
    public async Task RecordOpenAsync(
        Guid sectionId, Guid userId, CancellationToken ct = default)
    {
        var existing = await readEventRepo.GetAsync(sectionId, userId, ct);

        if (existing is null)
        {
            var readEvent = ReadEvent.Create(sectionId, userId);
            await readEventRepo.AddAsync(readEvent, ct);
        }
        else
        {
            existing.RecordOpen();
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<bool> IsCaughtUpAsync(
        Guid userId, Guid projectId, CancellationToken ct = default)
    {
        var published = await sectionRepo.GetPublishedByProjectIdAsync(projectId, ct);

        if (published.Count == 0)
            return true;

        foreach (var section in published)
        {
            if (!await readEventRepo.HasReadAsync(section.Id, userId, ct))
                return false;
        }

        return true;
    }

    public async Task<bool> HasReadSectionAsync(Guid userId, Guid sectionId, CancellationToken ct = default) =>
        await readEventRepo.HasReadAsync(sectionId, userId, ct);

    public async Task<IReadOnlyList<ReadEvent>> GetProgressForProjectAsync(
        Guid projectId, CancellationToken ct = default) =>
        await readEventRepo.GetByProjectIdAsync(projectId, ct);

    public async Task<ReadEvent?> GetLastReadEventAsync(
        Guid userId, Guid projectId, CancellationToken ct = default)
    {
        var events = await readEventRepo.GetByProjectIdAsync(projectId, ct);
        return events
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.LastOpenedAt)
            .FirstOrDefault();
    }

    public async Task UpdateLastReadVersionAsync(
        Guid sectionId,
        Guid userId,
        int versionNumber,
        CancellationToken ct = default)
    {
        var readEvent = await readEventRepo.GetAsync(sectionId, userId, ct);
        if (readEvent is not null)
        {
            readEvent.UpdateLastReadVersion(versionNumber);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task DismissBannerAsync(Guid sectionId, Guid userId, int versionNumber, CancellationToken ct = default)
    {
        var readEvent = await readEventRepo.GetAsync(sectionId, userId, ct);
        if (readEvent is null) return;

        readEvent.DismissBannerAtVersion(versionNumber);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Persists the reader's latest resume position as a resume-purpose passage anchor
    /// and stores the anchor id on the current read event.
    /// </summary>
    public async Task CaptureResumePositionAsync(
        CaptureResumePositionRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var anchor = await passageAnchorService.CreateAsync(
            new CreatePassageAnchorRequest(
                request.SectionId,
                request.OriginalSectionVersionId,
                PassageAnchorPurpose.Resume,
                request.SelectedText,
                request.NormalizedSelectedText,
                request.SelectedTextHash,
                request.PrefixContext,
                request.SuffixContext,
                request.StartOffset,
                request.EndOffset,
                request.CanonicalContentHash,
                request.HtmlSelectorHint),
            userId,
            ct);

        var readEvent = await readEventRepo.GetAsync(request.SectionId, userId, ct);
        if (readEvent is null)
        {
            readEvent = ReadEvent.Create(request.SectionId, userId);
            await readEventRepo.AddAsync(readEvent, ct);
        }

        readEvent.UpdateResumeAnchor(anchor.Id);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
