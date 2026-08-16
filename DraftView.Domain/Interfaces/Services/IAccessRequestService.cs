namespace DraftView.Domain.Interfaces.Services;

public interface IAccessRequestService
{
    Task SubmitRequestAsync(Guid readerId, Guid projectId, string? coverNote, string? contactEmail, CancellationToken ct = default);
    Task ApproveRequestAsync(Guid requestId, Guid authorId, CancellationToken ct = default);
    Task DeclineRequestAsync(Guid requestId, Guid authorId, CancellationToken ct = default);
    Task BulkDeclineOnRevokeAsync(Guid projectId, CancellationToken ct = default);
}
