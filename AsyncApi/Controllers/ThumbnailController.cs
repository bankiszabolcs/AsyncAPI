using AsyncApi.Data.Repositories;
using AsyncApi.Models;
using AsyncApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace AsyncApi.Controllers;

[ApiController]
[Route("thumbnails")]
public sealed class ThumbnailsController(
    IConfiguration configuration,
    LinkGenerator linkGenerator,
    ImageService imageService,
    StorageService storageService,
    QueueService queueService,      // korábban Channel<ThumbnailGenerationJob> volt
    StatusService statusService,
    ImageRepository imageRepository
    )    // korábban két ConcurrentDictionary volt (státusz + kiterjesztés)
    : ControllerBase
{
    private readonly string _uploadDirectory = configuration["UploadDirectory"] ?? "uploads";

    private Guid? CurrentUserId
    {
        get
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    // POST /thumbnails — feltölti a képet, sorba állítja a thumbnail generálást, visszaadja a státusz URL-t
    [Authorize]
    [HttpPost]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> UploadImage(IFormFile? file)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        if (file is null) return BadRequest("No file uploaded.");

        if (!imageService.IsValidImage(file))
            return BadRequest("Invalid image file. Only JPG, PNG, and GIF formats are allowed.");

        var id = Guid.NewGuid();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var folderPath = Path.Combine(_uploadDirectory, "images", id.ToString());
        var fileName = $"{id}{extension}";

        var originalFilePath = await imageService.SaveOriginalImageAsync(file, folderPath, fileName);

        // Kiterjesztés Redis-be mentve — a státusz endpointban kell a MinIO URL felépítéséhez
        await statusService.SetExtensionAsync(id.ToString(), extension);

        // DB rekord létrehozása — kiterjesztés is mentve, hogy a status endpoint össze tudja rakni a URL-t
        await imageRepository.CreateAsync(id, file.FileName, extension, userId);

        // Job Redis Stream-be írva — korábban channel.Writer.WriteAsync(job) volt
        var job = new ThumbnailGenerationJob(id.ToString(), originalFilePath, folderPath);
        await queueService.EnqueueAsync(QueueService.ThumbnailStreamKey, job);

        // Státusz Redis-be írva — korábban statusDictionary[id] = ... volt
        await statusService.SetStatusAsync(id.ToString(), ThumbnailGenerationStatus.Queued);

        var statusUrl = linkGenerator.GetUriByAction(HttpContext, nameof(GetStatus), "Thumbnails", new { id })
            ?? throw new InvalidOperationException("Failed to generate URL.");

        return Accepted(statusUrl, new { id, status = ThumbnailGenerationStatus.Queued.ToString() });
    }

    // GET /thumbnails/{id}/status — státusz lekérése Redis-ből; ha kész, MinIO linkeket ad vissza
    // Async lett, mert a Redis műveletek aszinkronok (korábban szinkron volt a ConcurrentDictionary)
    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetStatus(string id)
    {
        var status = await statusService.GetStatusAsync<ThumbnailGenerationStatus>(id);
        if (status is null) return NotFound();

        object response = new { id, status = status.ToString(), links = new Dictionary<string, string>() };

        if (status == ThumbnailGenerationStatus.Completed)
        {
            var ext = await statusService.GetExtensionAsync(id);
            if (ext is not null)
            {
                var links = ImageService.ThumbnailWidths.ToDictionary(
                    width => $"w{width}",
                    width => storageService.GetPublicUrl($"{id}/{id}_w{width}{ext}", StorageBucket.Images));

                links["original"] = storageService.GetPublicUrl($"{id}/{id}{ext}", StorageBucket.Images);

                response = new { id, status = status.ToString(), links };
            }
        }

        return Ok(response);
    }
}
