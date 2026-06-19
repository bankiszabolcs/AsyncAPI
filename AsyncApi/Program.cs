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
                }).AllowAnyHeader().AllowAnyMethod();
            else
                policy.WithOrigins(builder.Configuration["Cors:AllowedOrigin"]
                    ?? throw new InvalidOperationException("Cors:AllowedOrigin nincs beállítva."))
                    .AllowAnyHeader().AllowAnyMethod();
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

    // --- Keycloak JWT autentikáció ---
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority            = builder.Configuration["Keycloak:Authority"];
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                //ValidateIssuer   = !builder.Environment.IsDevelopment(),
                ValidIssuer      = builder.Configuration["Keycloak:ValidIssuer"],
                NameClaimType    = "preferred_username",
            };
        });
    builder.Services.AddAuthorization();

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
