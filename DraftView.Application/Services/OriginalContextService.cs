using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

#pragma warning disable CS9113
public class OriginalContextService(
    IPassageAnchorRepository passageAnchorRepo,
    ISectionVersionRepository sectionVersionRepo,
    ISectionRepository sectionRepo,
    IReaderAccessRepository readerAccessRepo,
    IUserRepository userRepo,
    IAuthorizationFacade authFacade) : IOriginalContextService
{
    public async Task<OriginalContextResultDto> GetOriginalContextAsync(
        Guid passageAnchorId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        // Load anchor
        var anchor = await passageAnchorRepo.GetByIdAsync(passageAnchorId, cancellationToken);
        if (anchor is null)
            return OriginalContextResultDto.Failure(OriginalContextFailureReason.NotFound);

        // Load section for authorization
        var section = await sectionRepo.GetByIdAsync(anchor.SectionId, cancellationToken);
        if (section is null)
            return OriginalContextResultDto.Failure(OriginalContextFailureReason.NotFound);

        // Authorize
        var authResult = await TryAuthorizeAsync(section, requestingUserId, cancellationToken);
        if (!authResult)
            return OriginalContextResultDto.Failure(OriginalContextFailureReason.Unauthorized);

        // Load original content
        string originalHtmlContent;
        int? originalVersionNumber = null;
        DateTime? originalVersionCreatedAt = null;
        bool isLegacyFallback;

        if (anchor.OriginalSectionVersionId.HasValue)
        {
            var version = await sectionVersionRepo.GetByIdAsync(
                anchor.OriginalSectionVersionId.Value,
                cancellationToken);

            if (version is null)
                return OriginalContextResultDto.Failure(OriginalContextFailureReason.OriginalContentMissing);

            originalHtmlContent = version.HtmlContent;
            originalVersionNumber = version.VersionNumber;
            originalVersionCreatedAt = version.CreatedAt;
            isLegacyFallback = false;
        }
        else
        {
            // Legacy fallback
            if (string.IsNullOrEmpty(section.HtmlContent))
                return OriginalContextResultDto.Failure(OriginalContextFailureReason.OriginalContentMissing);

            originalHtmlContent = section.HtmlContent;
            isLegacyFallback = true;
        }

        // Build DTO
        var context = new OriginalContextDto
        {
            PassageAnchorId = anchor.Id,
            SectionId = anchor.SectionId,
            OriginalSectionVersionId = anchor.OriginalSectionVersionId,
            IsLegacyFallback = isLegacyFallback,
            OriginalSelectedText = anchor.OriginalSnapshot.SelectedText,
            NormalizedSelectedText = anchor.OriginalSnapshot.NormalizedSelectedText,
            PrefixContext = anchor.OriginalSnapshot.PrefixContext,
            SuffixContext = anchor.OriginalSnapshot.SuffixContext,
            StartOffset = anchor.OriginalSnapshot.StartOffset,
            EndOffset = anchor.OriginalSnapshot.EndOffset,
            OriginalHtmlContent = originalHtmlContent,
            OriginalVersionLabel = originalVersionNumber.HasValue ? $"v{originalVersionNumber.Value}" : null,
            OriginalVersionNumber = originalVersionNumber,
            OriginalVersionCreatedAtUtc = originalVersionCreatedAt
        };

        return OriginalContextResultDto.Success(context);
    }

    private async Task<bool> TryAuthorizeAsync(Section section, Guid requestingUserId, CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(requestingUserId, ct);
        if (user is null)
            return false;

        // Author and system support have full access
        if (authFacade.IsAuthor() || authFacade.IsSystemSupport())
            return true;

        // Beta readers must have active access to published sections
        if (!authFacade.IsBetaReader())
            return false;

        if (!section.IsPublished)
            return false;

        var access = await readerAccessRepo.GetByReaderAndProjectAsync(requestingUserId, section.ProjectId, ct);
        return access is not null && access.IsActive;
    }
}
