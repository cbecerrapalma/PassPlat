using CBP.Caching.Interfaces;
using CBP.Caching.Models;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.WebAPI.Middleware;

public class TenantResolutionMiddleware
{
    private const int NotFoundSentinel = -1;
    private readonly RequestDelegate _next;
    private readonly ICacheService _cache;
    private readonly ICacheKeyGenerator _keyGen;

    public TenantResolutionMiddleware(RequestDelegate next, ICacheService cache, ICacheKeyGenerator keyGen)
    {
        _next = next;
        _cache = cache;
        _keyGen = keyGen;
    }

    public async Task InvokeAsync(HttpContext context, IDominioTenantRepository repo)
    {
        var host = context.Request.Host.Host;
        if (string.IsNullOrEmpty(host))
        {
            await _next(context);
            return;
        }

        var cacheKey = _keyGen.Generate<DominioTenant>(host.ToLowerInvariant());
        int? tenantId = await _cache.GetOrCreateAsync<int>(
            cacheKey,
            async ct =>
            {
                var result = await repo.ObtenerPorDominioAsync(host, ct);
                return result.IsSuccess && result.Value != null ? result.Value.IdTenant : NotFoundSentinel;
            },
            new CacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) },
            context.RequestAborted);

        if (tenantId.HasValue && tenantId.Value > 0)
            context.Items["ResolvedTenantId"] = tenantId.Value;

        await _next(context);
    }
}
