using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Provides reader management operations for the author's Readers management page,
/// including summary listing, access management, and reader removal.
/// </summary>
public class ReaderManagementService(
    IUserRepository userRepository,
    IInvitationRepository invitationRepository,
    IProjectRepository projectRepository,
    IReaderAccessRepository readerAccessRepository,
    IUserService userService,
    IUnitOfWork unitOfWork) : IReaderManagementService
{
    /// <summary>
    /// Returns all non-soft-deleted beta readers with their derived lifecycle status,
    /// ordered alphabetically by display name. Readers without a display name are
    /// shown as "Pending reader".
    /// </summary>
    public async Task<IReadOnlyList<ReaderSummaryRow>> GetReaderSummaryAsync(CancellationToken ct = default)
    {
        var readers = await userRepository.GetAllBetaReadersAsync(ct);
        var rows = new List<ReaderSummaryRow>();

        foreach (var r in readers.Where(r => !r.IsSoftDeleted))
        {
            var pending = await invitationRepository.GetPendingByUserIdAsync(r.Id, ct);
            var hasPending = pending.Count > 0;

            var status = r.IsActive
                ? ReaderStatus.Active
                : hasPending
                    ? ReaderStatus.Invited
                    : ReaderStatus.Inactive;

            rows.Add(new ReaderSummaryRow(
                Id: r.Id,
                DisplayName: string.IsNullOrWhiteSpace(r.DisplayName) ? "Pending reader" : r.DisplayName,
                Status: status,
                ActivatedAt: r.ActivatedAt,
                HasPendingInvitation: hasPending));
        }

        return [.. rows.OrderBy(r => r.DisplayName)];
    }

    /// <summary>
    /// Returns the reader access data for the ManageReaderAccess page, including the
    /// reader's derived status and the partition of non-deleted projects into those with
    /// and without access. Returns null when the reader does not exist.
    /// </summary>
    public async Task<ReaderAccessData?> GetReaderAccessDataAsync(
        Guid readerId, CancellationToken ct = default)
    {
        var reader = await userRepository.GetByIdAsync(readerId, ct);
        if (reader is null) return null;

        var allProjects   = await projectRepository.GetAllAsync(ct);
        var activeProjects = allProjects.Where(p => !p.IsSoftDeleted).ToList();

        var accessRecords    = await readerAccessRepository.GetByReaderIdAsync(readerId, ct);
        var accessProjectIds = accessRecords.Select(a => a.ProjectId).ToHashSet();

        var invitation = await invitationRepository.GetByUserIdAsync(readerId, ct);
        var isPending  = invitation is not null && invitation.Status == InvitationStatus.Pending;
        var status     = reader.IsActive
            ? ReaderStatus.Active
            : isPending ? ReaderStatus.Invited : ReaderStatus.Inactive;

        return new ReaderAccessData
        {
            ReaderId              = reader.Id,
            DisplayName           = reader.DisplayName,
            Status                = status,
            ProjectsWithAccess    = [.. activeProjects.Where(p => accessProjectIds.Contains(p.Id))],
            ProjectsWithoutAccess = [.. activeProjects.Where(p => !accessProjectIds.Contains(p.Id))]
        };
    }

    /// <summary>
    /// Grants access to projects in <paramref name="grantIds"/>, reinstating revoked
    /// records when they exist, and revokes access to projects in
    /// <paramref name="revokeIds"/>. Saves all changes in a single unit of work.
    /// </summary>
    public async Task UpdateReaderAccessAsync(
        Guid readerId, IReadOnlyList<Guid> grantIds, IReadOnlyList<Guid> revokeIds,
        Guid authorId, CancellationToken ct = default)
    {
        foreach (var projectId in grantIds)
        {
            var existing = await readerAccessRepository.GetByReaderAndProjectAsync(readerId, projectId, ct);
            if (existing is null)
            {
                var access = ReaderAccess.Grant(readerId, authorId, projectId);
                await readerAccessRepository.AddAsync(access, ct);
            }
            else if (!existing.IsActive)
            {
                existing.Reinstate();
            }
        }

        foreach (var projectId in revokeIds)
        {
            var existing = await readerAccessRepository.GetByReaderAndProjectAsync(readerId, projectId, ct);
            existing?.Revoke();
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Revokes all project access for the reader then soft-deletes the reader record.
    /// </summary>
    public async Task SoftDeleteReaderAsync(Guid userId, Guid authorId, CancellationToken ct = default)
    {
        await readerAccessRepository.RevokeAllForReaderAsync(userId, authorId, ct);
        await userService.SoftDeleteUserAsync(userId, authorId, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
