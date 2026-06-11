using AsyncApi.Enums;
using AsyncApi.Models;
using AsyncApi.Services;

public class ThumbnailGenerationService(
    ILogger<ThumbnailGenerationService> logger,
    ImageService imageService,
    QueueService queueService,
    StatusService statusService)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queueService.DequeueAsync<ThumbnailGenerationJob>(
            QueueService.ThumbnailStreamKey,
            QueueService.ThumbnailGroupName,
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
                logger.LogError(ex, "Error processing thumbnail generation job {Id}", job.Id);
            }
        }
    }

    private async Task ProcessJobAsync(ThumbnailGenerationJob job)
    {
        var id = Guid.Parse(job.Id);

        await statusService.SetImageStatusAsync(id, ProcessingStatus.Processing);

        try
        {
            var (width, height) = await imageService.ProcessAndUploadAsync(job.Id, job.OriginalFilePath, job.FolderPath);
            await statusService.SetImageStatusAsync(id, ProcessingStatus.Completed, width, height);
        }
        catch (Exception)
        {
            await statusService.SetImageStatusAsync(id, ProcessingStatus.Failed);
            throw;
        }
    }
}
