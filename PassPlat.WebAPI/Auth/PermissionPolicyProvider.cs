using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace PassPlat.WebAPI.Auth;

public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    private static readonly HashSet<string> _knownPolicies = ["SystemOnly"];

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (_knownPolicies.Contains(policyName))
            return _fallback.GetPolicyAsync(policyName);

        var policy = new AuthorizationPolicyBuilder()
            .RequireClaim("permiso", policyName)
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}
