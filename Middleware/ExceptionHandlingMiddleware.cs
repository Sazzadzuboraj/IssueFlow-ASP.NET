using System.Net;
using System.Text.Json;

namespace IssueFlow.Middleware;

/// <summary>
/// Logs unhandled exceptions. API paths get JSON ProblemDetails;
/// MVC paths rethrow so the pipeline / error page can handle them.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}: {Message}",
                context.Request.Method, context.Request.Path, ex.Message);

            var isApi = context.Request.Path.StartsWithSegments("/api")
                        || (context.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
                            && !context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase));

            if (isApi)
            {
                await WriteJsonErrorAsync(context, ex);
            }
            else
            {
                // Let ASP.NET Core developer exception page / UseExceptionHandler handle MVC
                throw;
            }
        }
    }

    private async Task WriteJsonErrorAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
            throw exception;

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var problem = new
        {
            status = context.Response.StatusCode,
            title = "An unexpected error occurred.",
            detail = _env.IsDevelopment() ? exception.Message : "Please try again later.",
            stackTrace = _env.IsDevelopment() ? exception.StackTrace : null
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
    }
}
