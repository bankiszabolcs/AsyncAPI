using System.Security.Claims;
using AsyncApi.Data.Repositories;
using AsyncApi.Models;
using AsyncApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AsyncApi.Controllers;

[ApiController]
[Route("playlists")]
[Authorize]
public sealed class PlaylistsController(
    PlaylistRepository playlistRepository,
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

    // GET /playlists — saját lejátszási listák
    [HttpGet]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> GetMyPlaylists()
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var playlists = await playlistRepository.GetAllByUserIdAsync(userId);

        return Ok(playlists.Select(p => new
        {
            id           = p.Id,
            title        = p.Title,
            description  = p.Description,
            visibilityId = p.VisibilityId,
            videoCount   = p.PlaylistVideos.Count(pv => pv.Active),
            createdAt    = p.CreateDate
        }));
    }

    // POST /playlists — új lejátszási lista
    [HttpPost]
    [EnableRateLimiting("playlist")]
    public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistRequest request)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var playlist = await playlistRepository.CreateAsync(
            userId, request.Title, request.Description, request.VisibilityId);

        return CreatedAtAction(nameof(GetPlaylist), new { id = playlist.Id }, new
        {
            id           = playlist.Id,
            title        = playlist.Title,
            description  = playlist.Description,
            visibilityId = playlist.VisibilityId,
            videoCount   = 0,
            createdAt    = playlist.CreateDate
        });
    }

    // GET /playlists/{id} — playlist részletek videókkal
    [HttpGet("{id:guid}")]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> GetPlaylist(Guid id)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var playlist = await playlistRepository.GetByIdAsync(id, userId);
        if (playlist is null) return NotFound();

        return Ok(new
        {
            id           = playlist.Id,
            title        = playlist.Title,
            description  = playlist.Description,
            visibilityId = playlist.VisibilityId,
            createdAt    = playlist.CreateDate,
            items        = playlist.PlaylistVideos
                .Where(pv => pv.Active && pv.Video.Active)
                .OrderBy(pv => pv.Position)
                .Select(pv => new
                {
                    videoId  = pv.VideoId,
                    position = pv.Position,
                    video    = new
                    {
                        id         = pv.Video.Id,
                        title      = pv.Video.Title,
                        duration   = pv.Video.DurationSeconds,
                        thumbnails = ImageService.ThumbnailWidths.Select(w => new
                        {
                            width = w,
                            url   = storageService.GetPublicUrl(
                                $"{pv.Video.Id}/{pv.Video.Id}_thumb_w{w}.jpg", StorageBucket.Images)
                        })
                    }
                })
        });
    }

    // PUT /playlists/{id} — cím, leírás, láthatóság frissítése
    [HttpPut("{id:guid}")]
    [EnableRateLimiting("playlist")]
    public async Task<IActionResult> UpdatePlaylist(Guid id, [FromBody] UpdatePlaylistRequest request)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var playlist = await playlistRepository.UpdateAsync(
            id, userId, request.Title, request.Description, request.VisibilityId);
        if (playlist is null) return NotFound();

        return Ok(new
        {
            id           = playlist.Id,
            title        = playlist.Title,
            description  = playlist.Description,
            visibilityId = playlist.VisibilityId
        });
    }

    // DELETE /playlists/{id} — törlés
    [HttpDelete("{id:guid}")]
    [EnableRateLimiting("playlist")]
    public async Task<IActionResult> DeletePlaylist(Guid id)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var deleted = await playlistRepository.DeleteAsync(id, userId);
        return deleted ? NoContent() : NotFound();
    }

    // POST /playlists/{id}/videos — videó hozzáadása
    [HttpPost("{id:guid}/videos")]
    [EnableRateLimiting("playlist")]
    public async Task<IActionResult> AddVideo(Guid id, [FromBody] AddVideoToPlaylistRequest request)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var item = await playlistRepository.AddVideoAsync(id, userId, request.VideoId);
        if (item is null) return NotFound();

        return Ok(new { videoId = item.VideoId, position = item.Position });
    }

    // DELETE /playlists/{id}/videos/{videoId} — videó eltávolítása
    [HttpDelete("{id:guid}/videos/{videoId:guid}")]
    [EnableRateLimiting("playlist")]
    public async Task<IActionResult> RemoveVideo(Guid id, Guid videoId)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var removed = await playlistRepository.RemoveVideoAsync(id, userId, videoId);
        return removed ? NoContent() : NotFound();
    }

    // PATCH /playlists/{id}/videos/{videoId} — pozíció módosítása
    [HttpPatch("{id:guid}/videos/{videoId:guid}")]
    [EnableRateLimiting("playlist")]
    public async Task<IActionResult> UpdateVideoPosition(Guid id, Guid videoId,
        [FromBody] UpdatePlaylistVideoPositionRequest request)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var updated = await playlistRepository.UpdateVideoPositionAsync(id, userId, videoId, request.Position);
        return updated ? NoContent() : NotFound();
    }
}
