using AsyncApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsyncApi.Data.Repositories;

public class PlaylistRepository(AsyncApiDbContext db)
{
    public async Task<List<Playlist>> GetAllByUserIdAsync(Guid userId) =>
        await db.Playlists
            .Include(p => p.PlaylistVideos)
            .Where(p => p.UserId == userId && p.Active)
            .OrderByDescending(p => p.CreateDate)
            .ToListAsync();

    public async Task<Playlist?> GetByIdAsync(Guid id, Guid userId) =>
        await db.Playlists
            .Include(p => p.PlaylistVideos.Where(pv => pv.Active))
                .ThenInclude(pv => pv.Video)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId && p.Active);

    public async Task<Playlist> CreateAsync(Guid userId, string title, string? description, int visibilityId)
    {
        var playlist = new Playlist
        {
            Id           = Guid.NewGuid(),
            UserId       = userId,
            Title        = title,
            Description  = description,
            VisibilityId = visibilityId,
            CreateUserId = userId,
            CreateDate   = DateTime.UtcNow,
            ModifyUserId = userId,
            Active       = true
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync();
        return playlist;
    }

    public async Task<Playlist?> UpdateAsync(Guid id, Guid userId, string title, string? description, int visibilityId)
    {
        var playlist = await db.Playlists
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId && p.Active);
        if (playlist is null) return null;

        playlist.Title        = title;
        playlist.Description  = description;
        playlist.VisibilityId = visibilityId;
        playlist.ModifyUserId = userId;

        await db.SaveChangesAsync();
        return playlist;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        var playlist = await db.Playlists
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId && p.Active);
        if (playlist is null) return false;

        playlist.Active      = false;
        playlist.ModifyUserId = userId;

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PlaylistVideo?> AddVideoAsync(Guid playlistId, Guid userId, Guid videoId)
    {
        var playlist = await db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == userId && p.Active);
        if (playlist is null) return null;

        var existing = await db.PlaylistVideos
            .FirstOrDefaultAsync(pv => pv.PlaylistId == playlistId && pv.VideoId == videoId);
        if (existing is not null)
        {
            if (!existing.Active)
            {
                existing.Active = true;
                await db.SaveChangesAsync();
            }
            return existing;
        }

        var maxPos = await db.PlaylistVideos
            .Where(pv => pv.PlaylistId == playlistId && pv.Active)
            .MaxAsync(pv => (int?)pv.Position) ?? 0;

        var item = new PlaylistVideo
        {
            PlaylistId   = playlistId,
            VideoId      = videoId,
            Position     = maxPos + 1,
            CreateUserId = userId,
            CreateDate   = DateTime.UtcNow,
            ModifyUserId = userId,
            Active       = true
        };

        db.PlaylistVideos.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task<bool> RemoveVideoAsync(Guid playlistId, Guid userId, Guid videoId)
    {
        var playlist = await db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == userId && p.Active);
        if (playlist is null) return false;

        var item = await db.PlaylistVideos
            .FirstOrDefaultAsync(pv => pv.PlaylistId == playlistId && pv.VideoId == videoId && pv.Active);
        if (item is null) return false;

        item.Active = false;
        item.ModifyUserId = userId;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateVideoPositionAsync(Guid playlistId, Guid userId, Guid videoId, int position)
    {
        var playlist = await db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == userId && p.Active);
        if (playlist is null) return false;

        var item = await db.PlaylistVideos
            .FirstOrDefaultAsync(pv => pv.PlaylistId == playlistId && pv.VideoId == videoId && pv.Active);
        if (item is null) return false;

        item.Position = position;
        item.ModifyUserId = userId;
        await db.SaveChangesAsync();
        return true;
    }
}
