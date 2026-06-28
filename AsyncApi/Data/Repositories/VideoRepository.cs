using AsyncApi.Data.Entities;
using AsyncApi.Models;
using Microsoft.EntityFrameworkCore;
using ProcessingStatus = AsyncApi.Enums.ProcessingStatus;
using ReactionType = AsyncApi.Enums.ReactionType;
using Visibility = AsyncApi.Enums.Visibility;

namespace AsyncApi.Data.Repositories;

public class VideoRepository(AsyncApiDbContext db, IConfiguration configuration)
{
    private readonly Guid _technicalUserId =
        Guid.Parse(configuration["TechnicalUser:Id"]
            ?? throw new InvalidOperationException("TechnicalUser:Id is not configured."));
    public async Task<Video> CreateAsync(Guid id, string originalFileName, Guid userId, long fileSizeBytes)
    {
        var video = new Video
        {
            Id               = id,
            UserId           = userId,
            Title            = originalFileName,
            OriginalFileName = originalFileName,
            FileSizeBytes    = fileSizeBytes,
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

    public async Task<long> GetStorageUsedBytesAsync(Guid userId) =>
        await db.Videos
            .Where(v => v.UserId == userId && v.Active)
            .SumAsync(v => v.FileSizeBytes);

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
    // null Video: a videó nem létezik, vagy nem a megadott useré.
    // JustPublished = true: most jelent meg először nyilvánosan (PublishedAt most lett beállítva).
    public async Task<(Video? Video, bool JustPublished)> UpdateDetailsAsync(
        Guid id, Guid userId, string title, string? description, int visibilityId, Guid? categoryId)
    {
        var video = await db.Videos
            .Include(v => v.Status)
            .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId && v.Active);
        if (video is null) return (null, false);

        var wasPublished   = video.PublishedAt.HasValue;

        video.Title        = title;
        video.Description  = description;
        video.VisibilityId = visibilityId;
        video.CategoryId   = categoryId;
        video.PublishedAt  = !wasPublished && visibilityId == (int)Visibility.Public
            ? DateTime.UtcNow
            : video.PublishedAt;
        video.ModifyUserId = userId;

        await db.SaveChangesAsync();

        var justPublished = !wasPublished && video.PublishedAt.HasValue;
        return (video, justPublished);
    }

    public async Task<Video?> GetByIdAsync(Guid id)
    {
        return await db.Videos
            .Include(v => v.User).ThenInclude(u => u.AvatarImage)
            .Include(v => v.Status)
            .Include(v => v.ThumbnailImage)
            .Include(v => v.VideoTags)
                .ThenInclude(vt => vt.Tag)
            .FirstOrDefaultAsync(v => v.Id == id && v.Active);
    }

    // Csak kész és publikus videók, legújabb elöl; opcionálisan kategória vagy csatorna szerint szűrve
    public async Task<List<Video>> GetListAsync(
        int page = 1, int pageSize = 20, Guid? categoryId = null, Guid? channelId = null)
    {
        return await db.Videos
            .Include(v => v.User).ThenInclude(u => u.AvatarImage)
            .Include(v => v.ThumbnailImage)
            .Where(v => v.Active
                     && v.StatusId     == (int)ProcessingStatus.Completed
                     && v.VisibilityId == (int)Visibility.Public
                     && (categoryId == null || v.CategoryId == categoryId)
                     && (channelId  == null || v.UserId     == channelId))
            .OrderByDescending(v => v.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    // % és _ karakterek escape-elése LIKE/ILIKE pattern előtt — ezek wildcard karakterek,
    // felhasználói bemenetből értelmezve lassú DB lekérdezést okozhatnak
    private static string EscapeLikePattern(string query) =>
        query.Replace("\\", "\\\\")
             .Replace("%",  "\\%")
             .Replace("_",  "\\_");

    public async Task<List<string>> GetTitleSuggestionsAsync(string query, int limit = 8)
    {
        var pattern = $"%{EscapeLikePattern(query)}%";
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
        var pattern = $"%{EscapeLikePattern(query)}%";
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
            .Include(v => v.VideoTags).ThenInclude(vt => vt.Tag)
            .Where(v => v.UserId == userId && v.Active)
            .OrderByDescending(v => v.CreateDate)
            .ToListAsync();
    }

    // Tag teljes csere egy videóhoz (csak a tulajdonos hívhatja).
    // Stratégia: meglévő VideoTag sorok soft-delete / reaktiválás, hiányzók insert.
    public async Task UpdateTagsAsync(Guid videoId, Guid userId, List<string> tagNames)
    {
        var normalized = tagNames
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0 && t.Length <= 20 && !t.Contains(' '))
            .Distinct()
            .Take(10)
            .ToList();

        var existingVts = await db.VideoTags
            .Where(vt => vt.VideoId == videoId)
            .ToListAsync();

        // Upsert Tag rekordok az új névlistához
        var tagsForVideo = new List<Tag>();
        foreach (var name in normalized)
        {
            var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == name);
            if (tag is null)
            {
                tag = new Tag { Id = Guid.NewGuid(), Name = name, Active = true, CreateDate = DateTime.UtcNow, CreateUserId = userId };
                db.Tags.Add(tag);
                await db.SaveChangesAsync();
            }
            tagsForVideo.Add(tag);
        }

        var newTagIds = tagsForVideo.Select(t => t.Id).ToHashSet();

        // Soft-delete a kivezetett tagekhez
        foreach (var vt in existingVts.Where(vt => !newTagIds.Contains(vt.TagId)))
        {
            vt.Active = false;
            vt.ModifyDate = DateTime.UtcNow;
            vt.ModifyUserId = userId;
        }

        // Reaktiválás vagy új sor
        foreach (var tag in tagsForVideo)
        {
            var existing = existingVts.FirstOrDefault(vt => vt.TagId == tag.Id);
            if (existing is not null)
            {
                existing.Active = true;
                existing.ModifyDate = DateTime.UtcNow;
                existing.ModifyUserId = userId;
            }
            else
            {
                db.VideoTags.Add(new VideoTag
                {
                    VideoId = videoId, TagId = tag.Id, Active = true,
                    CreateDate = DateTime.UtcNow, CreateUserId = userId
                });
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<RelatedVideoResult>> GetRelatedAsync(Guid videoId, Guid? userId, int limit = 10)
    {
        return await db.GetRelatedVideos(videoId, userId, limit).ToListAsync();
    }

    public async Task<List<Video>> GetByIdsAsync(List<Guid> ids)
    {
        var videos = await db.Videos
            .Include(v => v.User).ThenInclude(u => u.AvatarImage)
            .Include(v => v.ThumbnailImage)
            .Where(v => ids.Contains(v.Id) && v.Active)
            .ToListAsync();

        return ids
            .Select(id => videos.FirstOrDefault(v => v.Id == id))
            .OfType<Video>()
            .ToList();
    }

    public async Task IncrementViewCountAsync(Guid id)
    {
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE videos SET view_count = view_count + 1, modify_user_id = {1} WHERE id = {0}",
            id, _technicalUserId);
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
