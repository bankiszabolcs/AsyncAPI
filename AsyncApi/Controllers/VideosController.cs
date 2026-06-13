using AsyncApi.Data.Repositories;
using AsyncApi.Enums;
using AsyncApi.Models;
using AsyncApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AsyncApi.Controllers;

[ApiController]
[Route("videos")]
public sealed class VideosController(
    IConfiguration configuration,
    LinkGenerator linkGenerator,
    VideoService videoService,
    StorageService storageService,
    QueueService queueService,      // korábban Channel<VideoProcessingJob> volt
    StatusService statusService,
    VideoRepository videoRepository)    // korábban ConcurrentDictionary<string, VideoProcessingStatus> volt
    : ControllerBase
{
    private readonly string _uploadDirectory = configuration["UploadDirectory"] ?? "uploads";
    private readonly Guid _technicalUserId = Guid.Parse(configuration["TechnicalUser:Id"]
        ?? throw new InvalidOperationException("TechnicalUser:Id nincs beállítva az appsettings-ben."));

    // POST /videos — feltölti a videót, sorba állítja a feldolgozást, visszaadja a státusz URL-t
    [HttpPost]
    [RequestSizeLimit(2_000_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2_000_000_000)]
    public async Task<IActionResult> UploadVideo(IFormFile? file)
    {
        if (file is null) return BadRequest("No file uploaded.");

        if (!videoService.IsValidVideo(file))
            return BadRequest("Invalid video file. Allowed formats: mp4, mov, mkv, avi, webm.");

        var id = Guid.NewGuid();
        var folderPath = Path.Combine(_uploadDirectory, "videos", id.ToString());
        var fileName = $"{id}{Path.GetExtension(file.FileName)}";

        var originalFilePath = await videoService.SaveOriginalVideoAsync(file, folderPath, fileName);

        // DB rekord létrehozása — id és userId kötelező, a többi mező később frissíthető
        await videoRepository.CreateAsync(id, file.FileName, _technicalUserId);

        // Job Redis Stream-be írva — korábban channel.Writer.WriteAsync(job) volt
        var job = new VideoProcessingJob(id.ToString(), originalFilePath, folderPath);
        await queueService.EnqueueAsync(QueueService.VideoStreamKey, job);

        // Státusz Redis-be írva — korábban statusDictionary[id] = ... volt
        await statusService.SetStatusAsync(id.ToString(), VideoProcessingStatus.Queued);

        var statusUrl = linkGenerator.GetUriByAction(HttpContext, nameof(GetStatus), "Videos", new { id })
            ?? throw new InvalidOperationException("Failed to generate URL.");

        return Accepted(statusUrl, new { id, status = VideoProcessingStatus.Queued });
    }

    // GET /videos — kész és publikus videók listája lapozással
    [HttpGet]
    public async Task<IActionResult> GetVideos([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var videos = await videoRepository.GetListAsync(page, pageSize);

        var result = videos.Select(v => new
        {
            id          = v.Id,
            title       = v.Title,
            description = v.Description,
            duration    = v.DurationSeconds,
            publishedAt = v.PublishedAt,
            author = new
            {
                id   = v.User.Id,
                name = v.User.DisplayName ?? v.User.Username
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

    // GET /videos/{id} — egy videó adatai; csak a ténylegesen elérhető médiát adja vissza
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetVideo(Guid id)
    {
        var video = await videoRepository.GetByIdAsync(id);
        if (video is null) return NotFound();

        var isCompleted = video.StatusId == (int)ProcessingStatus.Completed;
        var hasThumbnails = video.ThumbnailImageId is not null || isCompleted;

        return Ok(new
        {
            id          = video.Id,
            title       = video.Title,
            description = video.Description,
            duration    = video.DurationSeconds,
            publishedAt = video.PublishedAt,
            statusId    = video.StatusId,
            status      = video.Status.Title,
            author = new
            {
                id   = video.User.Id,
                name = video.User.DisplayName ?? video.User.Username
            },
            tags   = video.VideoTags.Select(vt => vt.Tag.Name),
            media = new
            {
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
