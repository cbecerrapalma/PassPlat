using AutoMapper;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface IConfigTenantService : IServiceAsync<ConfigTenant, ConfigTenantDto>
{
    Task<Result<ConfigTenantDto?>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result> ActualizarPepperVersionAsync(int idTenant, byte version, CancellationToken ct = default);
}

public class ConfigTenantService : ServiceAsync<ConfigTenant, ConfigTenantDto>, IConfigTenantService
{
    private readonly ConfigTenantRepository _repo;

    public ConfigTenantService(ConfigTenantRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<ConfigTenantDto?>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (entityResult.IsFailure) return Result<ConfigTenantDto?>.Failure(entityResult.Error!);
        var dto = entityResult.Value != null ? Mapper.Map<ConfigTenantDto>(entityResult.Value) : null;
        return Result<ConfigTenantDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result> ActualizarPepperVersionAsync(int idTenant, byte version, CancellationToken ct = default)
    {
        var repoResult = _repo.ActualizarPepperVersion(idTenant, version);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        return Result.Success();
    }

}
