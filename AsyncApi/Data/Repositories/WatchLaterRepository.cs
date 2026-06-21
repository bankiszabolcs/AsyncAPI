using AsyncApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsyncApi.Data.Repositories;

public class WatchLaterRepository(AsyncApiDbContext db)
{
    public async Task<List<WatchLater>> GetByUserIdAsync(Guid userId) =>
        await db.WatchLaterVideos
            .Include(wl => wl.Video)
                .ThenInclude(v => v.User)
                    .ThenInclude(u => u.AvatarImage)
            .Where(wl => wl.UserId == userId && wl.Active && wl.Video.Active)
            .OrderByDescending(wl => wl.CreateDate)
            .ToListAsync();

    public async Task<bool> IsAddedAsync(Guid userId, Guid videoId) =>
        await db.WatchLaterVideos.AnyAsync(wl => wl.UserId == userId && wl.VideoId == videoId && wl.Active);

    public async Task<WatchLater?> AddAsync(Guid userId, Guid videoId)
    {
        var existing = await db.WatchLaterVideos
            .FirstOrDefaultAsync(wl => wl.UserId == userId && wl.VideoId == videoId);

        if (existing is not null)
        {
            if (existing.Active) return existing;
            existing.Active       = true;
            existing.ModifyUserId = userId;
            existing.ModifyDate   = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return existing;
        }

        var videoExists = await db.Videos.AnyAsync(v => v.Id == videoId && v.Active);
        if (!videoExists) return null;

        var entry = new WatchLater
        {
            UserId       = userId,
            VideoId      = videoId,
            CreateUserId = userId,
            CreateDate   = DateTime.UtcNow,
            Active       = true,
            Version      = 1
        };
        db.WatchLaterVideos.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    public async Task<bool> RemoveAsync(Guid userId, Guid videoId)
    {
        var entry = await db.WatchLaterVideos
            .FirstOrDefaultAsync(wl => wl.UserId == userId && wl.VideoId == videoId && wl.Active);
        if (entry is null) return false;

        entry.Active       = false;
        entry.ModifyUserId = userId;
        entry.ModifyDate   = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }
}
