using System.Security.Claims;
using CBP.MultiTenant.Abstractions;

namespace PassPlat.WebAPI.MultiTenant;

public class JwtTenantContext : ITenantContext
{
    private TenantInfo? _tenant;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JwtTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public TenantInfo? Current
    {
        get
        {
            if (_tenant is null)
                TryResolveFromJwt();
            return _tenant;
        }
    }

    public int CurrentId => Current?.Id
        ?? throw new InvalidOperationException("Tenant not set in JWT. Ensure the endpoint is [Authorize] and the token contains TenantId claim.");

    public bool HasTenant => Current is not null;

    public void Set(TenantInfo tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        _tenant = tenant;
    }

    public void Override(TenantInfo tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        _tenant = tenant;
    }

    public void Clear() => _tenant = null;

    private void TryResolveFromJwt()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var tenantIdClaim = httpContext?.User?.FindFirstValue("TenantId");
        if (tenantIdClaim is not null && int.TryParse(tenantIdClaim, out var id))
        {
            _tenant = TenantInfo.Create(id, "JWT", "From JWT Claim");
        }
    }
}
