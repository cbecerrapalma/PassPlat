using CBP.Results;

namespace PassPlat.Aplicacion.Services.Email;

public interface IEmailTemplateStoreService
{
    Task<Result<string>> RenderSubjectAsync(string templateCode, IReadOnlyDictionary<string, object?> variables, string cultura = "es", int? idTenant = null, CancellationToken ct = default);
    Task<Result<string>> RenderBodyAsync(string templateCode, IReadOnlyDictionary<string, object?> variables, string cultura = "es", int? idTenant = null, CancellationToken ct = default);
    Task InvalidateCacheAsync(string templateCode, string cultura = "es", int? idTenant = null, CancellationToken ct = default);
    Task InvalidateAllCacheAsync(CancellationToken ct = default);
}
