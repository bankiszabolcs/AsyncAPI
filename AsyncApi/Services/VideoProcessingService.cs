using System.Collections.Concurrent;
using System.Threading.Channels;
using AsyncApi.Models;
using AsyncApi.Services;

public class VideoProcessingService(
    ILogger<VideoProcessingService> logger,
    VideoService videoService,
    Channel<VideoProcessingJob> channel,
    ConcurrentDictionary<string, VideoProcessingStatus> statusDictionary) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in channel.Reader.ReadAllAsync(stoppingToken))
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
        statusDictionary[job.Id] = VideoProcessingStatus.Processing;

        try
        {
            // HLS és sprite generálás párhuzamosan fut — mindkettő az eredeti fájlból dolgozik
            await Task.WhenAll(
                videoService.GenerateHlsAsync(job.OriginalFilePath, job.FolderPath),
                videoService.GenerateSpriteAsync(job.OriginalFilePath, job.FolderPath)
            );

            statusDictionary[job.Id] = VideoProcessingStatus.Completed;
        }
        catch (Exception)
        {
            statusDictionary[job.Id] = VideoProcessingStatus.Failed;
            throw;
        }
    }
}
