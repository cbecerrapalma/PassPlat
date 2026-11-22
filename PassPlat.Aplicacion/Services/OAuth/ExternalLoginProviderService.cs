using CBP.Results;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Constants;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services.OAuth;

public interface IExternalLoginProviderService
{
    // S126 · D5/D9 — por proveedor: la configuración válida del tenant o, si no tiene, la de PLATFORM; y solo las que sirven a la App
    Task<Result<IReadOnlyList<ExternalLoginProviderDto>>> ObtenerDisponiblesAsync(int idTenant, int idApp, CancellationToken ct = default);
    // S126 · D9 — la misma regla para un proveedor: null = no hay; PROVIDER_NOT_ENABLED_FOR_APP = la del tenant no sirve a esa App
    Task<Result<ConfProvIden?>> ResolverConfiguracionAsync(int idTenant, int idProvIden, int idApp, CancellationToken ct = default);
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

    public async Task<Result<IReadOnlyList<ExternalLoginProviderDto>>> ObtenerDisponiblesAsync(int idTenant, int idApp, CancellationToken ct = default)
    {
        try
        {
            var lista = (await ObtenerEfectivasAsync(idTenant, ct))
                .Where(e => e.Conf.SirveParaApp(idApp))
                .OrderBy(e => e.Conf.OrdenVisual)
                .Select(e => new ExternalLoginProviderDto
                {
                    Codigo = e.Conf.ProvIden!.Codigo,
                    Nombre = e.Conf.Tooltip ?? e.Conf.ProvIden.Nombre,
                    Icono = e.Conf.ProvIden.Icono ?? e.Conf.ProvIden.Codigo.ToLowerInvariant(),
                    Color = e.Conf.Color,
                    Tooltip = e.Conf.Tooltip,
                    OrdenVisual = e.Conf.OrdenVisual,
                    EsDePlataforma = e.EsDePlataforma
                })
                .ToList();

            return Result<IReadOnlyList<ExternalLoginProviderDto>>.Success(lista);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ExternalLoginProviderDto>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<ConfProvIden?>> ResolverConfiguracionAsync(int idTenant, int idProvIden, int idApp, CancellationToken ct = default)
    {
        try
        {
            var conf = (await ObtenerEfectivasAsync(idTenant, ct))
                .Where(e => e.Conf.IdProvIden == idProvIden)
                .Select(e => e.Conf)
                .FirstOrDefault();
            if (conf is not null && !conf.SirveParaApp(idApp))
                return Result<ConfProvIden?>.Failure("PROVIDER_NOT_ENABLED_FOR_APP", "El proveedor no está habilitado para esta aplicación");
            return Result<ConfProvIden?>.Success(conf, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<ConfProvIden?>.Failure("DB_ERROR", ex.Message);
        }
    }

    // S126 · D9 — una por proveedor: la válida del tenant; si el tenant no tiene una válida de ese proveedor
    // y MostrarProveedoresPlataforma está activo, la válida de PLATFORM (antes: solo si el tenant no tenía ninguna)
    private async Task<List<(ConfProvIden Conf, bool EsDePlataforma)>> ObtenerEfectivasAsync(int idTenant, CancellationToken ct)
    {
        var propias = await ObtenerValidasAsync(idTenant, ct);
        var efectivas = propias.Select(c => (Conf: c, EsDePlataforma: false)).ToList();

        if (!await DebeHeredarProveedoresPlataformaAsync(ct))
            return efectivas;
        var plataforma = await ObtenerTenantPlataformaAsync(ct);
        if (plataforma is null || plataforma.Id == idTenant)
            return efectivas;

        var conPropia = propias.Select(c => c.IdProvIden).ToHashSet();
        efectivas.AddRange((await ObtenerValidasAsync(plataforma.Id, ct))
            .Where(c => !conPropia.Contains(c.IdProvIden))
            .Select(c => (Conf: c, EsDePlataforma: true)));
        return efectivas;
    }

    private async Task<IReadOnlyList<ConfProvIden>> ObtenerValidasAsync(int idTenant, CancellationToken ct)
    {
        var configsResult = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (configsResult.IsFailure || configsResult.Value is null || configsResult.Value.Count == 0)
            return [];

        return configsResult.Value
            .Where(c => _validator.Validar(c).Disponible)
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
