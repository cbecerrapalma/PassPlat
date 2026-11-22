using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PassPlat.WebAPI.Middleware;

public sealed class RequestLoggingMiddleware
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
        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente abortó el request (p. ej. navegó a otra página): no es un fallo del servidor.
            sw.Stop();
            _logger.LogInformation("HTTP {Method} {Path} cancelled by client in {ElapsedMs}ms",
                method, path, sw.ElapsedMilliseconds);
            throw;
        }
        catch (Exception)
        {
            sw.Stop();
            _logger.LogError("HTTP {Method} {Path} failed in {ElapsedMs}ms",
                method, path, sw.ElapsedMilliseconds);
            throw;
        }

        sw.Stop();
        var statusCode = context.Response.StatusCode;
        var level = statusCode >= 500 ? LogLevel.Error
                  : statusCode >= 400 ? LogLevel.Warning
                  : LogLevel.Information;

        _logger.Log(level,
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
            method, path, statusCode, sw.ElapsedMilliseconds);
    }
}
