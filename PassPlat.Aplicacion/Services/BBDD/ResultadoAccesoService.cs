using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface IResultadoAccesoService : IServiceAsync<ResultadoAcceso, ResultadoAccesoDto>
{
    Task<Result<IReadOnlyList<ResultadoAccesoDto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<ResultadoAccesoDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
}

public class ResultadoAccesoService : ServiceAsync<ResultadoAcceso, ResultadoAccesoDto>, IResultadoAccesoService
{
    public ResultadoAccesoService(ResultadoAccesoRepository repo, IMapper mapper)
        : base(repo, mapper) { }

    public async Task<Result<IReadOnlyList<ResultadoAccesoDto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await GetAllAsync(ct: ct);
    }

    public async Task<Result<ResultadoAccesoDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<ResultadoAccesoDto?>.Failure(result.Error!);
        return Result<ResultadoAccesoDto?>.Success(Mapper.Map<ResultadoAccesoDto>(result.Value));
    }
}
