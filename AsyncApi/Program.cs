using System.Collections.Concurrent;
using System.Threading.Channels;
using AsyncApi.Models;
using AsyncApi.Services;
using VideoProcessingStatus = AsyncApi.Models.VideoProcessingStatus;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Engedélyezi az API controllereket (pl. ThumbnailsController)
builder.Services.AddControllers();

// Beépített .NET 10 OpenAPI dokumentáció generálás (Swashbuckle nélkül)
builder.Services.AddOpenApi();

// Képvalidálásért és thumbnail készítésért felelős service
builder.Services.AddSingleton<ImageService>();

// Aszinkron job-sor: max 100 elemet tárol, ha tele van, a beírás vár (nem dob hibát)
// A controller ír bele, a ThumbnailGenerationService olvas ki belőle
builder.Services.AddSingleton(_ =>
{
    var channel = Channel.CreateBounded<ThumbnailGenerationJob>(new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.Wait
    });

    return channel;
});

// Thread-safe szótár a job-ok állapotának nyomon követéséhez (jobId -> státusz)
builder.Services.AddSingleton<ConcurrentDictionary<string, ThumbnailGenerationStatus>>();

// Háttérszolgáltatás, amely folyamatosan dolgozza fel a channel-ből érkező job-okat
builder.Services.AddHostedService<ThumbnailGenerationService>();

// --- Videó feldolgozás ---

builder.Services.AddSingleton<VideoService>();

// Külön channel és státusz szótár a videó job-oknak (független a thumbnail pipeline-tól)
builder.Services.AddSingleton(_ =>
{
    var channel = Channel.CreateBounded<VideoProcessingJob>(new BoundedChannelOptions(50)
    {
        FullMode = BoundedChannelFullMode.Wait
    });

    return channel;
});
builder.Services.AddSingleton<ConcurrentDictionary<string, VideoProcessingStatus>>();

builder.Services.AddHostedService<VideoProcessingService>();

var app = builder.Build();

// OpenAPI JSON végpont csak fejlesztői környezetben elérhető (/openapi/v1.json)
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
    app.MapOpenApi();
}

// HTTP kéréseket automatikusan HTTPS-re irányítja át
app.UseHttpsRedirection();

// Engedélyezi az autorizációs middleware-t (szükséges a MapControllers előtt)
app.UseAuthorization();

// Bekötik a controllerekben definiált route-okat a HTTP pipeline-ba
app.MapControllers();

app.Run();
