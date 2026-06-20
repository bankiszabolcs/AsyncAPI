using AsyncApi.Enums;
using AsyncApi.Models;
using AsyncApi.Services;
using FFMpegCore;
using FFMpegCore.Enums;

public class VideoProcessingService(
    ILogger<VideoProcessingService> logger,
    VideoService videoService,
    QueueService queueService,
    StatusService statusService,
    IServiceScopeFactory scopeFactory)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queueService.DequeueAsync<VideoProcessingJob>(
            QueueService.VideoStreamKey,
            QueueService.VideoGroupName,
            consumerName: Environment.MachineName,
            stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing video job {Id}", job.Id);
            }
        }
    }

    private async Task ProcessJobAsync(VideoProcessingJob job)
    {
        var id      = Guid.Parse(job.Id);
        var tempDir = Path.Combine(Path.GetTempPath(), "clipo_ai", job.Id);

        await statusService.SetVideoStatusAsync(id, ProcessingStatus.Processing);

        try
        {
            // AI média kinyerése az eredeti fájlból — mielőtt a ProcessAndUploadAsync törölné
            string? audioPath     = null;
            var keyframePaths     = new List<string>();
            try
            {
                Directory.CreateDirectory(tempDir);
                var info = await FFProbe.AnalyseAsync(job.OriginalFilePath);
                (audioPath, keyframePaths) = await ExtractAiMediaAsync(
                    job.OriginalFilePath, tempDir, info.Duration);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AI media extraction failed for {VideoId}, proceeding without tags", id);
            }

            var duration = await videoService.ProcessAndUploadAsync(job.Id, job.OriginalFilePath, job.FolderPath);
            await statusService.SetVideoCompletedAsync(id, (int)duration.TotalSeconds);

            // Tag generálás háttérben — nem blokkolja a sort
            _ = GenerateTagsInBackgroundAsync(id, job.Language, audioPath, keyframePaths, tempDir);
        }
        catch (Exception)
        {
            await statusService.SetVideoStatusAsync(id, ProcessingStatus.Failed);
            throw;
        }
    }

    private async Task GenerateTagsInBackgroundAsync(
        Guid videoId, string language, string? audioPath, List<string> keyframePaths, string tempDir)
    {
        try
        {
            using var scope      = scopeFactory.CreateScope();
            var tagService       = scope.ServiceProvider.GetRequiredService<VideoTagService>();
            await tagService.GenerateAndSaveTagsAsync(videoId, language, audioPath, keyframePaths);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background tag generation failed for {VideoId}", videoId);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private static async Task<(string? AudioPath, List<string> KeyframePaths)> ExtractAiMediaAsync(
        string videoPath, string tempDir, TimeSpan duration)
    {
        string? audioPath = null;

        if (duration > TimeSpan.Zero)
        {
            var ap   = Path.Combine(tempDir, "audio.aac");
            var secs = (int)Math.Min(60, duration.TotalSeconds);

            await FFMpegArguments
                .FromFileInput(videoPath)
                .OutputToFile(ap, overwrite: true, opts => opts
                    .WithAudioCodec(AudioCodec.Aac)
                    .WithAudioBitrate(64)
                    .WithCustomArgument("-ac 1")
                    .WithCustomArgument($"-t {secs}")
                    .WithCustomArgument("-vn"))
                .ProcessAsynchronously();

            audioPath = ap;
        }

        var keyframePaths  = new List<string>();
        double[] positions = [0.1, 0.3, 0.5, 0.7, 0.9];

        foreach (var (pct, i) in positions.Select((p, i) => (p, i)))
        {
            var seek = TimeSpan.FromSeconds(duration.TotalSeconds * pct);
            var fp   = Path.Combine(tempDir, $"frame_{i:D2}.jpg");

            await FFMpegArguments
                .FromFileInput(videoPath, false, o => o.Seek(seek))
                .OutputToFile(fp, overwrite: true, opts => opts
                    .WithFrameOutputCount(1)
                    .WithCustomArgument("-vf scale=512:-2")
                    .WithCustomArgument("-q:v 5"))
                .ProcessAsynchronously();

            keyframePaths.Add(fp);
        }

        return (audioPath, keyframePaths);
    }
}
