using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PassPlat.WebAPI.Middleware;

public sealed class DiagnosticAfterAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DiagnosticAfterAuthMiddleware> _logger;

    public DiagnosticAfterAuthMiddleware(RequestDelegate next, ILogger<DiagnosticAfterAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var isAuth = context.User.Identity?.IsAuthenticated ?? false;

        await _next(context);

        _logger.LogWarning(
            "DIAGNOSTIC AUTH [AFTER]: Path={Path} Status={Status} Auth={Auth} Endpoint={Ep}",
            context.Request.Path, context.Response.StatusCode, isAuth,
            endpoint?.DisplayName ?? "-");
    }
}
