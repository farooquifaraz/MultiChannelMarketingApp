using System.Diagnostics;

namespace MarketingApp.API.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.Items["CorrelationId"]?.ToString();
        var path = context.Request.Path;

        _logger.LogInformation(
            "HTTP {Method} {Path} started | CorrelationId: {CorrelationId} | IP: {IP}",
            context.Request.Method, path, correlationId,
            context.Connection.RemoteIpAddress);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var level = context.Response.StatusCode >= 500 ? LogLevel.Error
                       : context.Response.StatusCode >= 400 ? LogLevel.Warning
                       : LogLevel.Information;

            _logger.Log(level,
                "HTTP {Method} {Path} responded {StatusCode} in {Elapsed}ms | CorrelationId: {CorrelationId}",
                context.Request.Method, path,
                context.Response.StatusCode, stopwatch.ElapsedMilliseconds,
                correlationId);

            if (stopwatch.ElapsedMilliseconds > 2000)
                _logger.LogWarning("SLOW REQUEST: {Path} took {Elapsed}ms", path, stopwatch.ElapsedMilliseconds);
        }
    }
}
