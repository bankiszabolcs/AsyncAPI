using AsyncApi.Data.Repositories;
using AsyncApi.Models;

namespace AsyncApi.Services;

public class ViewWorkerService(
    ILogger<ViewWorkerService> logger,
    QueueService queueService,
    IServiceScopeFactory scopeFactory)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var record in queueService.DequeueAsync<ViewRecord>(
            QueueService.ViewStreamKey,
            QueueService.ViewGroupName,
            consumerName: Environment.MachineName,
            stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var repo = scope.ServiceProvider.GetRequiredService<VideoRepository>();
                await repo.IncrementViewCountAsync(record.VideoId);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error recording view for video {VideoId}", record.VideoId);
            }
        }
    }
}
