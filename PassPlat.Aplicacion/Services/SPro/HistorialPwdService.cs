using AutoMapper;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IHistorialPwdService : IServiceAsync<HistorialPwd, HistorialPwdDto>
{
    Task<Result<IReadOnlyList<HistorialPwdDto>>> ObtenerHistorialRecienteAsync(int idUsuario, int cantidad, CancellationToken ct = default);
    Task<Result> MarcarComprometidasPorHashAsync(string hashPwd, CancellationToken ct = default);
    Task<Result<IReadOnlyList<HistorialPwdDto>>> ObtenerPasswordsComprometidasAsync(CancellationToken ct = default);
    Task<Result<(IReadOnlyList<HistorialPwdDto> Items, int TotalCount)>> ObtenerPaginadoPorTenantAsync(int idTenant, int pageNumber, int pageSize, CancellationToken ct = default);
}

public class HistorialPwdService : ServiceAsync<HistorialPwd, HistorialPwdDto>, IHistorialPwdService
{
    private readonly HistorialPwdRepository _repo;

    public HistorialPwdService(HistorialPwdRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<IReadOnlyList<HistorialPwdDto>>> ObtenerHistorialRecienteAsync(int idUsuario, int cantidad, CancellationToken ct = default)
    {
        var historialResult = await _repo.ObtenerHistorialRecienteAsync(idUsuario, cantidad, ct);
        if (historialResult.IsFailure) return Result<IReadOnlyList<HistorialPwdDto>>.Failure(historialResult.Error!);
        var historial = historialResult.Value;
        return Result<IReadOnlyList<HistorialPwdDto>>.Success(Mapper.Map<IReadOnlyList<HistorialPwdDto>>(historial));
    }

    public async Task<Result> MarcarComprometidasPorHashAsync(string hashPwd, CancellationToken ct = default)
    {
        var repoResult = await _repo.MarcarComprometidasPorHashAsync(hashPwd, ct);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        return Result.Success();
    }

    public async Task<Result<(IReadOnlyList<HistorialPwdDto> Items, int TotalCount)>> ObtenerPaginadoPorTenantAsync(int idTenant, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerPaginadoPorTenantAsync(idTenant, pageNumber, pageSize, ct);
        if (repoResult.IsFailure) return Result<(IReadOnlyList<HistorialPwdDto>, int)>.Failure(repoResult.Error!);
        var items = Mapper.Map<IReadOnlyList<HistorialPwdDto>>(repoResult.Value.Items);
        return Result<(IReadOnlyList<HistorialPwdDto>, int)>.Success((items, repoResult.Value.TotalCount));
    }

    public async Task<Result<IReadOnlyList<HistorialPwdDto>>> ObtenerPasswordsComprometidasAsync(CancellationToken ct = default)
    {
        var passwordsResult = await _repo.ObtenerPasswordsComprometidasAsync(ct);
        if (passwordsResult.IsFailure) return Result<IReadOnlyList<HistorialPwdDto>>.Failure(passwordsResult.Error!);
        var passwords = passwordsResult.Value;
        return Result<IReadOnlyList<HistorialPwdDto>>.Success(Mapper.Map<IReadOnlyList<HistorialPwdDto>>(passwords));
    }
}
