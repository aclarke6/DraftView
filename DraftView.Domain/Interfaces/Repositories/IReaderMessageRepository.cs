using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

public interface IReaderMessageRepository
{
    Task AddAsync(ReaderMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<ReaderMessage>> GetByAuthorIdAsync(Guid authorId, CancellationToken ct = default);
}
