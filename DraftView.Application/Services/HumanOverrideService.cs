using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Evaluates reject and relink permissions for passage anchors.
/// </summary>
public sealed class HumanOverrideService(
    IPassageAnchorRepository anchorRepo,
    ISectionRepository sectionRepo,
    ICommentRepository commentRepo,
    IProjectRepository projectRepo,
    IUserRepository userRepo) : IHumanOverrideService
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
    /// Verifies that the caller is either the comment owner for the anchor or the
    /// author of the project that owns the anchor's section.
    /// </summary>
    private async Task EnsureCanOverrideAsync(
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
            return;

        var project = await projectRepo.GetByIdAsync(section.ProjectId, ct)
            ?? throw new EntityNotFoundException(nameof(Project), section.ProjectId);

        if (project.AuthorId == currentUserId)
            return;

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
}
