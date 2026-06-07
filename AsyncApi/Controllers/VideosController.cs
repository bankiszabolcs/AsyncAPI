using System.Collections.Concurrent;
using System.Threading.Channels;
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
    Channel<VideoProcessingJob> channel,
    ConcurrentDictionary<string, VideoProcessingStatus> statusDictionary) : ControllerBase
{
    private readonly string _uploadDirectory = configuration["UploadDirectory"] ?? "uploads";

    // POST /videos — feltölti a videót, sorba állítja a feldolgozást, visszaadja a státusz URL-t
    [HttpPost]
    [RequestSizeLimit(2_000_000_000)]                                        // 2 GB maximális kérés méret
    [RequestFormLimits(MultipartBodyLengthLimit = 2_000_000_000)]            // 2 GB multipart form limit
    public async Task<IActionResult> UploadVideo(IFormFile? file)
    {
        if (file is null)
            return BadRequest("No file uploaded.");

        if (!videoService.IsValidVideo(file))
            return BadRequest("Invalid video file. Allowed formats: mp4, mov, mkv, avi, webm.");

        var id = Guid.NewGuid().ToString();
        var folderPath = Path.Combine(_uploadDirectory, "videos", id);
        var fileName = $"{id}{Path.GetExtension(file.FileName)}";

        var originalFilePath = await videoService.SaveOriginalVideoAsync(file, folderPath, fileName);

        var job = new VideoProcessingJob(id, originalFilePath, folderPath);
        await channel.Writer.WriteAsync(job);

        statusDictionary[id] = VideoProcessingStatus.Queued;

        var statusUrl = linkGenerator.GetUriByAction(HttpContext, nameof(GetStatus), "Videos", new { id })
            ?? throw new InvalidOperationException("Failed to generate URL.");

        return Accepted(statusUrl, new { id, status = VideoProcessingStatus.Queued });
    }

    // GET /videos/{id}/status — visszaadja a feldolgozás állapotát; ha kész, linkeket is a fájlokhoz
    [HttpGet("{id}/status")]
    public IActionResult GetStatus(string id)
    {
        if (!statusDictionary.TryGetValue(id, out var status))
            return NotFound();

        object response = new { id, status, links = new Dictionary<string, string>() };

        if (status == VideoProcessingStatus.Completed)
        {
            var links = new Dictionary<string, string>
            {
                ["master"]  = GetFileUrl(id, "master.m3u8"),
                ["sprite"]  = GetFileUrl(id, "sprite.jpg"),
                ["preview"] = GetFileUrl(id, "sprite.vtt")
            };

            response = new { id, status, links };
        }

        return Ok(response);
    }

    // GET /videos/{id}/{fileName} — kiszolgálja a HLS fájlokat (master.m3u8, szegmensek, sprite)
    [HttpGet("{id}/{fileName}")]
    public IActionResult GetFile(string id, string fileName)
    {
        var filePath = Path.Combine(_uploadDirectory, "videos", id, fileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var contentType = Path.GetExtension(fileName) switch
        {
            ".m3u8" => "application/vnd.apple.mpegurl",
            ".ts"   => "video/mp2t",
            ".jpg"  => "image/jpeg",
            ".vtt"  => "text/vtt",
            _       => "application/octet-stream"
        };

        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return File(fileStream, contentType);
    }

    // GET /videos/{id}/{quality}/{fileName} — kiszolgálja a minőségi szintenkénti fájlokat (pl. 1080p/index.m3u8)
    [HttpGet("{id}/{quality}/{fileName}")]
    public IActionResult GetQualityFile(string id, string quality, string fileName)
    {
        var filePath = Path.Combine(_uploadDirectory, "videos", id, quality, fileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var contentType = Path.GetExtension(fileName) switch
        {
            ".m3u8" => "application/vnd.apple.mpegurl",
            ".ts"   => "video/mp2t",
            _       => "application/octet-stream"
        };

        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return File(fileStream, contentType);
    }

    private string GetFileUrl(string id, string fileName)
    {
        return linkGenerator.GetUriByAction(HttpContext, nameof(GetFile), "Videos", new { id, fileName })
            ?? throw new InvalidOperationException("Failed to generate URL.");
    }
}
