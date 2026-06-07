using FFMpegCore;
using FFMpegCore.Enums;

namespace AsyncApi.Services;

public sealed class VideoService
{
    private static readonly string[] AllowedExtensions = [".mp4", ".mov", ".mkv", ".avi", ".webm"];
    private static readonly string[] AllowedMimeTypes =
    [
        "video/mp4", "video/quicktime", "video/x-matroska",
        "video/x-msvideo", "video/webm"
    ];

    // Minden minőségi szinthez: felbontás szélessége és célbitráta
    private static readonly (int Width, int Bitrate, string Name)[] HlsQualities =
    [
        (1920, 5000, "1080p"),
        (1280, 2800, "720p"),
        (854,  1400, "480p")
    ];

    public bool IsValidVideo(IFormFile file)
    {
        if (file.Length == 0) return false;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
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

    // Generálja a HLS szegmenseket és a master.m3u8-at 3 minőségben
    // Minden minőségnek saját almappája lesz: 1080p/, 720p/, 480p/
    public async Task GenerateHlsAsync(string originalFilePath, string folderPath)
    {
        foreach (var (width, bitrate, name) in HlsQualities)
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

        await WriteMasterPlaylistAsync(folderPath);
    }

    // Generál egy sprite képet (rács elrendezésű frame-ek) és a hozzá tartozó WebVTT fájlt
    // A sprite-ot a videólejátszó a timeline hover előnézethez használja
    public async Task GenerateSpriteAsync(string originalFilePath, string folderPath)
    {
        var spritePath = Path.Combine(folderPath, "sprite.jpg");

        // Minden 10. másodpercből kivesz egy 160x90-es frame-et, 10x10-es rácsba rendezi
        await FFMpegArguments
            .FromFileInput(originalFilePath)
            .OutputToFile(spritePath, overwrite: true, options => options
                .WithCustomArgument("-vf fps=1/10,scale=160:90,tile=10x10")
                .WithFrameOutputCount(1))
            .ProcessAsynchronously();

        var mediaInfo = await FFProbe.AnalyseAsync(originalFilePath);
        await WriteWebVttAsync(folderPath, mediaInfo.Duration);
    }

    // A master.m3u8 hivatkozik a 3 minőségi szintű playlistre;
    // a videólejátszó (pl. HLS.js) ez alapján vált automatikusan
    private static async Task WriteMasterPlaylistAsync(string folderPath)
    {
        var masterPath = Path.Combine(folderPath, "master.m3u8");

        var lines = new List<string> { "#EXTM3U" };

        foreach (var (width, bitrate, name) in HlsQualities)
        {
            var bps = bitrate * 1000;
            lines.Add($"#EXT-X-STREAM-INF:BANDWIDTH={bps},RESOLUTION={width}x{GetHeight(width)}");
            lines.Add($"{name}/index.m3u8");
        }

        await File.WriteAllLinesAsync(masterPath, lines);
    }

    // WebVTT: szövegfájl ami megmondja a lejátszónak, hogy a sprite képen belül
    // melyik időintervallumhoz melyik téglalap tartozik
    private static async Task WriteWebVttAsync(string folderPath, TimeSpan duration)
    {
        var vttPath = Path.Combine(folderPath, "sprite.vtt");
        var lines = new List<string> { "WEBVTT", "" };

        const int intervalSeconds = 10;
        const int frameWidth = 160;
        const int frameHeight = 90;
        const int columns = 10;

        var totalFrames = (int)(duration.TotalSeconds / intervalSeconds);

        for (var i = 0; i < totalFrames; i++)
        {
            var start = TimeSpan.FromSeconds(i * intervalSeconds);
            var end = TimeSpan.FromSeconds((i + 1) * intervalSeconds);
            var x = (i % columns) * frameWidth;
            var y = (i / columns) * frameHeight;

            lines.Add($"{start:hh\\:mm\\:ss\\.fff} --> {end:hh\\:mm\\:ss\\.fff}");
            lines.Add($"sprite.jpg#xywh={x},{y},{frameWidth},{frameHeight}");
            lines.Add("");
        }

        await File.WriteAllLinesAsync(vttPath, lines);
    }

    private static int GetHeight(int width) => width switch
    {
        1920 => 1080,
        1280 => 720,
        854  => 480,
        _    => 0
    };
}
