using System.Runtime.CompilerServices;
using System.Text.Json;
using StackExchange.Redis;

namespace AsyncApi.Services;

public sealed class QueueService(IConnectionMultiplexer redis)
{
    // Stream kulcsok — ezek alatt tárolódnak a job-ok a Redis-ben
    public const string ThumbnailStreamKey = "queue:thumbnails";
    public const string VideoStreamKey     = "queue:videos";

    // Consumer group nevek — a Redis Streams consumer group mechanizmusa biztosítja,
    // hogy minden job-ot pontosan egy worker dolgozzon fel
    public const string ThumbnailGroupName = "thumbnail-workers";
    public const string VideoGroupName     = "video-workers";

    // Job berakása a Redis Stream-be; JSON-ná szerializálja az objektumot
    // A stream automatikusan létrejön ha még nem létezik (MKSTREAM flag)
    public async Task EnqueueAsync<T>(string streamKey, T job)
    {
        var db = redis.GetDatabase();
        var json = JsonSerializer.Serialize(job);
        await db.StreamAddAsync(streamKey, [new NameValueEntry("data", json)]);
    }

    // Folyamatosan olvassa a stream-et és adja vissza a job-okat IAsyncEnumerable-ként —
    // ugyanúgy használható mint a korábbi channel.Reader.ReadAllAsync(stoppingToken)
    // Ha nincs új üzenet, 500ms-t vár majd újra próbálja (nem pörög üres loopban)
    public async IAsyncEnumerable<T> DequeueAsync<T>(
        string streamKey,
        string groupName,
        string consumerName,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var db = redis.GetDatabase();

        await EnsureConsumerGroupExistsAsync(db, streamKey, groupName);

        // Újraindítás után feldolgozatlan (pending) üzenetek visszavétele
        // Ezek olyanok, amiket korábban kivettünk a stream-ből de nem XACK-oltunk
        await foreach (var job in ReadEntriesAsync<T>(db, streamKey, groupName, consumerName, "0", ct))
        {
            yield return job;
        }

        // Új üzenetek olvasása folyamatosan
        await foreach (var job in ReadEntriesAsync<T>(db, streamKey, groupName, consumerName, ">", ct))
        {
            yield return job;
        }
    }

    // Segédfüggvény: beolvassa és ACK-olja az entryket a megadott pozíciótól
    // "0" = pending (újraindítás utáni recovery), ">" = csak új üzenetek
    private static async IAsyncEnumerable<T> ReadEntriesAsync<T>(
        IDatabase db,
        string streamKey,
        string groupName,
        string consumerName,
        string position,
        [EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var entries = await db.StreamReadGroupAsync(streamKey, groupName, consumerName, position, count: 1);

            if (entries.Length == 0)
            {
                // Ha "0"-ról (pending) olvasunk és nincs több, készen vagyunk a recoveryvel
                if (position == "0") yield break;

                await Task.Delay(500, ct);
                continue;
            }

            foreach (var entry in entries)
            {
                var json = entry.Values.First(v => v.Name == "data").Value.ToString();
                var job = JsonSerializer.Deserialize<T>(json);

                if (job is not null)
                {
                    // XACK: jelzi a Redis-nek hogy a job feldolgozása sikeres volt
                    // Enélkül a job "pending" maradna és újraindítás után újra feldolgozná
                    await db.StreamAcknowledgeAsync(streamKey, groupName, entry.Id);
                    yield return job;
                }
            }
        }
    }

    // Létrehozza a consumer groupot ha még nem létezik
    // BUSYGROUP: a group már létezik — ez normális, nem hiba
    // MKSTREAM: a stream is létrejön ha még nem volt
    private static async Task EnsureConsumerGroupExistsAsync(IDatabase db, string streamKey, string groupName)
    {
        try
        {
            await db.ExecuteAsync("XGROUP", "CREATE", streamKey, groupName, "$", "MKSTREAM");
        }
        catch (RedisException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group már létezik, nem kell csinálni semmit
        }
    }
}
