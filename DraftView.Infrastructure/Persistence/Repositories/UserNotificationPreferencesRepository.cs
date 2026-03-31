using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Infrastructure.Persistence;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class UserNotificationPreferencesRepository(DraftViewDbContext db) : IUserNotificationPreferencesRepository
{
    public async Task<UserNotificationPreferences?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public async Task AddAsync(UserNotificationPreferences preferences, CancellationToken ct = default) =>
        await db.NotificationPreferences.AddAsync(preferences, ct);
}
