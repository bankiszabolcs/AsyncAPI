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
    private const string GeminiEndpoint =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

    public async Task GenerateAndSaveTagsAsync(
        Guid videoId, string? audioPath, IReadOnlyList<string> keyframePaths)
    {
        var apiKey = configuration["AI:GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("AI:GeminiApiKey is not configured, skipping tag generation for {VideoId}", videoId);
            return;
        }

        var video = await db.Videos.FindAsync(videoId);
        if (video is null) return;

        try
        {
            var tags = await CallGeminiAsync(apiKey, video.Title ?? "", audioPath, keyframePaths);
            if (tags.Count > 0)
                await SaveTagsAsync(videoId, tags);

            logger.LogInformation("Generated {Count} tags for {VideoId}: {Tags}",
                tags.Count, videoId, string.Join(", ", tags));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tag generation failed for {VideoId}", videoId);
        }
    }

    private async Task<List<string>> CallGeminiAsync(
        string apiKey, string title, string? audioPath, IReadOnlyList<string> keyframePaths)
    {
        var parts = new List<object>
        {
            new
            {
                text = $"""
                    Analyze this video and generate exactly 4 short, relevant tags.
                    Title: "{title}"
                    Rules:
                    - Use the same language as the title
                    - Each tag: 1-3 words max
                    - Tags should be useful for search and discovery
                    - Return ONLY a valid JSON array of strings, nothing else
                    Example: ["cooking","italian pasta","beginner","recipe"]
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
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{GeminiEndpoint}?key={apiKey}");
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
                    Id         = Guid.NewGuid(),
                    Name       = name,
                    Active     = true,
                    CreateDate = DateTime.UtcNow,
                };
                db.Tags.Add(tag);
                await db.SaveChangesAsync();
            }

            var exists = await db.VideoTags.AnyAsync(vt => vt.VideoId == videoId && vt.TagId == tag.Id);
            if (!exists)
            {
                db.VideoTags.Add(new VideoTag
                {
                    VideoId    = videoId,
                    TagId      = tag.Id,
                    Active     = true,
                    CreateDate = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
