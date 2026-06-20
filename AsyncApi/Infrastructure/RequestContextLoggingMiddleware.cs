using Microsoft.Extensions.Primitives;
using Serilog.Context;
using System.Text.RegularExpressions;

namespace AsyncApi.Infrastructure;

public sealed class RequestContextLoggingMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";
    private static readonly Regex SafeCorrelationId = new(@"^[a-zA-Z0-9\-]{1,64}$", RegexOptions.Compiled);

    public async Task Invoke(HttpContext context)
    {
        string correlationId = GetCorrelationId(context);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next.Invoke(context);
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out StringValues correlationId);

        var value = correlationId.FirstOrDefault();
        return value is not null && SafeCorrelationId.IsMatch(value)
            ? value
            : context.TraceIdentifier;
    }
}

public static class RequestContextLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestContextLogging(this IApplicationBuilder app)
    {
        app.UseMiddleware<RequestContextLoggingMiddleware>();
        return app;
    }
}
