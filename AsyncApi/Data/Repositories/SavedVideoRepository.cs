using AsyncApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsyncApi.Data.Repositories;

public class SavedVideoRepository(AsyncApiDbContext db)
{
    public async Task<List<SavedVideo>> GetByUserIdAsync(Guid userId) =>
        await db.SavedVideos
            .Include(sv => sv.Video)
                .ThenInclude(v => v.User)
                    .ThenInclude(u => u.AvatarImage)
            .Where(sv => sv.UserId == userId && sv.Active && sv.Video.Active)
            .OrderByDescending(sv => sv.CreateDate)
            .ToListAsync();

    public async Task<bool> IsSavedAsync(Guid userId, Guid videoId) =>
        await db.SavedVideos.AnyAsync(sv => sv.UserId == userId && sv.VideoId == videoId && sv.Active);

    public async Task<SavedVideo?> SaveAsync(Guid userId, Guid videoId)
    {
        var existing = await db.SavedVideos
            .FirstOrDefaultAsync(sv => sv.UserId == userId && sv.VideoId == videoId);

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

        var saved = new SavedVideo
        {
            UserId       = userId,
            VideoId      = videoId,
            CreateUserId = userId,
            CreateDate   = DateTime.UtcNow,
            Active       = true,
            Version      = 1
        };
        db.SavedVideos.Add(saved);
        await db.SaveChangesAsync();
        return saved;
    }

    public async Task<bool> UnsaveAsync(Guid userId, Guid videoId)
    {
        var saved = await db.SavedVideos
            .FirstOrDefaultAsync(sv => sv.UserId == userId && sv.VideoId == videoId && sv.Active);
        if (saved is null) return false;

        saved.Active       = false;
        saved.ModifyUserId = userId;
        saved.ModifyDate   = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }
}
