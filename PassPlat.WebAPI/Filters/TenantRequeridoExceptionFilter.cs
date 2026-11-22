using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using PassPlat.WebAPI.MultiTenant;

namespace PassPlat.WebAPI.Filters;

public sealed class TenantRequeridoExceptionFilter : IExceptionFilter
{
    private readonly ILogger<TenantRequeridoExceptionFilter> _logger;

    public TenantRequeridoExceptionFilter(ILogger<TenantRequeridoExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not TenantRequeridoException)
        {
            return;
        }

        _logger.LogWarning(context.Exception, "Solicitud sin sesión de tenant");

        var path = context.HttpContext.Request.Path;
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            Title = "Forbidden",
            Detail = "Esta operación requiere una sesión dentro de un tenant. Entra a un tenant e inténtalo de nuevo.",
            Status = 403,
            Instance = path
        };
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        problem.Extensions["errorCode"] = "TENANT_REQUIRED";

        context.ExceptionHandled = true;
        context.HttpContext.Response.ContentType = "application/problem+json";
        context.Result = new ObjectResult(problem) { StatusCode = 403 };
    }
}
