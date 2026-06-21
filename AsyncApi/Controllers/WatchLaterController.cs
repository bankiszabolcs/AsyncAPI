using System.Security.Claims;
using AsyncApi.Data.Repositories;
using AsyncApi.Enums;
using AsyncApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AsyncApi.Controllers;

[ApiController]
[Route("watch-later")]
[Authorize]
public sealed class WatchLaterController(
    WatchLaterRepository watchLaterRepository,
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

    // GET /watch-later
    [HttpGet]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> GetWatchLater()
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var items = await watchLaterRepository.GetByUserIdAsync(userId);

        return Ok(items.Select(wl => new
        {
            videoId = wl.VideoId,
            addedAt = wl.CreateDate,
            video   = new
            {
                id          = wl.Video.Id,
                title       = wl.Video.Title,
                duration    = wl.Video.DurationSeconds,
                publishedAt = wl.Video.PublishedAt,
                viewCount   = wl.Video.ViewCount,
                author      = new
                {
                    id        = wl.Video.User.Id,
                    name      = wl.Video.User.DisplayName ?? wl.Video.User.Username,
                    avatarUrl = wl.Video.User.AvatarImage is { Extension: var ext }
                        ? storageService.GetPublicUrl(
                            $"{wl.Video.User.AvatarImageId}/{wl.Video.User.AvatarImageId}_w128{ext}",
                            StorageBucket.Images)
                        : null
                },
                media = new
                {
                    hoverStream = storageService.GetPublicUrl($"{wl.Video.Id}/480p/index.m3u8", StorageBucket.Videos),
                    preview     = storageService.GetPublicUrl($"{wl.Video.Id}/sprite.vtt",      StorageBucket.Videos),
                    thumbnails  = ImageService.ThumbnailWidths.Select(w => new
                    {
                        width = w,
                        url   = storageService.GetPublicUrl(
                            $"{wl.Video.Id}/{wl.Video.Id}_thumb_w{w}.jpg", StorageBucket.Images)
                    })
                }
            }
        }));
    }

    // GET /watch-later/{videoId}/status
    [HttpGet("{videoId:guid}/status")]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> GetStatus(Guid videoId)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var isAdded = await watchLaterRepository.IsAddedAsync(userId, videoId);
        return Ok(new { isAdded });
    }

    // POST /watch-later/{videoId}
    [HttpPost("{videoId:guid}")]
    [EnableRateLimiting("playlist")]
    public async Task<IActionResult> Add(Guid videoId)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var result = await watchLaterRepository.AddAsync(userId, videoId);
        if (result is null) return NotFound();

        return Ok(new { isAdded = true });
    }

    // DELETE /watch-later/{videoId}
    [HttpDelete("{videoId:guid}")]
    [EnableRateLimiting("playlist")]
    public async Task<IActionResult> Remove(Guid videoId)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var removed = await watchLaterRepository.RemoveAsync(userId, videoId);
        return removed ? Ok(new { isAdded = false }) : NotFound();
    }
}
