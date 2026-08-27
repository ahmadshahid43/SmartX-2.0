using Microsoft.AspNetCore.Diagnostics;

namespace OmniBusiness.Api.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception while processing {Path}.", httpContext.Request.Path);

        var response = exception switch
        {
            InvalidOperationException invalidOperationException => new ApiErrorResponse(
                false,
                "INVALID_OPERATION",
                invalidOperationException.Message,
                Array.Empty<string>()),
            _ => new ApiErrorResponse(
                false,
                "SERVER_ERROR",
                "The server was unable to complete the request.",
                Array.Empty<string>())
        };

        httpContext.Response.StatusCode = exception is InvalidOperationException
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
