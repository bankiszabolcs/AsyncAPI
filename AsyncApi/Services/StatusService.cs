using StackExchange.Redis;

namespace AsyncApi.Services;

public sealed class StatusService(IConnectionMultiplexer redis)
{
    // Ennyi ideig marad meg a státusz és a metaadatok a Redis-ben újraindítás után is
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(7);

    // Státusz mentése Redis-be string formában (pl. "Queued", "Processing", "Completed", "Failed")
    // Az enum.ToString() emberi olvasható formátumot ad, ami Redis-ben is könnyen debugolható
    public async Task SetStatusAsync(string id, Enum status)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync($"status:{id}", status.ToString(), Ttl);
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
    // Pl. ".jpg", ".png" — feltöltéskor ismert, de a státusz lekérésekor is kell
    public async Task SetExtensionAsync(string id, string extension)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync($"ext:{id}", extension, Ttl);
    }

    // Fájlkiterjesztés lekérése job ID alapján
    public async Task<string?> GetExtensionAsync(string id)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync($"ext:{id}");
        return value.HasValue ? value.ToString() : null;
    }
}
