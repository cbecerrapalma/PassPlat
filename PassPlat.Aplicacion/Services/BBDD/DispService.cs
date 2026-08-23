using AutoMapper;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Contexto;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Contexto;

namespace PassPlat.Aplicacion.Services;

public interface IDispService : IServiceAsync<Disp, DispDto>
{
    Task<Result<DispDto?>> ObtenerPorIdAsync(int idDisp, CancellationToken ct = default);
    Task<Result<DispDto>> ObtenerOCrearAsync(int idTipoDisp, string? fabricante = null, string? modelo = null, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DispDto>>> ObtenerTodosConTipoAsync(CancellationToken ct = default);
    Task<Result<DispDto?>> ObtenerConDetallesAsync(int idDisp, CancellationToken ct = default);
}

public class DispService : ServiceAsync<Disp, DispDto>, IDispService
{
    private readonly DispRepository _repo;

    public DispService(DispRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<DispDto?>> ObtenerPorIdAsync(int idDisp, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorIdAsync(idDisp, ct);
        if (entityResult.IsFailure)
            return Result<DispDto?>.Failure(entityResult.Error!);
        var entity = entityResult.Value;
        return Result<DispDto?>.Success(Mapper.Map<DispDto?>(entity), allowNull: true);
    }

    public async Task<Result<DispDto>> ObtenerOCrearAsync(int idTipoDisp, string? fabricante = null, string? modelo = null, CancellationToken ct = default)
    {
        var repoResult = _repo.ObtenerOCrear(idTipoDisp, fabricante, modelo);
        if (repoResult.IsFailure) return Result<DispDto>.Failure(repoResult.Error!);
        return Result<DispDto>.Success(Mapper.Map<DispDto>(repoResult.Value));
    }

    public async Task<Result<IReadOnlyList<DispDto>>> ObtenerTodosConTipoAsync(CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerTodosConTipoAsync(ct);
        if (repoResult.IsFailure) return Result<IReadOnlyList<DispDto>>.Failure(repoResult.Error!);
        return Result<IReadOnlyList<DispDto>>.Success(Mapper.Map<IReadOnlyList<DispDto>>(repoResult.Value));
    }

    public async Task<Result<DispDto?>> ObtenerConDetallesAsync(int idDisp, CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerConDetallesAsync(idDisp, ct);
        if (repoResult.IsFailure) return Result<DispDto?>.Failure(repoResult.Error!);
        return Result<DispDto?>.Success(Mapper.Map<DispDto?>(repoResult.Value), allowNull: true);
    }
}
