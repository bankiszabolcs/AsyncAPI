using AsyncApi.Data;
using AsyncApi.Data.Repositories;
using AsyncApi.Infrastructure;
using AsyncApi.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, loggerConfig) =>
        loggerConfig.ReadFrom.Configuration(context.Configuration));

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // --- Redis ---
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var connectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
        return ConnectionMultiplexer.Connect(connectionString);
    });

    // --- Adatbázis (PostgreSQL) ---
    builder.Services.AddDbContext<AsyncApiDbContext>(options =>
        options.UseNpgsql(builder.Configuration["Database:ConnectionString"]));

    builder.Services.AddScoped<VideoRepository>();
    builder.Services.AddScoped<ImageRepository>();

    builder.Services.AddSingleton<QueueService>();
    builder.Services.AddSingleton<StatusService>();

    // --- Tárhely (MinIO) ---
    builder.Services.AddSingleton<StorageService>();

    // --- Kép feldolgozás ---
    builder.Services.AddSingleton<ImageService>();
    builder.Services.AddHostedService<ThumbnailGenerationService>();

    // --- Videó feldolgozás ---
    builder.Services.AddSingleton<VideoService>();
    builder.Services.AddHostedService<VideoProcessingService>();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference();
        app.MapOpenApi();
    }

    app.UseExceptionHandler();
    app.UseRequestContextLogging();
    app.UseSerilogRequestLogging();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
