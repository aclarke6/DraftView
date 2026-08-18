using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Builds the aggregated reader summary for the author's Readers management page,
/// resolving each reader's derived status from invitation and activation state.
/// </summary>
public class ReaderManagementService(
    IUserRepository userRepository,
    IInvitationRepository invitationRepository) : IReaderManagementService
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
}
