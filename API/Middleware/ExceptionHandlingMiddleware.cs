using System.Net;
using System.Text.Json;

namespace API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var isDev = context.RequestServices
            .GetRequiredService<IWebHostEnvironment>().IsDevelopment();

        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized,      exception.Message),
            ArgumentException           => (HttpStatusCode.BadRequest,        exception.Message),
            InvalidOperationException   => (HttpStatusCode.BadRequest,        exception.Message),
            KeyNotFoundException        => (HttpStatusCode.NotFound,          exception.Message),
            _                           => (HttpStatusCode.InternalServerError,
                                            isDev ? exception.ToString() : "An unexpected error occurred")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = (int)statusCode;

        var response = JsonSerializer.Serialize(new
        {
            error     = message,
            status    = (int)statusCode,
            timestamp = DateTime.UtcNow
        });

        return context.Response.WriteAsync(response);
    }
}
