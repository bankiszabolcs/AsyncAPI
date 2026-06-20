using System.Net;
using System.Text;
using System.Text.Json;
using AsyncApi.Data;
using AsyncApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsyncApi.Services;

public sealed class VideoTagService(
    AsyncApiDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<VideoTagService> logger)
{
    private const string GeminiBase = "https://generativelanguage.googleapis.com/v1beta/models";

    // Elsődleges → fallback sorrend. Ha az első 503/429 után is elszáll, a következővel próbál.
    private static readonly string[] Models = ["gemini-2.5-flash", "gemini-2.5-flash-lite"];

    private static readonly int[] RetryDelaysSeconds = [5, 15];

    private Guid? TechnicalUserId =>
        Guid.TryParse(configuration["TechnicalUser:Id"], out var id) ? id : null;

    public async Task GenerateAndSaveTagsAsync(
        Guid videoId, string language, string? audioPath, IReadOnlyList<string> keyframePaths)
    {
        var apiKey = configuration["AI:GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("AI:GeminiApiKey is not configured, skipping tag generation for {VideoId}", videoId);
            return;
        }

        var video = await db.Videos.FindAsync(videoId);
        if (video is null) return;

        foreach (var model in Models)
        {
            var succeeded = await TryGenerateWithModelAsync(videoId, apiKey, model, language, audioPath, keyframePaths);
            if (succeeded) return;

            logger.LogWarning("Model {Model} failed for {VideoId}, trying next fallback", model, videoId);
        }

        logger.LogWarning("Tag generation skipped for {VideoId}: all models exhausted", videoId);
    }

    private async Task<bool> TryGenerateWithModelAsync(
        Guid videoId, string apiKey, string model, string language,
        string? audioPath, IReadOnlyList<string> keyframePaths)
    {
        for (var attempt = 0; attempt <= RetryDelaysSeconds.Length; attempt++)
        {
            try
            {
                var tags = await CallGeminiAsync(apiKey, model, language, audioPath, keyframePaths);
                if (tags.Count > 0)
                    await SaveTagsAsync(videoId, tags);

                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("Generated {Count} tags via {Model} for {VideoId}: {@Tags}",
                        tags.Count, model, videoId, tags);

                return true;
            }
            catch (HttpRequestException ex) when (IsRetryable(ex.StatusCode))
            {
                if (attempt < RetryDelaysSeconds.Length)
                {
                    var delay = RetryDelaysSeconds[attempt];
                    logger.LogWarning(
                        "{Status} from {Model} for {VideoId}, waiting {Delay}s (attempt {Attempt}/{Max})",
                        ex.StatusCode, model, videoId, delay, attempt + 1, RetryDelaysSeconds.Length);
                    await Task.Delay(TimeSpan.FromSeconds(delay));
                }
                else
                {
                    // Retryk elfogytak ennél a modellnél → lépjünk a következőre
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Tag generation error with {Model} for {VideoId}", model, videoId);
                return false;
            }
        }

        return false;
    }

    private static bool IsRetryable(HttpStatusCode? statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;

    private async Task<List<string>> CallGeminiAsync(
        string apiKey, string model, string language,
        string? audioPath, IReadOnlyList<string> keyframePaths)
    {
        var parts = new List<object>
        {
            new
            {
                text = $"""
                    Analyze this video's audio and visuals, then generate exactly 4 relevant tags.
                    Rules:
                    - Generate the tags in this language (BCP 47 code): {language}
                    - Each tag must be a SINGLE word only — no spaces, no hyphens
                    - Base tags on the actual audio/visual content, not any filename
                    - Tags should be useful for search and discovery
                    - Return ONLY a valid JSON array of strings, nothing else
                    Example for "hu": ["főzés","recept","kezdők","tészta"]
                    Example for "en": ["cooking","recipe","beginner","pasta"]
                    """
            }
        };

        if (audioPath is not null && File.Exists(audioPath))
        {
            var bytes = await File.ReadAllBytesAsync(audioPath);
            parts.Add(new { inlineData = new { mimeType = "audio/aac", data = Convert.ToBase64String(bytes) } });
        }

        foreach (var framePath in keyframePaths.Where(File.Exists))
        {
            var bytes = await File.ReadAllBytesAsync(framePath);
            parts.Add(new { inlineData = new { mimeType = "image/jpeg", data = Convert.ToBase64String(bytes) } });
        }

        var payload = JsonSerializer.Serialize(new { contents = new[] { new { parts } } });

        var client = httpClientFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{GeminiBase}/{model}:generateContent");
        req.Headers.Add("x-goog-api-key", apiKey);
        req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";

        var start = text.IndexOf('[');
        var end   = text.LastIndexOf(']');
        if (start < 0 || end < 0) return [];

        return JsonSerializer.Deserialize<List<string>>(text[start..(end + 1)]) ?? [];
    }

    private async Task SaveTagsAsync(Guid videoId, List<string> tagNames)
    {
        var systemUserId = TechnicalUserId;

        var normalized = tagNames
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t is { Length: > 0 and <= 50 })
            .Distinct()
            .ToList();

        foreach (var name in normalized)
        {
            var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == name);
            if (tag is null)
            {
                tag = new Tag
                {
                    Id           = Guid.NewGuid(),
                    Name         = name,
                    Active       = true,
                    CreateDate   = DateTime.UtcNow,
                    CreateUserId = systemUserId,
                };
                db.Tags.Add(tag);
                await db.SaveChangesAsync();
            }

            var exists = await db.VideoTags.AnyAsync(vt => vt.VideoId == videoId && vt.TagId == tag.Id);
            if (!exists)
            {
                db.VideoTags.Add(new VideoTag
                {
                    VideoId      = videoId,
                    TagId        = tag.Id,
                    Active       = true,
                    CreateDate   = DateTime.UtcNow,
                    CreateUserId = systemUserId,
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
