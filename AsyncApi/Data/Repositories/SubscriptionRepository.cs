using AsyncApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsyncApi.Data.Repositories;

public class SubscriptionRepository(AsyncApiDbContext db)
{
    public async Task<bool> IsSubscribedAsync(Guid subscriberId, Guid channelId) =>
        await db.Subscriptions
            .AnyAsync(s => s.SubscriberId == subscriberId && s.ChannelId == channelId && s.Active);

    public async Task<long> GetSubscriberCountAsync(Guid channelId) =>
        await db.Subscriptions.CountAsync(s => s.ChannelId == channelId && s.Active);

    // Subscribe: upsert — ha volt inaktív sor, visszaaktiválja
    public async Task<long> SubscribeAsync(Guid subscriberId, Guid channelId)
    {
        var existing = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.SubscriberId == subscriberId && s.ChannelId == channelId);

        if (existing is null)
        {
            db.Subscriptions.Add(new Subscription
            {
                SubscriberId   = subscriberId,
                ChannelId      = channelId,
                Active         = true,
                CreateDate     = DateTime.UtcNow,
                CreateUserId   = subscriberId,
                ModifyUserId   = subscriberId,
            });
        }
        else if (!existing.Active)
        {
            existing.Active       = true;
            existing.ModifyDate   = DateTime.UtcNow;
            existing.ModifyUserId = subscriberId;
        }

        await db.SaveChangesAsync();
        return await GetSubscriberCountAsync(channelId);
    }

    // Unsubscribe: soft delete
    public async Task<long?> UnsubscribeAsync(Guid subscriberId, Guid channelId)
    {
        var sub = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.SubscriberId == subscriberId && s.ChannelId == channelId && s.Active);

        if (sub is null) return null;

        sub.Active       = false;
        sub.ModifyDate   = DateTime.UtcNow;
        sub.ModifyUserId = subscriberId;
        await db.SaveChangesAsync();
        return await GetSubscriberCountAsync(channelId);
    }

    // A bejelentkezett user által követett csatornák, legújabb feliratkozás elöl
    public async Task<List<(User Channel, long SubscriberCount)>> GetMySubscriptionsAsync(Guid subscriberId)
    {
        var subs = await db.Subscriptions
            .Include(s => s.Channel).ThenInclude(u => u.AvatarImage)
            .Where(s => s.SubscriberId == subscriberId && s.Active)
            .OrderByDescending(s => s.CreateDate)
            .ToListAsync();

        var result = new List<(User, long)>();
        foreach (var s in subs)
        {
            var count = await GetSubscriberCountAsync(s.ChannelId);
            result.Add((s.Channel, count));
        }

        return result;
    }
}
