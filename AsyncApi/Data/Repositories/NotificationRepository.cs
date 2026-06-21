using AsyncApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsyncApi.Data.Repositories;

public class NotificationRepository(AsyncApiDbContext db)
{
    public async Task<List<Notification>> GetByUserAsync(Guid userId, int page = 1, int pageSize = 20) =>
        await db.Notifications
            .Where(n => n.UserId == userId && n.Active)
            .OrderByDescending(n => n.CreateDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<int> GetUnreadCountAsync(Guid userId) =>
        await db.Notifications.CountAsync(n => n.UserId == userId && n.Active && !n.IsRead);

    public async Task<bool> MarkReadAsync(Guid id, Guid userId)
    {
        var n = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId && n.Active);
        if (n is null) return false;
        n.IsRead       = true;
        n.ModifyUserId = userId;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task MarkAllReadAsync(Guid userId) =>
        await db.Notifications
            .Where(n => n.UserId == userId && n.Active && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead,      true)
                .SetProperty(n => n.ModifyUserId, userId));
}
