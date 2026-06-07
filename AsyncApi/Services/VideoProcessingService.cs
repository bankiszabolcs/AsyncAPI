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

    // Státuszt Redis-be ír (nem memóriába), majd feldolgozza a job-ot
    private async Task ProcessJobAsync(VideoProcessingJob job)
    {
        await statusService.SetStatusAsync(job.Id, VideoProcessingStatus.Processing);

        try
        {
            await videoService.ProcessAndUploadAsync(job.Id, job.OriginalFilePath, job.FolderPath);
            await statusService.SetStatusAsync(job.Id, VideoProcessingStatus.Completed);
        }
        catch (Exception)
        {
            await statusService.SetStatusAsync(job.Id, VideoProcessingStatus.Failed);
            throw;
        }
    }
}
