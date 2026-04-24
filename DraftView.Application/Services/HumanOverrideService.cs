using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.ValueObjects;

namespace DraftView.Application.Services;

/// <summary>
/// Evaluates and applies human reject/relink overrides for passage anchors.
/// </summary>
public sealed class HumanOverrideService(
    IPassageAnchorRepository anchorRepo,
    ISectionRepository sectionRepo,
    ICommentRepository commentRepo,
    IProjectRepository projectRepo,
    IUserRepository userRepo,
    IPassageAnchorService passageAnchorService,
    IUnitOfWork unitOfWork) : IHumanOverrideService
{
    public Task EnsureCanRejectAsync(
        Guid anchorId,
        Guid currentUserId,
        CancellationToken ct = default) =>
        EnsureCanOverrideAsync(anchorId, currentUserId, ct);

    public Task EnsureCanRelinkAsync(
        Guid anchorId,
        Guid currentUserId,
        CancellationToken ct = default) =>
        EnsureCanOverrideAsync(anchorId, currentUserId, ct);

    /// <summary>
    /// Records a human rejection of the current active match and persists the audit state.
    /// </summary>
    public async Task<PassageAnchorDto> RejectAsync(
        Guid anchorId,
        Guid currentUserId,
        string? reason = null,
        CancellationToken ct = default)
    {
        var anchor = await LoadAuthorizedAnchorAsync(anchorId, currentUserId, ct);
        var currentMatch = anchor.CurrentMatch
            ?? throw new InvariantViolationException("I-ANCHOR-MATCH",
                "A current match is required before rejecting it.");

        anchor.Reject(currentMatch, currentUserId, reason);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(anchor);
    }

    /// <summary>
    /// Records a human-selected replacement match and persists the manual override.
    /// </summary>
    public async Task<PassageAnchorDto> RelinkAsync(
        Guid anchorId,
        CreatePassageAnchorRequest relinkRequest,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        await passageAnchorService.ValidateSelectionAsync(relinkRequest, currentUserId, ct);

        var anchor = await LoadAuthorizedAnchorAsync(anchorId, currentUserId, ct);
        if (relinkRequest.SectionId != anchor.SectionId)
            throw new InvariantViolationException("I-ANCHOR-SECTION",
                "Relink selection must belong to the anchor's section.");

        if (relinkRequest.Purpose != anchor.Purpose)
            throw new InvariantViolationException("I-ANCHOR-PURPOSE",
                "Relink selection must preserve the anchor purpose.");

        var match = PassageAnchorMatch.Create(
            relinkRequest.OriginalSectionVersionId,
            relinkRequest.StartOffset,
            relinkRequest.EndOffset,
            relinkRequest.SelectedText,
            100,
            PassageAnchorMatchMethod.ManualRelink,
            currentUserId);

        anchor.Relink(match, currentUserId);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(anchor);
    }

    /// <summary>
    /// Verifies that the caller is either the comment owner for the anchor or the
    /// author of the project that owns the anchor's section.
    /// </summary>
    private async Task EnsureCanOverrideAsync(
        Guid anchorId,
        Guid currentUserId,
        CancellationToken ct)
    {
        _ = await LoadAuthorizedAnchorAsync(anchorId, currentUserId, ct);
    }

    /// <summary>
    /// Loads the anchor and enforces the comment-owner or project-author rule.
    /// </summary>
    private async Task<PassageAnchor> LoadAuthorizedAnchorAsync(
        Guid anchorId,
        Guid currentUserId,
        CancellationToken ct)
    {
        _ = await userRepo.GetByIdAsync(currentUserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), currentUserId);

        var anchor = await anchorRepo.GetByIdAsync(anchorId, ct)
            ?? throw new EntityNotFoundException(nameof(PassageAnchor), anchorId);
        var section = await sectionRepo.GetByIdAsync(anchor.SectionId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), anchor.SectionId);

        if (await IsCommentOwnerAsync(anchorId, section.Id, currentUserId, ct))
            return anchor;

        var project = await projectRepo.GetByIdAsync(section.ProjectId, ct)
            ?? throw new EntityNotFoundException(nameof(Project), section.ProjectId);

        if (project.AuthorId == currentUserId)
            return anchor;

        throw new UnauthorisedOperationException(
            "Only the comment owner or project author may override a passage anchor.");
    }

    /// <summary>
    /// Checks whether the caller owns the comment linked to the anchor in the section.
    /// </summary>
    private async Task<bool> IsCommentOwnerAsync(
        Guid anchorId,
        Guid sectionId,
        Guid currentUserId,
        CancellationToken ct)
    {
        var comments = await commentRepo.GetAllBySectionIdAsync(sectionId, ct);
        return comments.Any(comment =>
            comment.PassageAnchorId == anchorId &&
            comment.AuthorId == currentUserId);
    }

    /// <summary>
    /// Maps a passage anchor aggregate to the contract returned by override operations.
    /// </summary>
    private static PassageAnchorDto Map(PassageAnchor anchor)
    {
        return new PassageAnchorDto(
            anchor.Id,
            anchor.SectionId,
            anchor.OriginalSectionVersionId,
            anchor.Purpose,
            anchor.CreatedByUserId,
            anchor.CreatedAt,
            anchor.Status,
            anchor.UpdatedAt,
            new PassageAnchorSnapshotDto(
                anchor.OriginalSnapshot.SelectedText,
                anchor.OriginalSnapshot.NormalizedSelectedText,
                anchor.OriginalSnapshot.SelectedTextHash,
                anchor.OriginalSnapshot.PrefixContext,
                anchor.OriginalSnapshot.SuffixContext,
                anchor.OriginalSnapshot.StartOffset,
                anchor.OriginalSnapshot.EndOffset,
                anchor.OriginalSnapshot.CanonicalContentHash,
                anchor.OriginalSnapshot.HtmlSelectorHint),
            anchor.CurrentMatch is null
                ? null
                : new PassageAnchorMatchDto(
                    anchor.CurrentMatch.TargetSectionVersionId,
                    anchor.CurrentMatch.StartOffset,
                    anchor.CurrentMatch.EndOffset,
                    anchor.CurrentMatch.MatchedText,
                    anchor.CurrentMatch.ConfidenceScore,
                    anchor.CurrentMatch.MatchMethod,
                    anchor.CurrentMatch.ResolvedAt,
                    anchor.CurrentMatch.ResolvedByUserId,
                    anchor.CurrentMatch.Reason),
            anchor.Rejection is null
                ? null
                : new PassageAnchorRejectionDto(
                    anchor.Rejection.TargetSectionVersionId,
                    anchor.Rejection.RejectedByUserId,
                    anchor.Rejection.RejectedAt,
                    anchor.Rejection.Reason));
    }
}
