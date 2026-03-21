using ScrivenerSync.Domain.Entities;
using ScrivenerSync.Domain.Interfaces.Repositories;
using ScrivenerSync.Domain.Interfaces.Services;

namespace ScrivenerSync.Application.Services;

public class DashboardService(
    ISectionRepository sectionRepo,
    IUserRepository userRepo,
    IReadEventRepository readEventRepo,
    ICommentRepository commentRepo,
    IEmailDeliveryLogRepository logRepo) : IDashboardService
{
    public async Task<IReadOnlyList<Section>> GetProjectOverviewAsync(
        Guid projectId, CancellationToken ct = default) =>
        await sectionRepo.GetPublishedByProjectIdAsync(projectId, ct);

    public async Task<IReadOnlyList<User>> GetReaderSummaryAsync(
        CancellationToken ct = default) =>
        await userRepo.GetAllBetaReadersAsync(ct);

    public async Task<IReadOnlyList<EmailDeliveryLog>> GetEmailHealthSummaryAsync(
        CancellationToken ct = default) =>
        await logRepo.GetFailedAsync(ct);
}
