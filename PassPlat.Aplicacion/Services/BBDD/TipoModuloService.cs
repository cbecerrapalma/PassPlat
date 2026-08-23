using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface ITipoModuloService : IServiceAsync<TipoModulo, TipoModuloDto>
{
    Task<Result<TipoModuloDto?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
}

public class TipoModuloService : ServiceAsync<TipoModulo, TipoModuloDto>, ITipoModuloService
{
    private readonly TipoModuloRepository _repo;

    public TipoModuloService(TipoModuloRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<TipoModuloDto?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorCodigoAsync(codigo, ct);
        if (entityResult.IsFailure)
            return Result<TipoModuloDto?>.Failure(entityResult.Error!);
        var entity = entityResult.Value;
        return Result<TipoModuloDto?>.Success(Mapper.Map<TipoModuloDto?>(entity), allowNull: true);
    }
}
