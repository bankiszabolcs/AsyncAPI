using AsyncApi.Models;
using AsyncApi.Services;

// BackgroundService: az alkalmazás élettartama alatt folyamatosan fut a háttérben
public class ThumbnailGenerationService(
    ILogger<ThumbnailGenerationService> logger,
    ImageService imageService,
    QueueService queueService,    // korábban Channel volt; most Redis Stream-ből olvassa a job-okat
    StatusService statusService)  // korábban ConcurrentDictionary volt; most Redis-ben tárolja az állapotot
    : BackgroundService
{
    // Az ASP.NET Core automatikusan hívja meg induláskor; addig fut, míg az app le nem áll
    // DequeueAsync ugyanúgy viselkedik mint a korábbi ReadAllAsync: vár ha nincs új job
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queueService.DequeueAsync<ThumbnailGenerationJob>(
            QueueService.ThumbnailStreamKey,
            QueueService.ThumbnailGroupName,
            consumerName: Environment.MachineName, // egyedi név, multi-instance esetén fontos
            stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job);
            }
            catch (OperationCanceledException)
            {
                // Az app leállítása váltja ki; normál kilépés, nem hiba
                break;
            }
            catch (Exception ex)
            {
                // Job-szintű hiba: logolja és folytatja a következő job-bal
                logger.LogError(ex, "Error processing thumbnail generation job {Id}", job.Id);
            }
        }
    }

    // Státuszt Redis-be ír (nem memóriába), majd feldolgozza a job-ot
    private async Task ProcessJobAsync(ThumbnailGenerationJob job)
    {
        await statusService.SetStatusAsync(job.Id, ThumbnailGenerationStatus.Processing);

        try
        {
            await imageService.ProcessAndUploadAsync(job.Id, job.OriginalFilePath, job.FolderPath);
            await statusService.SetStatusAsync(job.Id, ThumbnailGenerationStatus.Completed);
        }
        catch (Exception)
        {
            await statusService.SetStatusAsync(job.Id, ThumbnailGenerationStatus.Failed);
            throw; // továbbdobja, hogy az ExecuteAsync catch ága logolhassa
        }
    }
}
