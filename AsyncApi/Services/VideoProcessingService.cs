using AsyncApi.Enums;
using AsyncApi.Models;
using AsyncApi.Services;

// BackgroundService: az alkalmazás élettartama alatt folyamatosan fut a háttérben
public class VideoProcessingService(
    ILogger<VideoProcessingService> logger,
    VideoService videoService,
    QueueService queueService,    // korábban Channel volt; most Redis Stream-ből olvassa a job-okat
    StatusService statusService)  // korábban ConcurrentDictionary volt; most Redis-ben tárolja az állapotot
    : BackgroundService
{
    // DequeueAsync ugyanúgy viselkedik mint a korábbi ReadAllAsync: vár ha nincs új job
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queueService.DequeueAsync<VideoProcessingJob>(
            QueueService.VideoStreamKey,
            QueueService.VideoGroupName,
            consumerName: Environment.MachineName, // egyedi név, multi-instance esetén fontos
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
        var id = Guid.Parse(job.Id);
        await statusService.SetVideoStatusAsync(id, ProcessingStatus.Processing);

        try
        {
            var duration = await videoService.ProcessAndUploadAsync(job.Id, job.OriginalFilePath, job.FolderPath);
            await statusService.SetVideoCompletedAsync(id, (int)duration.TotalSeconds);
        }
        catch (Exception)
        {
            await statusService.SetVideoStatusAsync(id, ProcessingStatus.Failed);
            throw;
        }
    }
}
