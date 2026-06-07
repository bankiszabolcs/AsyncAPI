using AsyncApi.Services;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Engedélyezi az API controllereket (pl. ThumbnailsController, VideosController)
builder.Services.AddControllers();

// Beépített .NET 10 OpenAPI dokumentáció generálás (Swashbuckle nélkül)
builder.Services.AddOpenApi();

// --- Redis ---

// Egyetlen kapcsolat az egész app életciklusa alatt; thread-safe, megosztható minden service között
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});

builder.Services.AddSingleton<QueueService>();
builder.Services.AddSingleton<StatusService>();

// --- Tárhely (MinIO) ---

// MinIO S3-kompatibilis tárhellyel kommunikál; fájlok feltöltése és publikus URL generálás
builder.Services.AddSingleton<StorageService>();

// --- Kép feldolgozás ---

// Képvalidálásért, thumbnail készítésért és MinIO feltöltésért felelős service
builder.Services.AddSingleton<ImageService>();

// Háttérszolgáltatás, amely folyamatosan dolgozza fel a Redis queue-ból érkező thumbnail job-okat
builder.Services.AddHostedService<ThumbnailGenerationService>();

// --- Videó feldolgozás ---

// HLS generálásért, sprite készítésért és MinIO feltöltésért felelős service
builder.Services.AddSingleton<VideoService>();

// Háttérszolgáltatás, amely folyamatosan dolgozza fel a Redis queue-ból érkező videó job-okat
builder.Services.AddHostedService<VideoProcessingService>();

var app = builder.Build();

// OpenAPI JSON végpont és Scalar UI csak fejlesztői környezetben elérhető
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
    app.MapOpenApi();
}

// Engedélyezi az autorizációs middleware-t (szükséges a MapControllers előtt)
app.UseAuthorization();

// Bekötik a controllerekben definiált route-okat a HTTP pipeline-ba
app.MapControllers();

app.Run();
