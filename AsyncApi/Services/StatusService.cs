using AsyncApi.Enums;
using AsyncApi.Data.Repositories;
using AsyncApi.Services;
using StackExchange.Redis;

namespace AsyncApi.Services;

public sealed class StatusService(IConnectionMultiplexer redis, IServiceScopeFactory scopeFactory)
{
    // Ennyi ideig marad meg a státusz és a metaadatok a Redis-ben újraindítás után is
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(7);

    // Redis-only — kontrollerek hívják feltöltéskor
    // DB-t nem kell frissíteni, mert CreateAsync már 'queued'-del hozta létre a rekordot
    public async Task SetStatusAsync(string id, Enum status)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync($"status:{id}", status.ToString(), Ttl);
    }

    // Redis + DB — háttérszolgáltatások hívják (Processing, Completed, Failed átmeneteknél)
    // IServiceScopeFactory kell, mert StatusService singleton, de a repository scoped
    public async Task SetVideoStatusAsync(Guid id, ProcessingStatus status)
    {
        await SetStatusAsync(id.ToString(), status);

        await using var scope = scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<VideoRepository>();
        await repo.UpdateStatusAsync(id, status);
    }

    public async Task SetVideoCompletedAsync(Guid id, int durationSeconds)
    {
        await SetStatusAsync(id.ToString(), ProcessingStatus.Completed);

        await using var scope = scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<VideoRepository>();
        await repo.UpdateCompletedAsync(id, durationSeconds);

        var notifSvc = scope.ServiceProvider.GetRequiredService<NotificationService>();
        await notifSvc.NotifyVideoProcessedAsync(id);
    }

    public async Task SetImageStatusAsync(Guid id, ProcessingStatus status, int? width = null, int? height = null)
    {
        await SetStatusAsync(id.ToString(), status);

        await using var scope = scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ImageRepository>();
        await repo.UpdateStatusAsync(id, status, width, height);
    }

    // Státusz lekérése és visszaalakítása a kért enum típusra
    // Null-t ad vissza ha az ID nem létezik (404-hez használja a controller)
    public async Task<T?> GetStatusAsync<T>(string id) where T : struct, Enum
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync($"status:{id}");

        if (!value.HasValue) return null;

        return Enum.TryParse<T>(value.ToString(), out var result) ? result : null;
    }

    // Fájlkiterjesztés mentése — szükséges a MinIO URL felépítéséhez a státusz endpointban
    public async Task SetExtensionAsync(string id, string extension)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync($"ext:{id}", extension, Ttl);
    }

    public async Task<string?> GetExtensionAsync(string id)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync($"ext:{id}");
        return value.HasValue ? value.ToString() : null;
    }
}
