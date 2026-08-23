using AutoMapper;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface IDominioTenantService : IServiceAsync<DominioTenant, DominioTenantDto>
{
    Task<Result<IReadOnlyList<DominioTenantDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<DominioTenantDto>> AgregarDominioAsync(CrearDominioTenantDto dto, CancellationToken ct = default);
    Task<Result<DominioTenantDto>> ActualizarDominioAsync(int id, string nuevoDominio, CancellationToken ct = default);
    Task<Result> EliminarDominioAsync(int id, CancellationToken ct = default);
    Task<Result<bool>> ExisteDominioAsync(int idTenant, string dominio, CancellationToken ct = default);
}

public class DominioTenantService : ServiceAsync<DominioTenant, DominioTenantDto>, IDominioTenantService
{
    private readonly DominioTenantRepository _repo;

    public DominioTenantService(DominioTenantRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<IReadOnlyList<DominioTenantDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var listResult = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (listResult.IsFailure)
            return Result<IReadOnlyList<DominioTenantDto>>.Failure(listResult.Error!);
        return Result<IReadOnlyList<DominioTenantDto>>.Success(Mapper.Map<IReadOnlyList<DominioTenantDto>>(listResult.Value));
    }

    public async Task<Result<DominioTenantDto>> AgregarDominioAsync(CrearDominioTenantDto dto, CancellationToken ct = default)
    {
        var entityResult = _repo.AgregarDominio(dto.IdTenant, dto.Dominio);
        if (entityResult.IsFailure)
            return Result<DominioTenantDto>.Failure(entityResult.Error!);
        return Result<DominioTenantDto>.Success(Mapper.Map<DominioTenantDto>(entityResult.Value));
    }

    public async Task<Result<bool>> ExisteDominioAsync(int idTenant, string dominio, CancellationToken ct = default)
    {
        var existeResult = await _repo.ExisteDominioAsync(idTenant, dominio, ct);
        if (existeResult.IsFailure)
            return Result<bool>.Failure(existeResult.Error!);
        return Result<bool>.Success(existeResult.Value);
    }

    public async Task<Result<DominioTenantDto>> ActualizarDominioAsync(int id, string nuevoDominio, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity.IsFailure) return Result<DominioTenantDto>.Failure(entity.Error!);
        var dominio = entity.Value;
        dominio.Dominio = nuevoDominio;
        var actResult = _repo.Actualizar(dominio);
        if (actResult.IsFailure) return Result<DominioTenantDto>.Failure(actResult.Error!);
        return Result<DominioTenantDto>.Success(Mapper.Map<DominioTenantDto>(dominio));
    }

    public async Task<Result> EliminarDominioAsync(int id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity.IsFailure) return Result.Failure(entity.Error!);
        var delResult = _repo.Eliminar(entity.Value);
        if (delResult.IsFailure) return Result.Failure(delResult.Error!);
        return Result.Success();
    }
}
