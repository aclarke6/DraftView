using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class ChangeStateService(
    ISectionRepository sectionRepo,
    IReaderSnapshotRepository snapshotRepo,
    IHtmlDiffService htmlDiffService,
    IChangeClassificationService classificationService) : IChangeStateService
{
    public async Task<ChangeClassification?> GetChangeStateAsync(
        Guid sectionId, Guid userId, CancellationToken ct = default)
    {
        var section = await sectionRepo.GetByIdAsync(sectionId, ct);
        if (section is null || string.IsNullOrEmpty(section.HtmlContent))
            return ChangeClassification.New;

        var snapshot = await snapshotRepo.GetAsync(sectionId, userId, ct);
        if (snapshot is null)
            return ChangeClassification.New;

        if (snapshot.HtmlContent == section.HtmlContent)
            return null;

        var paragraphs = htmlDiffService.Compute(snapshot.HtmlContent, section.HtmlContent);
        return classificationService.Classify(paragraphs);
    }
}
