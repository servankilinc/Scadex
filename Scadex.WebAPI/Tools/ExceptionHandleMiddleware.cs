using Microsoft.AspNetCore.Diagnostics;

namespace Scadex.WebAPI.Tools;

public class ExceptionHandleMiddleware : IExceptionHandler
{
    private readonly ILogger<ExceptionHandleMiddleware> _logger;
    public ExceptionHandleMiddleware(ILogger<ExceptionHandleMiddleware> logger) => _logger = logger;


    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        // Gecersiz dinamik filtre/sort istekleri ArgumentException firlatir. Bunlar istemci
        // hatasidir; 500 donmek hem yaniltici hem de gereksiz alarm uretir.
        if (exception is ArgumentException)
        {
            _logger.LogWarning(exception, "Invalid request argument. TraceId: {TraceId}, Message: {Message}", traceId, exception.Message);

            httpContext.Response.ContentType = "application/problem+json";
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails()
            {
                Status = StatusCodes.Status400BadRequest,
                Type = $"http://Scadex.com/problems/BadRequest",
                Title = "The request could not be processed",
                Detail = exception.Message,
                Extensions =
                {
                    ["traceId"] = traceId
                }
            });
            return true;
        }

        _logger.LogError(exception, "An error occurred during the process. TraceId: {TraceId}, Message: {Message}, InnerException: {InnerException}", traceId, exception.Message, exception.InnerException?.Message ?? string.Empty);

        httpContext.Response.ContentType = "application/problem+json";
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails()
        {
            Status = StatusCodes.Status500InternalServerError,
            Type = $"http://Scadex.com/problems/InternalServerError",
            Title = "An error occurred",
            Extensions =
            {
                ["traceId"] = traceId
            }
        });
        return true;
    }
}