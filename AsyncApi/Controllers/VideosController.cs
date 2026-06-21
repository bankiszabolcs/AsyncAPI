using System.Security.Claims;
using AsyncApi.Data.Repositories;
using AsyncApi.Enums;
using AsyncApi.Models;
using AsyncApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;

namespace AsyncApi.Controllers;

[ApiController]
[Route("videos")]
public sealed class VideosController(
    IConfiguration configuration,
    LinkGenerator linkGenerator,
    VideoService videoService,
    StorageService storageService,
    QueueService queueService,
    StatusService statusService,
    VideoRepository videoRepository,
    IConnectionMultiplexer redis)
    : ControllerBase
{
    private readonly string _uploadDirectory = configuration["UploadDirectory"] ?? "uploads";
    private readonly long _userQuotaBytes = long.Parse(configuration["Storage:UserQuotaBytes"] ?? "2147483648");

    // A bejelentkezett user Keycloak sub-ja (== DB user Id). null ha nincs/érvénytelen.
    private Guid? CurrentUserId
    {
        get
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    // POST /videos — feltölti a videót, sorba állítja a feldolgozást, visszaadja a státusz URL-t
    [HttpPost]
    [Authorize]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(2_000_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2_000_000_000)]
    public async Task<IActionResult> UploadVideo(IFormFile? file)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        if (file is null) return BadRequest("No file uploaded.");

        if (!videoService.IsValidVideo(file))
            return BadRequest("Invalid video file. Allowed formats: mp4, mov, mkv, avi, webm.");

        var usedBytes = await videoRepository.GetStorageUsedBytesAsync(userId);
        if (usedBytes + file.Length > _userQuotaBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                $"Storage quota exceeded. Used {usedBytes} of {_userQuotaBytes} bytes.");

        var id = Guid.NewGuid();
        var safeFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        var folderPath = Path.Combine(_uploadDirectory, "videos", id.ToString());
        var fileName = $"{id}{extension}";

        var originalFilePath = await videoService.SaveOriginalVideoAsync(file, folderPath, fileName);

        // DB rekord létrehozása — a tulajdonos a bejelentkezett user
        await videoRepository.CreateAsync(id, safeFileName, userId, file.Length);

        // Accept-Language alapján határozzuk meg a tagek nyelvét (pl. "hu", "en", "de")
        var language = Request.GetTypedHeaders().AcceptLanguage
            .OrderByDescending(l => l.Quality ?? 1.0)
            .Select(l => l.Value.ToString().Split('-')[0].ToLowerInvariant())
            .FirstOrDefault(l => l.Length == 2) ?? "en";

        // Job Redis Stream-be írva — korábban channel.Writer.WriteAsync(job) volt
        var job = new VideoProcessingJob(id.ToString(), originalFilePath, folderPath, language);
        await queueService.EnqueueAsync(QueueService.VideoStreamKey, job);

        // Státusz Redis-be írva — korábban statusDictionary[id] = ... volt
        await statusService.SetStatusAsync(id.ToString(), VideoProcessingStatus.Queued);

        var statusUrl = linkGenerator.GetUriByAction(HttpContext, nameof(GetStatus), "Videos", new { id })
            ?? throw new InvalidOperationException("Failed to generate URL.");

        return Accepted(statusUrl, new { id, status = VideoProcessingStatus.Queued });
    }

    // GET /videos/suggestions?q= — gyors cím-autocomplete (csak szöveg, max 8 találat)
    [HttpGet("suggestions")]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> GetSuggestions([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(Array.Empty<string>());

        if (q.Length > 100)
            return BadRequest("Search query too long.");

        var titles = await videoRepository.GetTitleSuggestionsAsync(q.Trim());
        return Ok(titles);
    }

    // GET /videos — kész és publikus videók listája lapozással; ?q= esetén cím/leírás szerinti szűréssel
    [HttpGet]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> GetVideos(
        [FromQuery] string? q = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? channelId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (q is { Length: > 100 })
            return BadRequest("Search query too long.");

        var videos = string.IsNullOrWhiteSpace(q)
            ? await videoRepository.GetListAsync(page, pageSize, categoryId, channelId)
            : await videoRepository.SearchAsync(q.Trim(), page, pageSize);

        var result = videos.Select(v => new
        {
            id          = v.Id,
            title       = v.Title,
            description = v.Description,
            duration    = v.DurationSeconds,
            publishedAt = v.PublishedAt,
            viewCount   = v.ViewCount,
            author = new
            {
                id        = v.User.Id,
                name      = v.User.DisplayName ?? v.User.Username,
                avatarUrl = v.User.AvatarImage is { Extension: var ext }
                    ? storageService.GetPublicUrl($"{v.User.AvatarImageId}/{v.User.AvatarImageId}_w128{ext}", StorageBucket.Images)
                    : null
            },
            media = new
            {
                sprite   = storageService.GetPublicUrl($"{v.Id}/sprite.jpg",         StorageBucket.Videos),
                preview  = storageService.GetPublicUrl($"{v.Id}/sprite.vtt",         StorageBucket.Videos),
                // hover preview: legalacsonyabb minőségű stream, frontenden hls.js kell hozzá
                hoverStream = storageService.GetPublicUrl($"{v.Id}/480p/index.m3u8", StorageBucket.Videos),
                thumbnails = ImageService.ThumbnailWidths.Select(w => new
                {
                    width = w,
                    url   = storageService.GetPublicUrl($"{v.Id}/{v.Id}_thumb_w{w}.jpg", StorageBucket.Images)
                })
            }
        });

        return Ok(result);
    }

    // GET /videos/my — bejelentkezett user összes videója, státusztól függetlenül
    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMyVideos()
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var videos = await videoRepository.GetAllByUserIdAsync(userId);

        var result = videos.Select(v =>
        {
            var isCompleted   = v.StatusId == (int)ProcessingStatus.Completed;
            var hasThumbnails = isCompleted || v.StatusId == (int)ProcessingStatus.Processing;

            return new
            {
                id           = v.Id,
                title        = v.Title,
                description  = v.Description,
                duration     = v.DurationSeconds,
                publishedAt  = v.PublishedAt,
                viewCount    = v.ViewCount,
                statusId     = v.StatusId,
                status       = v.Status.Title,
                visibilityId = v.VisibilityId,
                categoryId   = v.CategoryId,
                tags         = v.VideoTags.Where(vt => vt.Active).Select(vt => vt.Tag.Name).ToList(),
                media = new
                {
                    thumbnails = hasThumbnails
                        ? ImageService.ThumbnailWidths.Select(w => new
                        {
                            width = w,
                            url   = storageService.GetPublicUrl($"{v.Id}/{v.Id}_thumb_w{w}.jpg", StorageBucket.Images)
                        })
                        : null,
                    hoverStream = isCompleted
                        ? storageService.GetPublicUrl($"{v.Id}/480p/index.m3u8", StorageBucket.Videos)
                        : null,
                    preview = isCompleted
                        ? storageService.GetPublicUrl($"{v.Id}/sprite.vtt", StorageBucket.Videos)
                        : null,
                }
            };
        });

        return Ok(result);
    }

    // PUT /videos/{id} — cím / leírás / láthatóság / tagek szerkesztése (csak a tulajdonos)
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateVideo(Guid id, [FromBody] UpdateVideoRequest request)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest("Title is required.");

        if (!Enum.IsDefined(typeof(Visibility), request.VisibilityId))
            return BadRequest("Invalid visibility.");

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        var video = await videoRepository.UpdateDetailsAsync(
            id, userId, title, description, request.VisibilityId, request.CategoryId);

        if (video is null) return NotFound();

        if (request.Tags is not null)
            await videoRepository.UpdateTagsAsync(id, userId, request.Tags);

        // A mentett (normalizált) tagek kiszámítása a válaszhoz
        var savedTags = (request.Tags ?? [])
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0 && t.Length <= 20 && !t.Contains(' '))
            .Distinct()
            .Take(10)
            .ToList();

        return Ok(new
        {
            id           = video.Id,
            title        = video.Title,
            description  = video.Description,
            statusId     = video.StatusId,
            status       = video.Status.Title,
            visibilityId = video.VisibilityId,
            categoryId   = video.CategoryId,
            tags         = savedTags
        });
    }

    // GET /videos/{id} — egy videó adatai; csak a ténylegesen elérhető médiát adja vissza
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetVideo(Guid id)
    {
        var video = await videoRepository.GetByIdAsync(id);
        if (video is null) return NotFound();

        if (video.VisibilityId == (int)Visibility.Private && video.UserId != CurrentUserId)
            return NotFound();

        var isCompleted = video.StatusId == (int)ProcessingStatus.Completed;
        // A thumbnail már Processing alatt elérhető (a feldolgozás legelején feltöltjük)
        var hasThumbnails = isCompleted || video.StatusId == (int)ProcessingStatus.Processing;

        var userReaction = CurrentUserId.HasValue
            ? await videoRepository.GetUserReactionAsync(id, CurrentUserId.Value)
            : null;

        return Ok(new
        {
            id           = video.Id,
            title        = video.Title,
            description  = video.Description,
            duration     = video.DurationSeconds,
            publishedAt  = video.PublishedAt,
            statusId     = video.StatusId,
            status       = video.Status.Title,
            viewCount    = video.ViewCount,
            likeCount    = video.LikeCount,
            dislikeCount = video.DislikeCount,
            userReaction,
            author = new
            {
                id   = video.User.Id,
                name = video.User.DisplayName ?? video.User.Username
            },
            tags   = video.VideoTags.Select(vt => vt.Tag.Name),
            media = new
            {
                masterStream = isCompleted ? storageService.GetPublicUrl($"{video.Id}/master.m3u8", StorageBucket.Videos) : null,
                streams = isCompleted
                    ? VideoService.StreamQualities.Select(q => new
                    {
                        quality = q,
                        url     = storageService.GetPublicUrl($"{video.Id}/{q}/index.m3u8", StorageBucket.Videos)
                    })
                    : null,
                sprite  = isCompleted ? storageService.GetPublicUrl($"{video.Id}/sprite.jpg", StorageBucket.Videos) : null,
                preview = isCompleted ? storageService.GetPublicUrl($"{video.Id}/sprite.vtt", StorageBucket.Videos) : null,
                thumbnails = hasThumbnails
                    ? ImageService.ThumbnailWidths.Select(w => new
                    {
                        width = w,
                        url   = storageService.GetPublicUrl($"{video.Id}/{video.Id}_thumb_w{w}.jpg", StorageBucket.Images)
                    })
                    : null
            }
        });
    }

    // GET /videos/{id}/related — kapcsolódó videók score-alapú rangsorban
    [HttpGet("{id:guid}/related")]
    public async Task<IActionResult> GetRelatedVideos(Guid id, [FromQuery] int limit = 8)
    {
        var video = await videoRepository.GetByIdAsync(id);
        if (video is null) return NotFound();

        if (video.VisibilityId == (int)Visibility.Private && video.UserId != CurrentUserId)
            return Ok(Array.Empty<object>());

        var related = await videoRepository.GetRelatedAsync(id, CurrentUserId, Math.Clamp(limit, 1, 20));
        var videos  = await videoRepository.GetByIdsAsync(related.Select(r => r.VideoId).ToList());

        var result = videos.Select(v => new
        {
            id          = v.Id,
            title       = v.Title,
            description = v.Description,
            duration    = v.DurationSeconds,
            publishedAt = v.PublishedAt,
            viewCount   = v.ViewCount,
            author = new
            {
                id        = v.User.Id,
                name      = v.User.DisplayName ?? v.User.Username,
                avatarUrl = v.User.AvatarImage is { Extension: var ext }
                    ? storageService.GetPublicUrl($"{v.User.AvatarImageId}/{v.User.AvatarImageId}_w128{ext}", StorageBucket.Images)
                    : null
            },
            media = new
            {
                sprite      = storageService.GetPublicUrl($"{v.Id}/sprite.jpg",         StorageBucket.Videos),
                preview     = storageService.GetPublicUrl($"{v.Id}/sprite.vtt",         StorageBucket.Videos),
                hoverStream = storageService.GetPublicUrl($"{v.Id}/480p/index.m3u8",    StorageBucket.Videos),
                thumbnails  = ImageService.ThumbnailWidths.Select(w => new
                {
                    width = w,
                    url   = storageService.GetPublicUrl($"{v.Id}/{v.Id}_thumb_w{w}.jpg", StorageBucket.Images)
                })
            }
        });

        return Ok(result);
    }

    // POST /videos/{id}/views — megtekintés rögzítése; Redis NX+EX deduplikáció, majd Stream-be kerül
    // Azonosító: bejelentkezett usernél userId, anonimnak X-Session-Id header (frontend generálja)
    [HttpPost("{id:guid}/views")]
    public async Task<IActionResult> RecordView(
        Guid id,
        [FromHeader(Name = "X-Session-Id")] string? sessionId)
    {
        if (CurrentUserId is null && !Guid.TryParse(sessionId, out _))
            return BadRequest("X-Session-Id header is required and must be a valid UUID for anonymous views.");

        var clientIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault()
                       ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                       ?? "unknown";

        var identifier = CurrentUserId.HasValue
            ? CurrentUserId.Value.ToString()
            : $"{clientIp}:{sessionId}";

        var db = redis.GetDatabase();
        var key = $"view:{id}:{identifier}";
        var isNew = await db.StringSetAsync(key, "1", TimeSpan.FromHours(1), When.NotExists);

        if (isNew)
            await queueService.EnqueueAsync(QueueService.ViewStreamKey, new ViewRecord(id));

        return NoContent();
    }

    // POST /videos/{id}/reactions — like / dislike toggle (1=like, 2=dislike)
    [HttpPost("{id:guid}/reactions")]
    [Authorize]
    public async Task<IActionResult> ReactToVideo(Guid id, [FromBody] ReactToVideoRequest request)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        if (!Enum.IsDefined(typeof(ReactionType), request.ReactionTypeId))
            return BadRequest("Invalid reaction type. Use 1 (like) or 2 (dislike).");

        var video = await videoRepository.GetByIdAsync(id);
        if (video is null) return NotFound();

        var (likeCount, dislikeCount, userReaction) = await videoRepository.ReactAsync(id, userId, request.ReactionTypeId);

        return Ok(new { likeCount, dislikeCount, userReaction });
    }

    // GET /videos/{id}/status — státusz lekérése Redis-ből; ha kész, MinIO URL-eket ad vissza
    // Async lett, mert a Redis műveletek aszinkronok (korábban szinkron volt a ConcurrentDictionary)
    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetStatus(string id)
    {
        var status = await statusService.GetStatusAsync<VideoProcessingStatus>(id);
        if (status is null) return NotFound();

        object response = new { id, status, links = new Dictionary<string, string>() };

        if (status == VideoProcessingStatus.Completed)
        {
            var links = new Dictionary<string, string>
            {
                ["master"]  = storageService.GetPublicUrl($"{id}/master.m3u8", StorageBucket.Videos),
                ["sprite"]  = storageService.GetPublicUrl($"{id}/sprite.jpg",  StorageBucket.Videos),
                ["preview"] = storageService.GetPublicUrl($"{id}/sprite.vtt",  StorageBucket.Videos)
            };

            foreach (var width in ImageService.ThumbnailWidths)
                links[$"thumb_w{width}"] = storageService.GetPublicUrl($"{id}/{id}_thumb_w{width}.jpg", StorageBucket.Images);

            response = new { id, status, links };
        }

        return Ok(response);
    }
}
