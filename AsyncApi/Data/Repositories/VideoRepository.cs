using AsyncApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using ProcessingStatus = AsyncApi.Enums.ProcessingStatus;
using Visibility = AsyncApi.Enums.Visibility;

namespace AsyncApi.Data.Repositories;

public class VideoRepository(AsyncApiDbContext db)
{
    public async Task<Video> CreateAsync(Guid id, string originalFileName, Guid userId)
    {
        var video = new Video
        {
            Id               = id,
            UserId           = userId,
            Title            = originalFileName,
            OriginalFileName = originalFileName,
            CreateUserId     = userId,
            CreateDate       = DateTime.UtcNow,
            ModifyUserId     = userId,
            ModifyDate       = DateTime.UtcNow,
            Active           = true,
            Version          = 1
            // StatusId és VisibilityId a DB default értékét kapja (1=Queued, 3=Private)
        };

        db.Videos.Add(video);
        await db.SaveChangesAsync();

        return video;
    }

    public async Task UpdateStatusAsync(Guid id, ProcessingStatus status)
    {
        var video = await db.Videos.FindAsync(id);
        if (video is null) return;
        video.StatusId = (int)status;
        await db.SaveChangesAsync();
    }

    public async Task UpdateCompletedAsync(Guid id, int durationSeconds)
    {
        var video = await db.Videos.FindAsync(id);
        if (video is null) return;
        video.StatusId        = (int)ProcessingStatus.Completed;
        video.DurationSeconds = durationSeconds;
        await db.SaveChangesAsync();
    }

    public async Task<Video?> GetByIdAsync(Guid id)
    {
        return await db.Videos
            .Include(v => v.User)
            .Include(v => v.Status)
            .Include(v => v.ThumbnailImage)
            .Include(v => v.VideoTags)
                .ThenInclude(vt => vt.Tag)
            .FirstOrDefaultAsync(v => v.Id == id && v.Active);
    }

    // Csak kész és publikus videók, legújabb elöl
    public async Task<List<Video>> GetListAsync(int page = 1, int pageSize = 20)
    {
        return await db.Videos
            .Include(v => v.User)
            .Include(v => v.ThumbnailImage)
            .Where(v => v.Active
                     && v.StatusId     == (int)ProcessingStatus.Completed
                     && v.VisibilityId == (int)Visibility.Public)
            .OrderByDescending(v => v.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    // Saját videók — minden státusz, csak a user saját videói
    public async Task<List<Video>> GetAllByUserIdAsync(Guid userId)
    {
        return await db.Videos
            .Include(v => v.Status)
            .Where(v => v.UserId == userId && v.Active)
            .OrderByDescending(v => v.CreateDate)
            .ToListAsync();
    }

    public async Task IncrementViewCountAsync(Guid id)
    {
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE videos SET view_count = view_count + 1 WHERE id = {0}", id);
    }
}
