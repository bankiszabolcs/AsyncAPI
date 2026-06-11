using AsyncApi.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AsyncApi.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException        => (StatusCodes.Status404NotFound,            "Not Found"),
            AppValidationException   => (StatusCodes.Status400BadRequest,          "Validation Error"),
            _                        => (StatusCodes.Status500InternalServerError,  "Internal Server Error")
        };

        logger.LogError(exception, "Unhandled exception: {Title}", title);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Type   = $"https://httpstatuses.com/{status}",
            Title  = title,
            Status = status,
            Detail = exception.Message
        }, ct);

        return true;
    }
}
