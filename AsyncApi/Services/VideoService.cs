using FFMpegCore;
using FFMpegCore.Enums;

namespace AsyncApi.Services;

public sealed class VideoService(
    ILogger<VideoService> logger,
    StorageService storageService)
{
    private static readonly string[] AllowedExtensions = [".mp4", ".mov", ".mkv", ".avi", ".webm"];
    private static readonly string[] AllowedMimeTypes =
    [
        "video/mp4", "video/quicktime", "video/x-matroska",
        "video/x-msvideo", "video/webm"
    ];

    private static readonly (int Width, int Bitrate, string Name)[] HlsQualities =
    [
        (3840, 15000, "2160p"),
        (1920,  5000, "1080p"),
        (1280,  2800,  "720p"),
        ( 854,  1400,  "480p")
    ];

    public static IEnumerable<string> StreamQualities => HlsQualities.Select(q => q.Name);

    public bool IsValidVideo(IFormFile file)
    {
        if (file.Length == 0) return false;

        var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        return AllowedExtensions.Contains(extension) && AllowedMimeTypes.Contains(file.ContentType);
    }

    public async Task<string> SaveOriginalVideoAsync(IFormFile file, string folderPath, string fileName)
    {
        Directory.CreateDirectory(folderPath);

        var originalFilePath = Path.Combine(folderPath, fileName);
        using var stream = new FileStream(originalFilePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return originalFilePath;
    }

    // FFProbe egyszer fut le; a duration-t megosztja a sprite és a thumbnail generálás között
    public async Task<TimeSpan> ProcessAndUploadAsync(string id, string originalFilePath, string folderPath)
    {
        logger.LogInformation("Starting video processing for {VideoId}", id);

        try
        {
            var thumbFolder = Path.Combine(Path.GetDirectoryName(folderPath)!, "..", "thumbs", id);
            Directory.CreateDirectory(thumbFolder);

            var mediaInfo = await FFProbe.AnalyseAsync(originalFilePath);

            // Thumbnail először, és rögtön fel is töltjük — így a videó még feldolgozás
            // alatt van, de a kliens már megkapja a thumbnailt (háttérkép a lejátszóban).
            await GenerateVideoThumbnailsAsync(originalFilePath, thumbFolder, id, mediaInfo.Duration);
            await storageService.UploadDirectoryAsync(thumbFolder, id, StorageBucket.Images);

            // Utána a lassú rész: HLS kódolás + sprite párhuzamosan
            var sourceWidth = mediaInfo.PrimaryVideoStream?.Width ?? 0;

            await Task.WhenAll(
                GenerateHlsAsync(originalFilePath, folderPath, sourceWidth),
                GenerateSpriteAsync(originalFilePath, folderPath, mediaInfo.Duration)
            );

            File.Delete(originalFilePath);

            await storageService.UploadDirectoryAsync(folderPath, id, StorageBucket.Videos);

            Directory.Delete(folderPath, recursive: true);
            Directory.Delete(thumbFolder, recursive: true);

            logger.LogInformation("Video processing completed for {VideoId}", id);
            return mediaInfo.Duration;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Video processing failed for {VideoId}", id);
            throw;
        }
    }

    private static async Task GenerateHlsAsync(string originalFilePath, string folderPath, int sourceWidth)
    {
        var qualities = HlsQualities.Where(q => q.Width <= sourceWidth).ToArray();

        // Ha a forrás kisebb mint a legkisebb minőségünk (854px), akkor is generáljuk a legkisebbet
        if (qualities.Length == 0)
            qualities = [HlsQualities[^1]];

        foreach (var (width, bitrate, name) in qualities)
        {
            var qualityFolder = Path.Combine(folderPath, name);
            Directory.CreateDirectory(qualityFolder);

            var segmentPattern = Path.Combine(qualityFolder, "segment%03d.ts");
            var playlistPath = Path.Combine(qualityFolder, "index.m3u8");

            await FFMpegArguments
                .FromFileInput(originalFilePath)
                .OutputToFile(playlistPath, overwrite: true, options => options
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithVideoBitrate(bitrate)
                    .WithAudioCodec(AudioCodec.Aac)
                    .WithAudioBitrate(128)
                    .WithCustomArgument($"-vf scale={width}:-2")
                    .WithCustomArgument("-hls_time 6")
                    .WithCustomArgument("-hls_playlist_type vod")
                    .WithCustomArgument($"-hls_segment_filename {segmentPattern}")
                    .ForceFormat("hls"))
                .ProcessAsynchronously();
        }

        await WriteMasterPlaylistAsync(folderPath, qualities);
    }

    private static async Task GenerateSpriteAsync(string originalFilePath, string folderPath, TimeSpan duration)
    {
        var totalSeconds    = (int)duration.TotalSeconds;
        var intervalSeconds = Math.Max(1, totalSeconds / 100);

        var spritePath = Path.Combine(folderPath, "sprite.jpg");

        await FFMpegArguments
            .FromFileInput(originalFilePath)
            .OutputToFile(spritePath, overwrite: true, options => options
                .WithCustomArgument($"-vf fps=1/{intervalSeconds},scale=160:90:force_original_aspect_ratio=decrease,pad=160:90:(ow-iw)/2:(oh-ih)/2,tile=10x10")
                .WithFrameOutputCount(1))
            .ProcessAsynchronously();

        await WriteWebVttAsync(folderPath, duration, intervalSeconds);
    }

    // Teljes felbontású frame kimentése a 10. másodpercből (vagy az elejéről ha rövidebb a videó),
    // majd az ImageService meglévő resize logikájával legenerálja az összes standard méretet
    private static async Task GenerateVideoThumbnailsAsync(
        string originalFilePath, string thumbFolder, string id, TimeSpan duration)
    {
        var seekTime   = duration.TotalSeconds >= 10 ? TimeSpan.FromSeconds(10) : TimeSpan.Zero;
        var sourcePath = Path.Combine(thumbFolder, "source.jpg");

        await FFMpegArguments
            .FromFileInput(originalFilePath, false, options => options.Seek(seekTime))
            .OutputToFile(sourcePath, overwrite: true, options => options
                .WithFrameOutputCount(1)
                .WithCustomArgument("-q:v 2"))
            .ProcessAsynchronously();

        await ImageService.GenerateResizedCopiesAsync(sourcePath, thumbFolder, $"{id}_thumb");
        File.Delete(sourcePath);
    }

    private static async Task WriteMasterPlaylistAsync(
        string folderPath,
        (int Width, int Bitrate, string Name)[] qualities)
    {
        var masterPath = Path.Combine(folderPath, "master.m3u8");

        var lines = new List<string> { "#EXTM3U" };

        foreach (var (width, bitrate, name) in qualities)
        {
            var bps = bitrate * 1000;
            lines.Add($"#EXT-X-STREAM-INF:BANDWIDTH={bps},RESOLUTION={width}x{GetHeight(width)}");
            lines.Add($"{name}/index.m3u8");
        }

        await File.WriteAllLinesAsync(masterPath, lines);
    }

    private static async Task WriteWebVttAsync(string folderPath, TimeSpan duration, int intervalSeconds)
    {
        var vttPath = Path.Combine(folderPath, "sprite.vtt");
        var lines = new List<string> { "WEBVTT", "" };

        const int frameWidth  = 160;
        const int frameHeight = 90;
        const int columns     = 10;

        var totalFrames = (int)(duration.TotalSeconds / intervalSeconds);

        for (var i = 0; i < totalFrames; i++)
        {
            var start = TimeSpan.FromSeconds(i * intervalSeconds);
            var end   = TimeSpan.FromSeconds((i + 1) * intervalSeconds);
            var x     = i % columns * frameWidth;
            var y     = i / columns * frameHeight;

            lines.Add($"{start:hh\\:mm\\:ss\\.fff} --> {end:hh\\:mm\\:ss\\.fff}");
            lines.Add($"sprite.jpg#xywh={x},{y},{frameWidth},{frameHeight}");
            lines.Add("");
        }

        await File.WriteAllLinesAsync(vttPath, lines);
    }

    private static int GetHeight(int width) => width switch
    {
        3840 => 2160,
        1920 => 1080,
        1280 =>  720,
         854 =>  480,
        _    =>    0
    };
}
