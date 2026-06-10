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
    StatusService statusService)    // korábban ConcurrentDictionary<string, VideoProcessingStatus> volt
    : ControllerBase
{
    private readonly string _uploadDirectory = configuration["UploadDirectory"] ?? "uploads";

    // POST /videos — feltölti a videót, sorba állítja a feldolgozást, visszaadja a státusz URL-t
    [HttpPost]
    [RequestSizeLimit(2_000_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2_000_000_000)]
    public async Task<IActionResult> UploadVideo(IFormFile? file)
    {
        if (file is null) return BadRequest("No file uploaded.");

        if (!videoService.IsValidVideo(file))
            return BadRequest("Invalid video file. Allowed formats: mp4, mov, mkv, avi, webm.");

        var id = Guid.NewGuid().ToString();
        var folderPath = Path.Combine(_uploadDirectory, "videos", id);
        var fileName = $"{id}{Path.GetExtension(file.FileName)}";

        var originalFilePath = await videoService.SaveOriginalVideoAsync(file, folderPath, fileName);

        // Job Redis Stream-be írva — korábban channel.Writer.WriteAsync(job) volt
        var job = new VideoProcessingJob(id, originalFilePath, folderPath);
        await queueService.EnqueueAsync(QueueService.VideoStreamKey, job);

        // Státusz Redis-be írva — korábban statusDictionary[id] = ... volt
        await statusService.SetStatusAsync(id, VideoProcessingStatus.Queued);

        var statusUrl = linkGenerator.GetUriByAction(HttpContext, nameof(GetStatus), "Videos", new { id })
            ?? throw new InvalidOperationException("Failed to generate URL.");

        return Accepted(statusUrl, new { id, status = VideoProcessingStatus.Queued });
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
