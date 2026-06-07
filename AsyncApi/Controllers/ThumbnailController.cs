using System.Collections.Concurrent;
using System.Threading.Channels;
using AsyncApi.Models;
using AsyncApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AsyncApi.Controllers;

[ApiController]
[Route("thumbnails")]
public sealed class ThumbnailsController(
    IConfiguration configuration,
    LinkGenerator linkGenerator,       // teljes URL-ek generálásához (pl. státusz link a válaszban)
    ImageService imageService,
    Channel<ThumbnailGenerationJob> channel,
    ConcurrentDictionary<string, ThumbnailGenerationStatus> statusDictionary) : ControllerBase
{
    // Az appsettings.json-ból olvassa a feltöltési mappát; ha nincs beállítva, "uploads" az alapértelmezett
    private readonly string _uploadDirectory = configuration["UploadDirectory"] ?? "uploads";

    // POST /thumbnails — feltölti a képet, sorba állítja a thumbnail generálást, és visszaadja a státusz URL-t
    [HttpPost]
    public async Task<IActionResult> UploadImage(IFormFile? file)
    {
        if (file is null)
        {
            return BadRequest("No file uploaded.");
        }

        if (!imageService.IsValidImage(file))
        {
            return BadRequest("Invalid image file. Only JPG, PNG, and GIF formats are allowed.");
        }

        // Egyedi azonosító alapján hozzuk létre a mappát és a fájlnevet, elkerülve az ütközéseket
        var id = Guid.NewGuid().ToString();
        var folderPath = Path.Combine(_uploadDirectory, "images", id);
        var fileName = $"{id}{Path.GetExtension(file.FileName)}";

        var originalFilePath = await imageService.SaveOriginalImageAsync(file, folderPath, fileName);

        // A job bekerül a channel-be; a ThumbnailGenerationService veszi ki és dolgozza fel
        var job = new ThumbnailGenerationJob(id, originalFilePath, folderPath);
        await channel.Writer.WriteAsync(job);

        // Azonnal Queued státuszt állítunk be, még mielőtt a háttérszolgáltatás elkezdené
        statusDictionary[id] = ThumbnailGenerationStatus.Queued;

        // 202 Accepted: a feldolgozás még folyamatban van, a státusz URL-en lehet követni
        var statusUrl = GetFullyQualifiedUrl(nameof(GetStatus), new { id });
        return Accepted(statusUrl, new { id, status = ThumbnailGenerationStatus.Queued });
    }

    // GET /thumbnails/{id}/status — visszaadja a job állapotát; ha kész, linkeket is a thumbnail-ekhez
    [HttpGet("{id}/status")]
    public IActionResult GetStatus(string id)
    {
        if (!statusDictionary.TryGetValue(id, out var status))
        {
            return NotFound();
        }

        var response = new { id, status, links = new Dictionary<string, string>() };

        // Csak akkor adjuk vissza a thumbnail linkeket, ha a feldolgozás már befejeződött
        if (status == ThumbnailGenerationStatus.Completed)
        {
            var thumbnailLinks = ImageService.ThumbnailWidths.ToDictionary(
                width => $"w{width}",
                width => GetFullyQualifiedUrl(nameof(GetImage), new { id, width }));

            thumbnailLinks.Add("original", GetFullyQualifiedUrl(nameof(GetImage), new { id }));

            response = response with { links = thumbnailLinks };
        }

        return Ok(response);
    }

    // GET /thumbnails/{id} — visszaadja az eredeti képet
    // GET /thumbnails/{id}?width=128 — visszaadja az adott szélességű thumbnail-t
    [HttpGet("{id}")]
    public IActionResult GetImage(string id, int? width = null)
    {
        var folderPath = Path.Combine(_uploadDirectory, "images", id);

        if (!Directory.Exists(folderPath))
        {
            return NotFound();
        }

        string fileName;

        if (width is null)
        {
            // Width nélkül az eredeti képet keresi (pl. {id}.jpg)
            fileName = Directory.GetFiles(folderPath, $"{id}.*").FirstOrDefault() ?? string.Empty;
        }
        else
        {
            // Width megadásával a megfelelő thumbnail-t keresi (pl. {id}_w128.jpg)
            fileName = Directory.GetFiles(folderPath, $"{id}_w{width}.*").FirstOrDefault() ?? string.Empty;
        }

        if (string.IsNullOrEmpty(fileName) || !System.IO.File.Exists(fileName))
        {
            return NotFound();
        }

        var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);

        return File(fileStream, "image/jpeg");
    }

    // Segédfüggvény: action neve és route értékei alapján teljes URL-t generál (pl. https://localhost:5001/thumbnails/abc/status)
    private string GetFullyQualifiedUrl(string actionName, object values)
    {
        return linkGenerator.GetUriByAction(
            HttpContext,
            action: actionName,
            controller: "Thumbnails",
            values: values)
            ?? throw new InvalidOperationException("Failed to generate URL.");
    }
}
