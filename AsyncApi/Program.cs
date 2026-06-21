using AsyncApi.Data;
using AsyncApi.Data.Repositories;
using AsyncApi.Infrastructure;
using AsyncApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;
using System.Security.Claims;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, loggerConfig) =>
        loggerConfig.ReadFrom.Configuration(context.Configuration));

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Angular", policy =>
        {
            if (builder.Environment.IsDevelopment())
                policy.SetIsOriginAllowed(origin =>
                {
                    var host = new Uri(origin).Host;
                    return host == "localhost" || host.StartsWith("192.168.");
                });
            else
                policy.WithOrigins(builder.Configuration["Cors:AllowedOrigin"]
                    ?? throw new InvalidOperationException("Cors:AllowedOrigin nincs beállítva."));

            policy.AllowAnyHeader()
                  .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE");
        });
    });

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
    builder.Services.AddScoped<UserRepository>();
    builder.Services.AddScoped<CommentRepository>();
    builder.Services.AddScoped<PlaylistRepository>();
    builder.Services.AddScoped<SavedVideoRepository>();
    builder.Services.AddScoped<WatchLaterRepository>();
    builder.Services.AddScoped<SubscriptionRepository>();
    builder.Services.AddScoped<NotificationRepository>();
    builder.Services.AddScoped<NotificationService>();

    // --- Keycloak JWT autentikáció ---
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority            = builder.Configuration["Keycloak:Authority"];
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidAudience    = builder.Configuration["Keycloak:ValidAudience"],
                ValidateIssuer   = true,
                ValidIssuer      = builder.Configuration["Keycloak:ValidIssuer"],
                NameClaimType    = "preferred_username",
            };
        });
    builder.Services.AddAuthorization();

    // --- Rate limiting ---
    builder.Services.AddRateLimiter(rl =>
    {
        rl.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Globális alap: minden IP-re 200 req/perc — brutális flood ellen
        rl.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            var ip = ctx.Request.Headers["X-Real-IP"].FirstOrDefault()
                     ?? ctx.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window      = TimeSpan.FromMinutes(1),
                QueueLimit  = 0
            });
        });

        // Feltöltés: 5 db / 10 perc userenként — tárhely, FFmpeg CPU, Gemini kvóta védelme
        rl.AddPolicy("upload", ctx =>
        {
            var key = ctx.User.FindFirstValue("sub")
                      ?? ctx.Request.Headers["X-Real-IP"].FirstOrDefault()
                      ?? ctx.Connection.RemoteIpAddress?.ToString()
                      ?? "unknown";
            return RateLimitPartition.GetSlidingWindowLimiter($"upload:{key}", _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit      = 5,
                Window           = TimeSpan.FromMinutes(10),
                SegmentsPerWindow = 5,
                QueueLimit       = 0
            });
        });

        // Keresés: 60 req/perc IP-nként — DB lekérdezések ellen
        rl.AddPolicy("search", ctx =>
        {
            var ip = ctx.Request.Headers["X-Real-IP"].FirstOrDefault()
                     ?? ctx.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter($"search:{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window      = TimeSpan.FromMinutes(1),
                QueueLimit  = 0
            });
        });

        // Playlist írás: 30 mutáció/perc userenként — tömeges lista/videó spam ellen
        rl.AddPolicy("playlist", ctx =>
        {
            var key = ctx.User.FindFirstValue("sub")
                      ?? ctx.Request.Headers["X-Real-IP"].FirstOrDefault()
                      ?? ctx.Connection.RemoteIpAddress?.ToString()
                      ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter($"playlist:{key}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window      = TimeSpan.FromMinutes(1),
                QueueLimit  = 0
            });
        });
    });

    builder.Services.AddSingleton<QueueService>();
    builder.Services.AddSingleton<StatusService>();
    builder.Services.AddSingleton<CommentSseService>();

    // --- Tárhely (MinIO) ---
    builder.Services.AddSingleton<StorageService>();

    // --- Kép feldolgozás ---
    builder.Services.AddSingleton<ImageService>();
    builder.Services.AddHostedService<ThumbnailGenerationService>();

    // --- Videó feldolgozás ---
    builder.Services.AddSingleton<VideoService>();
    builder.Services.AddHostedService<VideoProcessingService>();

    // --- AI tag generálás ---
    builder.Services.AddHttpClient();
    builder.Services.AddScoped<VideoTagService>();

    // --- Megtekintés rögzítés ---
    builder.Services.AddHostedService<ViewWorkerService>();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference();
        app.MapOpenApi();
    }

    app.UseExceptionHandler();
    app.UseCors("Angular");
    app.UseRequestContextLogging();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();
    app.MapControllers();

    // Redis → SSE bridge: minden API instance figyeli a comments:* channel-t
    var commentSse = app.Services.GetRequiredService<CommentSseService>();
    var redisSubscriber = app.Services.GetRequiredService<IConnectionMultiplexer>().GetSubscriber();
    await redisSubscriber.SubscribeAsync(RedisChannel.Pattern("comments:*"), async (channel, msg) =>
    {
        if (msg.IsNull) return;
        var raw = (string)channel!;
        var videoIdStr = raw["comments:".Length..];
        if (Guid.TryParse(videoIdStr, out var videoId))
            await commentSse.BroadcastAsync(videoId, msg!);
    });

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
