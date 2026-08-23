using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace PassPlat.WebAPI.Middleware;

public sealed class LoggingScopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingScopeMiddleware> _logger;

    public LoggingScopeMiddleware(RequestDelegate next, ILogger<LoggingScopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.Items["ResolvedTenantId"] as int?;
        var userId = context.User?.FindFirst("sub")?.Value
                  ?? context.User?.FindFirst("nameidentifier")?.Value;

        using (LogContext.PushProperty("TenantId", tenantId ?? 0))
        using (LogContext.PushProperty("UserId", userId ?? "anonymous"))
        {
            await _next(context);
        }
    }
}
