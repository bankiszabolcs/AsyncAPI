using AsyncApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using ProcessingStatus = AsyncApi.Enums.ProcessingStatus;
using ReactionType = AsyncApi.Enums.ReactionType;
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

    // Cím / leírás / láthatóság frissítése — kizárólag a tulajdonos módosíthat.
    // null visszatérés: a videó nem létezik, vagy nem a megadott useré.
    // ModifyDate-et DB trigger állítja, itt nem írjuk.
    public async Task<Video?> UpdateDetailsAsync(
        Guid id, Guid userId, string title, string? description, int visibilityId)
    {
        var video = await db.Videos
            .Include(v => v.Status)
            .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId && v.Active);
        if (video is null) return null;

        video.Title        = title;
        video.Description  = description;
        video.VisibilityId = visibilityId;
        video.PublishedAt = video.PublishedAt is null && visibilityId == (int)Visibility.Public
            ? DateTime.UtcNow
            : video.PublishedAt;
        video.ModifyUserId = userId;

        await db.SaveChangesAsync();
        return video;
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
            .Include(v => v.User).ThenInclude(u => u.AvatarImage)
            .Include(v => v.ThumbnailImage)
            .Where(v => v.Active
                     && v.StatusId     == (int)ProcessingStatus.Completed
                     && v.VisibilityId == (int)Visibility.Public)
            .OrderByDescending(v => v.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<string>> GetTitleSuggestionsAsync(string query, int limit = 8)
    {
        var pattern = $"%{query}%";
        return await db.Videos
            .Where(v => v.Active
                     && v.StatusId     == (int)ProcessingStatus.Completed
                     && v.VisibilityId == (int)Visibility.Public
                     && EF.Functions.ILike(v.Title, pattern))
            .OrderByDescending(v => v.PublishedAt)
            .Take(limit)
            .Select(v => v.Title)
            .ToListAsync();
    }

    public async Task<List<Video>> SearchAsync(string query, int page = 1, int pageSize = 20)
    {
        var pattern = $"%{query}%";
        return await db.Videos
            .Include(v => v.User).ThenInclude(u => u.AvatarImage)
            .Include(v => v.ThumbnailImage)
            .Where(v => v.Active
                     && v.StatusId     == (int)ProcessingStatus.Completed
                     && v.VisibilityId == (int)Visibility.Public
                     && (EF.Functions.ILike(v.Title, pattern)
                         || (v.Description != null && EF.Functions.ILike(v.Description, pattern))))
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

    // Reakció hozzáadása / módosítása / visszavonása (toggle).
    // - Nincs meglévő reakció → új sor, counter +1
    // - Ugyanolyan aktív reakció → soft delete, counter -1
    // - Másik típusú aktív reakció → típus csere, mindkét counter frissül
    // - Inaktív meglévő sor → visszaaktiválás az új típussal, counter +1
    // Visszaadja az aktuális like/dislike számot.
    public async Task<int?> GetUserReactionAsync(Guid videoId, Guid userId)
    {
        var reaction = await db.VideoReactions
            .FirstOrDefaultAsync(r => r.VideoId == videoId && r.UserId == userId && r.Active);
        return reaction?.ReactionTypeId;
    }

    public async Task<(int likeCount, int dislikeCount, int? userReaction)> ReactAsync(Guid videoId, Guid userId, int reactionTypeId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var existing = await db.VideoReactions
            .FirstOrDefaultAsync(r => r.VideoId == videoId && r.UserId == userId);

        int? newUserReaction = reactionTypeId; // toggle-off esetén null-ra állítjuk

        if (existing is null)
        {
            db.VideoReactions.Add(new VideoReaction
            {
                VideoId        = videoId,
                UserId         = userId,
                ReactionTypeId = reactionTypeId,
                CreateUserId   = userId,
                CreateDate     = DateTime.UtcNow,
                ModifyUserId   = userId,
                Active         = true
            });
#pragma warning disable EF1002 // col kizárólag hardcoded string lehet, nem user input
            var col = reactionTypeId == (int)ReactionType.Like ? "like_count" : "dislike_count";
            await db.Database.ExecuteSqlRawAsync(
                $"UPDATE videos SET {col} = {col} + 1 WHERE id = {{0}}", videoId);
        }
        else if (existing.Active && existing.ReactionTypeId == reactionTypeId)
        {
            existing.Active       = false;
            existing.ModifyUserId = userId;
            newUserReaction       = null;
            var col = reactionTypeId == (int)ReactionType.Like ? "like_count" : "dislike_count";
            await db.Database.ExecuteSqlRawAsync(
                $"UPDATE videos SET {col} = {col} - 1 WHERE id = {{0}}", videoId);
        }
        else if (existing.Active && existing.ReactionTypeId != reactionTypeId)
        {
            existing.ReactionTypeId = reactionTypeId;
            existing.ModifyUserId   = userId;
            if (reactionTypeId == (int)ReactionType.Like)
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE videos SET like_count = like_count + 1, dislike_count = dislike_count - 1 WHERE id = {0}", videoId);
            else
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE videos SET like_count = like_count - 1, dislike_count = dislike_count + 1 WHERE id = {0}", videoId);
        }
        else
        {
            // Inaktív sor → visszaaktiválás
            existing.ReactionTypeId = reactionTypeId;
            existing.Active         = true;
            existing.ModifyUserId   = userId;
            var col = reactionTypeId == (int)ReactionType.Like ? "like_count" : "dislike_count";
            await db.Database.ExecuteSqlRawAsync(
                $"UPDATE videos SET {col} = {col} + 1 WHERE id = {{0}}", videoId);
        }
#pragma warning restore EF1002

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        var video = await db.Videos.AsNoTracking()
            .Select(v => new { v.Id, v.LikeCount, v.DislikeCount })
            .FirstAsync(v => v.Id == videoId);

        return (video.LikeCount, video.DislikeCount, newUserReaction);
    }
}
