using System.Diagnostics;
using CBP.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace PassPlat.WebAPI.Middleware;

public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Items[LoggingPropertyNames.HttpCorrelationIdKey] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty(LoggingPropertyNames.CorrelationId, correlationId))
        {
            try
            {
                await _next(context);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var values) &&
            values.Count > 0 &&
            !string.IsNullOrWhiteSpace(values[0]))
        {
            return values[0]!;
        }

        return Activity.Current?.Id ?? context.TraceIdentifier ?? Guid.NewGuid().ToString("N");
    }
}
