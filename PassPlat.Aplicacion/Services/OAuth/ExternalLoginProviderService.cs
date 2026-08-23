using CBP.Results;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Constants;

namespace PassPlat.Aplicacion.Services.OAuth;

public interface IExternalLoginProviderService
{
    Task<Result<IReadOnlyList<ExternalLoginProviderDto>>> ObtenerDisponiblesAsync(int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProviderAvailabilityResultDto>>> ObtenerDiagnosticoAsync(int idTenant, CancellationToken ct = default);
}

public class ExternalLoginProviderService : IExternalLoginProviderService
{
    private readonly IConfProvIdenRepository _repo;
    private readonly IExternalProviderValidator _validator;
    private readonly IConfigAppRepository _configAppRepo;
    private readonly ITenantRepository _tenantRepo;

    public ExternalLoginProviderService(
        IConfProvIdenRepository repo,
        IExternalProviderValidator validator,
        IConfigAppRepository configAppRepo,
        ITenantRepository tenantRepo)
    {
        _repo = repo;
        _validator = validator;
        _configAppRepo = configAppRepo;
        _tenantRepo = tenantRepo;
    }

    public async Task<Result<IReadOnlyList<ExternalLoginProviderDto>>> ObtenerDisponiblesAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var propios = await ObtenerValidosAsync(idTenant, esDePlataforma: false, ct);

            if (propios.Count == 0 && await DebeHeredarProveedoresPlataformaAsync(ct))
            {
                var plataforma = await ObtenerTenantPlataformaAsync(ct);
                if (plataforma is not null && plataforma.Id != idTenant)
                {
                    var heredados = await ObtenerValidosAsync(plataforma.Id, esDePlataforma: true, ct);
                    if (heredados.Count > 0)
                        return Result<IReadOnlyList<ExternalLoginProviderDto>>.Success(heredados);
                }
            }

            return Result<IReadOnlyList<ExternalLoginProviderDto>>.Success(propios);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ExternalLoginProviderDto>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<ProviderAvailabilityResultDto>>> ObtenerDiagnosticoAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var configsResult = await _repo.ObtenerPorTenantAsync(idTenant, ct);
            if (configsResult.IsFailure || configsResult.Value is null || configsResult.Value.Count == 0)
                return Result<IReadOnlyList<ProviderAvailabilityResultDto>>.Success([]);

            var result = configsResult.Value
                .OrderBy(c => c.OrdenVisual)
                .Select(c =>
                {
                    var validation = _validator.Validar(c);
                    return new ProviderAvailabilityResultDto
                    {
                        Codigo = c.ProvIden?.Codigo ?? "",
                        Nombre = c.Tooltip ?? c.ProvIden?.Nombre ?? "",
                        Disponible = validation.Disponible,
                        Motivo = validation.Motivo
                    };
                })
                .ToList();

            return Result<IReadOnlyList<ProviderAvailabilityResultDto>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ProviderAvailabilityResultDto>>.Failure("DB_ERROR", ex.Message);
        }
    }

    private async Task<IReadOnlyList<ExternalLoginProviderDto>> ObtenerValidosAsync(int idTenant, bool esDePlataforma, CancellationToken ct)
    {
        var configsResult = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (configsResult.IsFailure || configsResult.Value is null || configsResult.Value.Count == 0)
            return [];

        return configsResult.Value
            .Where(c => _validator.Validar(c).Disponible)
            .OrderBy(c => c.OrdenVisual)
            .Select(c => new ExternalLoginProviderDto
            {
                Codigo = c.ProvIden!.Codigo,
                Nombre = c.Tooltip ?? c.ProvIden.Nombre,
                Icono = c.ProvIden.Icono ?? c.ProvIden.Codigo.ToLowerInvariant(),
                Color = c.Color,
                Tooltip = c.Tooltip,
                OrdenVisual = c.OrdenVisual,
                EsDePlataforma = esDePlataforma
            })
            .ToList();
    }

    private async Task<bool> DebeHeredarProveedoresPlataformaAsync(CancellationToken ct)
    {
        var cfgResult = await _configAppRepo.ObtenerPorClaveAsync(ConfigAppKeys.MostrarProveedoresPlataforma, null, ct);
        if (cfgResult.IsFailure || cfgResult.Value is null || !cfgResult.Value.Activo)
            return false;

        return string.Equals(cfgResult.Value.Valor, "true", StringComparison.OrdinalIgnoreCase)
            || cfgResult.Value.Valor == "1";
    }

    private async Task<Dominio.Entities.Catalogos.Tenant?> ObtenerTenantPlataformaAsync(CancellationToken ct)
    {
        var result = await _tenantRepo.ObtenerPorCodigoAsync(TenantCodes.Plataforma, ct);
        return result.IsSuccess ? result.Value : null;
    }
}
