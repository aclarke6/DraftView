using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class ReaderMessageRepository(DraftViewDbContext db) : IReaderMessageRepository
{
    public async Task AddAsync(ReaderMessage message, CancellationToken ct = default) =>
        await db.ReaderMessages.AddAsync(message, ct);

    public Task<IReadOnlyList<ReaderMessage>> GetByAuthorIdAsync(Guid authorId, CancellationToken ct = default) =>
        db.ReaderMessages
            .Where(m => m.AuthorId == authorId)
            .OrderByDescending(m => m.SentAt)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<ReaderMessage>)t.Result, ct);
}
