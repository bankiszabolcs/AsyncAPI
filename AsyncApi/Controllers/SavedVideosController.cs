using System.Security.Claims;
using AsyncApi.Data.Repositories;
using AsyncApi.Enums;
using AsyncApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AsyncApi.Controllers;

[ApiController]
[Route("saved-videos")]
[Authorize]
public sealed class SavedVideosController(
    SavedVideoRepository savedVideoRepository,
    StorageService storageService) : ControllerBase
{
    private Guid? CurrentUserId
    {
        get
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    // GET /saved-videos — mentett videók listája
    [HttpGet]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> GetSavedVideos()
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var saved = await savedVideoRepository.GetByUserIdAsync(userId);

        return Ok(saved.Select(sv => new
        {
            videoId = sv.VideoId,
            savedAt = sv.CreateDate,
            video   = new
            {
                id          = sv.Video.Id,
                title       = sv.Video.Title,
                duration    = sv.Video.DurationSeconds,
                publishedAt = sv.Video.PublishedAt,
                viewCount   = sv.Video.ViewCount,
                author      = new
                {
                    id        = sv.Video.User.Id,
                    name      = sv.Video.User.DisplayName ?? sv.Video.User.Username,
                    avatarUrl = sv.Video.User.AvatarImage is { Extension: var ext }
                        ? storageService.GetPublicUrl(
                            $"{sv.Video.User.AvatarImageId}/{sv.Video.User.AvatarImageId}_w128{ext}",
                            StorageBucket.Images)
                        : null
                },
                media = new
                {
                    hoverStream = storageService.GetPublicUrl($"{sv.Video.Id}/480p/index.m3u8", StorageBucket.Videos),
                    preview     = storageService.GetPublicUrl($"{sv.Video.Id}/sprite.vtt",      StorageBucket.Videos),
                    thumbnails  = ImageService.ThumbnailWidths.Select(w => new
                    {
                        width = w,
                        url   = storageService.GetPublicUrl(
                            $"{sv.Video.Id}/{sv.Video.Id}_thumb_w{w}.jpg", StorageBucket.Images)
                    })
                }
            }
        }));
    }

    // GET /saved-videos/{videoId}/status — be van-e mentve
    [HttpGet("{videoId:guid}/status")]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> GetStatus(Guid videoId)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var isSaved = await savedVideoRepository.IsSavedAsync(userId, videoId);
        return Ok(new { isSaved });
    }

    // POST /saved-videos/{videoId} — mentés
    [HttpPost("{videoId:guid}")]
    [EnableRateLimiting("playlist")]
    public async Task<IActionResult> Save(Guid videoId)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var result = await savedVideoRepository.SaveAsync(userId, videoId);
        if (result is null) return NotFound();

        return Ok(new { isSaved = true });
    }

    // DELETE /saved-videos/{videoId} — eltávolítás
    [HttpDelete("{videoId:guid}")]
    [EnableRateLimiting("playlist")]
    public async Task<IActionResult> Unsave(Guid videoId)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var removed = await savedVideoRepository.UnsaveAsync(userId, videoId);
        return removed ? Ok(new { isSaved = false }) : NotFound();
    }
}
