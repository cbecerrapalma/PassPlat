using System.Text;
using System.Text.RegularExpressions;
using CBP.Caching.Interfaces;
using CBP.Caching.Models;
using CBP.Results;
using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PassPlat.Datos.Repositories;

namespace PassPlat.Aplicacion.Services.Email;

public partial class EmailTemplateStoreService : IEmailTemplateStoreService
{
    private const string LayoutName = "_layout";
    private const string DefaultCulture = "es";
    private const string BodyPlaceholder = "{{Body}}";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICacheService _cache;
    private readonly ILogger<EmailTemplateStoreService> _logger;
    private static readonly FluidParser _parser = new();
    private static readonly TemplateOptions _options = new();

    public EmailTemplateStoreService(
        IServiceScopeFactory scopeFactory,
        ICacheService cache,
        ILogger<EmailTemplateStoreService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<string>> RenderSubjectAsync(string templateCode, IReadOnlyDictionary<string, object?> variables, string cultura = "es", int? idTenant = null, CancellationToken ct = default)
    {
        var entity = await ResolveTemplateAsync(templateCode, cultura, idTenant, ct);
        if (entity == null)
            return Result<string>.Failure("TEMPLATE_NOT_FOUND", $"Plantilla '{templateCode}' no encontrada");

        var cacheKey = BuildSubjectCacheKey(templateCode, cultura, idTenant);
        var compiled = await _cache.GetAsync<IFluidTemplate>(cacheKey, ct);
        if (compiled == null)
        {
            compiled = _parser.Parse(entity.Asunto);
            await _cache.SetAsync(cacheKey, compiled, new CacheEntryOptions { SlidingExpiration = CacheTtl }, ct);
        }

        var context = BuildContext(variables);
        var rendered = await compiled.RenderAsync(context);
        return Result<string>.Success(rendered);
    }

    public async Task<Result<string>> RenderBodyAsync(string templateCode, IReadOnlyDictionary<string, object?> variables, string cultura = "es", int? idTenant = null, CancellationToken ct = default)
    {
        var entity = await ResolveTemplateAsync(templateCode, cultura, idTenant, ct);
        if (entity == null)
            return Result<string>.Failure("TEMPLATE_NOT_FOUND", $"Plantilla '{templateCode}' no encontrada");

        var body = await RenderTemplateBodyAsync(entity, variables, ct);

        var layout = await GetLayoutAsync(cultura, idTenant, ct);
        if (!string.IsNullOrEmpty(layout))
        {
            var merged = layout.Replace(BodyPlaceholder, body);
            var layoutCompiled = _parser.Parse(merged);
            var layoutContext = BuildContext(variables);
            body = await layoutCompiled.RenderAsync(layoutContext);
        }

        return Result<string>.Success(body);
    }

    public async Task InvalidateCacheAsync(string templateCode, string cultura = "es", int? idTenant = null, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(BuildBodyCacheKey(templateCode, cultura, idTenant), ct);
        await _cache.RemoveAsync(BuildSubjectCacheKey(templateCode, cultura, idTenant), ct);
    }

    public Task InvalidateAllCacheAsync(CancellationToken ct = default)
    {
        _logger.LogWarning("InvalidateAllCacheAsync called — individual template code invalidation recommended instead of full flush");
        return Task.CompletedTask;
    }

    private async Task<Dominio.Entities.Core.EmailTemplate?> ResolveTemplateAsync(string nombre, string cultura, int? idTenant, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var templateRepo = scope.ServiceProvider.GetRequiredService<IEmailTemplateRepository>();

        var result = await templateRepo.ObtenerPorNombreCulturaAsync(nombre, cultura, idTenant, ct);
        if (result.IsSuccess && result.Value != null)
            return result.Value;

        if (idTenant.HasValue)
        {
            result = await templateRepo.ObtenerPorNombreCulturaAsync(nombre, cultura, null, ct);
            if (result.IsSuccess && result.Value != null)
                return result.Value;
        }

        if (!string.Equals(cultura, DefaultCulture, StringComparison.OrdinalIgnoreCase))
        {
            result = await templateRepo.ObtenerPorNombreCulturaAsync(nombre, DefaultCulture, null, ct);
            if (result.IsSuccess && result.Value != null)
                return result.Value;
        }

        return null;
    }

    private async Task<string> GetLayoutAsync(string cultura, int? idTenant, CancellationToken ct)
    {
        var cacheKey = $"layout:{cultura}:{idTenant}";

        var cached = await _cache.GetAsync<string>(cacheKey, ct);
        if (cached != null)
            return cached;

        var layoutEntity = await ResolveTemplateAsync(LayoutName, cultura, idTenant, ct);
        if (layoutEntity == null)
            return string.Empty;

        var body = await ResolvePartialsAsync(layoutEntity.CuerpoHtml, ct);
        await _cache.SetAsync(cacheKey, body, new CacheEntryOptions { SlidingExpiration = CacheTtl }, ct);
        return body;
    }

    private async Task<string> RenderTemplateBodyAsync(Dominio.Entities.Core.EmailTemplate entity, IReadOnlyDictionary<string, object?> variables, CancellationToken ct)
    {
        var cacheKey = $"body:{entity.Nombre}:{entity.Cultura}:{entity.IdTenant}";

        var compiled = await _cache.GetAsync<IFluidTemplate>(cacheKey, ct);
        if (compiled == null)
        {
            var body = await ResolvePartialsAsync(entity.CuerpoHtml, ct);
            compiled = _parser.Parse(body);
            await _cache.SetAsync(cacheKey, compiled, new CacheEntryOptions { SlidingExpiration = CacheTtl }, ct);
        }

        var context = BuildContext(variables);
        return await compiled.RenderAsync(context);
    }

    private static TemplateContext BuildContext(IReadOnlyDictionary<string, object?> variables)
    {
        var context = new TemplateContext(_options);
        foreach (var (key, value) in variables)
            context.SetValue(key, value);
        return context;
    }

    private async Task<string> ResolvePartialsAsync(string body, CancellationToken ct)
    {
        var regex = PartialTagRegex();
        var sb = new StringBuilder();
        var lastIndex = 0;

        foreach (Match match in regex.Matches(body))
        {
            sb.Append(body.AsSpan(lastIndex, match.Index - lastIndex));

            var partialName = match.Groups[1].Value;
            var argsPart = match.Groups[2].Value.Trim();

            var partialBody = await _cache.GetAsync<string>($"partial:{partialName}", ct);
            if (partialBody == null)
            {
                using var scope = _scopeFactory.CreateScope();
                var partialRepo = scope.ServiceProvider.GetRequiredService<IEmailTemplatePartialRepository>();
                var partialResult = await partialRepo.ObtenerPorNombreAsync(partialName, ct);
                if (partialResult.IsFailure || partialResult.Value == null)
                {
                    sb.Append($"<!-- partial '{partialName}' not found -->");
                    lastIndex = match.Index + match.Length;
                    continue;
                }
                partialBody = partialResult.Value.CuerpoHtml;
                await _cache.SetAsync($"partial:{partialName}", partialBody, new CacheEntryOptions { SlidingExpiration = CacheTtl }, ct);
            }

            if (!string.IsNullOrEmpty(argsPart))
            {
                var args = ParsePartialArgs(argsPart);
                var partialSb = new StringBuilder(partialBody);
                foreach (var arg in args)
                    partialSb.Replace($"{{{{{arg.Key}}}}}", arg.Value);
                partialBody = partialSb.ToString();
            }

            sb.Append(partialBody);
            lastIndex = match.Index + match.Length;
        }

        sb.Append(body.AsSpan(lastIndex));
        return sb.ToString();
    }

    private static Dictionary<string, string> ParsePartialArgs(string argsPart)
    {
        var args = new Dictionary<string, string>();
        foreach (Match m in ArgRegex().Matches(argsPart))
            args[m.Groups[1].Value] = m.Groups[2].Value.Trim('"');
        return args;
    }

    private static string BuildBodyCacheKey(string templateCode, string cultura, int? idTenant) =>
        $"body:{templateCode}:{cultura}:{idTenant}";

    private static string BuildSubjectCacheKey(string templateCode, string cultura, int? idTenant) =>
        $"subject:{templateCode}:{cultura}:{idTenant}";

    [GeneratedRegex(@"\{\%\s*partial\s+""([^""]+)""\s*([^\%]*?)\%\}")]
    private static partial Regex PartialTagRegex();

    [GeneratedRegex(@"(\w+):(""[^""]*""|\S+)")]
    private static partial Regex ArgRegex();
}
