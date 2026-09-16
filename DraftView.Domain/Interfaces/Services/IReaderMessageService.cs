using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

public interface IReaderMessageService
{
    Task SendAsync(Guid authorId, Guid recipientId, string subject, string body, CancellationToken ct = default);
    Task<IReadOnlyList<ReaderMessage>> GetSentByAuthorAsync(Guid authorId, CancellationToken ct = default);
}
