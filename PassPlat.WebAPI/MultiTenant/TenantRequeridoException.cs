using System;

namespace PassPlat.WebAPI.MultiTenant;

public sealed class TenantRequeridoException : InvalidOperationException
{
    public TenantRequeridoException()
        : base("Tenant not set in JWT. Ensure the endpoint is [Authorize] and the token contains TenantId claim.")
    {
    }
}
