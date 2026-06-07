using System.Collections.Concurrent;
using System.Threading.Channels;
using AsyncApi.Models;
using AsyncApi.Services;

// BackgroundService: az alkalmazás élettartama alatt folyamatosan fut a háttérben
public class ThumbnailGenerationService(
    ILogger<ThumbnailGenerationService> logger,                                    // hibák és események naplózása
    ImageService imageService,                                                     // a tényleges thumbnail generálás logikája
    Channel<ThumbnailGenerationJob> channel,                                       // ebből olvassa ki a feldolgozandó job-okat (a controller írja bele)
    ConcurrentDictionary<string, ThumbnailGenerationStatus> statusDictionary)     // itt frissíti a job állapotát (Processing → Completed/Failed)
    : BackgroundService
{
    // Az ASP.NET Core automatikusan hívja meg induláskor; addig fut, míg az app le nem áll
    // ReadAllAsync megvárja az újabb elemeket, nem pörög üres loopban (nem CPU-igényes)
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
                // Az app leállítása váltja ki; normál kilépés, nem hiba
                break;
            }
            catch (Exception ex)
            {
                // Job-szintű hiba: logolja és folytatja a következő job-bal
                logger.LogError(ex, "Error processing thumbnail generation job");
            }
        }
    }

    // Feldolgoz egy job-ot: státuszt frissít, thumbnail-eket generál, majd sikerjelzés vagy hiba
    private async Task ProcessJobAsync(ThumbnailGenerationJob job)
    {
        statusDictionary[job.Id] = ThumbnailGenerationStatus.Processing;

        try
        {
            await imageService.GenerateThumbnailsAsync(job.OriginalFilePath, job.FolderPath, job.Id);
            statusDictionary[job.Id] = ThumbnailGenerationStatus.Completed;
        }
        catch (Exception e)
        {
            statusDictionary[job.Id] = ThumbnailGenerationStatus.Failed;
            throw; // továbbdobja, hogy az ExecuteAsync catch ága logolhassa
        }
    }
}
