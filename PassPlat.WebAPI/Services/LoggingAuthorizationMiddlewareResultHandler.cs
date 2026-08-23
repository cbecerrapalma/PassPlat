using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PassPlat.WebAPI.Services;

public sealed class LoggingAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _inner = new();
    private readonly ILogger<LoggingAuthorizationMiddlewareResultHandler> _logger;

    public LoggingAuthorizationMiddlewareResultHandler(ILogger<LoggingAuthorizationMiddlewareResultHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult result)
    {
        var path = context.Request.Path;
        var endpoint = context.GetEndpoint()?.DisplayName ?? "-";

        if (result.Succeeded)
        {
            _logger.LogInformation(
                "AUTHZ OK: Path={Path} Endpoint={Ep} Requirements={Req}",
                path, endpoint, policy.Requirements.Count);
        }
        else
        {
            var fail = result.AuthorizationFailure;
            _logger.LogWarning(
                "AUTHZ **FAIL**: Path={Path} Endpoint={Ep}\n" +
                "  FailureReasons={Reasons}\n" +
                "  FailedRequirements={FailedReqs}\n" +
                "  Policy Requirements:\n{Requirements}",
                path, endpoint,
                fail != null
                    ? string.Join(" | ", fail.FailureReasons.Select(r => $"[{r.GetType().Name}] {r.Message}"))
                    : "(null - possibly Challenge/Forbid result)",
                fail != null
                    ? string.Join(", ", fail.FailedRequirements.Select(DescribeRequirement))
                    : "(null)",
                string.Join("\n  ", policy.Requirements.Select(DescribeRequirement)));
        }

        await _inner.HandleAsync(next, context, policy, result);
    }

    private static string DescribeRequirement(IAuthorizationRequirement requirement)
    {
        if (requirement is ClaimsAuthorizationRequirement cr)
        {
            var allowed = cr.AllowedValues != null
                ? string.Join(",", cr.AllowedValues.Select(v => $"'{v}'"))
                : "(any)";
            return $"ClaimsAuthorizationRequirement [ClaimType='{cr.ClaimType}', AllowedValues=[{allowed}]]";
        }

        if (requirement is DenyAnonymousAuthorizationRequirement)
            return "DenyAnonymousAuthorizationRequirement";

        if (requirement is AssertionRequirement)
            return "AssertionRequirement";

        return $"{requirement.GetType().Name}: {requirement}";
    }
}
