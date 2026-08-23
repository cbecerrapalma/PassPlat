using CBP.MultiTenant.Abstractions;
using CBP.Results;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.WebAPI.MultiTenant;

public class PassPlatTenantResolver : ITenantResolver<Tenant>
{
    private readonly ITenantRepository _repo;

    public PassPlatTenantResolver(ITenantRepository repo) => _repo = repo;

    public async Task<Result<Tenant?>> FindByCodigoAsync(string codigo, CancellationToken ct = default)
        => await _repo.ObtenerPorCodigoAsync(codigo, ct);
}
