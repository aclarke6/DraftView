using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Notifications;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Infrastructure.Persistence;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class CommentRepository(DraftViewDbContext db) : ICommentRepository
{
    public async Task<Comment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Comments.FindAsync([id], ct);

    public async Task<IReadOnlyList<Comment>> GetRootsBySectionIdAsync(Guid sectionId, CancellationToken ct = default) =>
        await db.Comments
            .Where(c => c.SectionId == sectionId && c.ParentCommentId == null)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Comment>> GetAllBySectionIdAsync(Guid sectionId, CancellationToken ct = default) =>
        await db.Comments
            .Where(c => c.SectionId == sectionId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Comment>> GetRepliesByParentIdAsync(Guid parentCommentId, CancellationToken ct = default) =>
        await db.Comments
            .Where(c => c.ParentCommentId == parentCommentId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Comment>> GetByAuthorIdAsync(Guid authorId, CancellationToken ct = default) =>
        await db.Comments
            .Where(c => c.AuthorId == authorId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<int> CountBySectionIdAsync(Guid sectionId, CancellationToken ct = default) =>
        await db.Comments.CountAsync(c => c.SectionId == sectionId && !c.IsSoftDeleted, ct);

    public async Task<IReadOnlyList<Comment>> GetUnreadCommentsForAuthorAsync(
        Guid authorUserId, DateTime? since, CancellationToken ct = default)
    {
        var query = db.Comments.Where(c => c.AuthorId != authorUserId && !c.IsSoftDeleted);
        if (since.HasValue)
            query = query.Where(c => c.CreatedAt > since.Value);
        return await query.OrderByDescending(c => c.CreatedAt).Take(50).ToListAsync(ct);
    }

    public async Task AddAsync(Comment comment, CancellationToken ct = default) =>
        await db.Comments.AddAsync(comment, ct);

    public async Task<IReadOnlyList<CommentNotificationRow>> GetRecentCommentsForDashboardAsync(
        Guid authorUserId, int take, CancellationToken ct = default)
    {
        return await (
            from c in db.Comments
            join s in db.Sections on c.SectionId equals s.Id
            join u in db.AppUsers on c.AuthorId equals u.Id
            join parent in db.Comments on c.ParentCommentId equals parent.Id into parentJoin
            from parent in parentJoin.DefaultIfEmpty()
            where c.AuthorId != authorUserId && !c.IsSoftDeleted && !s.IsSoftDeleted
            orderby c.CreatedAt descending
            select new CommentNotificationRow(
                c.Id, c.SectionId, s.Title, c.AuthorId, u.DisplayName,
                c.ParentCommentId,
                parent != null ? (Guid?)parent.AuthorId : null,
                c.Body, c.CreatedAt, c.Status)
        ).Take(take).ToListAsync(ct);
    }
}
