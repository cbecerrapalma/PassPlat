using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;
using PassPlat.Datos.SPResults;

namespace PassPlat.Aplicacion.Services;

public interface ISesionService : IServiceAsync<Sesion, SesionDto>
{
    Task<Result<CrearSesionResult>> CrearSesionAsync(int idUsuario, int idTenant, int idApp, string idTokenExt, DateTime fecExpira, string? hashRefresh, int? idDisp, int? idIP, Guid? idSesionPadre, CancellationToken ct = default);
    Task<Result<RevocarSesionesResult>> RevocarTodasAsync(int idUsuario, int idTenant, Guid? idSesionExcluir, CancellationToken ct = default);
    Task<Result> RevocarSesionAsync(Guid idSesion, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SesionDto>>> ObtenerSesionesActivasAsync(int idUsuario, int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SesionDto>>> ObtenerSesionesActivasTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<int>> RevocarTodasPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<int>> ContarSesionesActivasAsync(int idUsuario, int idTenant, CancellationToken ct = default);
    Task<Result<int>> ContarSesionesActivasPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<SesionDto?>> ObtenerPorIdTokenExtAsync(string idTokenExt, CancellationToken ct = default);
    Task<Result<SesionDto?>> ObtenerPorHashRefreshAsync(string hashRefresh, CancellationToken ct = default);
    Task<Result<bool>> IntentarRotarHashRefreshAsync(Guid idSesion, string? hashRefreshEsperado, string? nuevoHashRefresh, DateTime nuevaFecExpira, CancellationToken ct = default);
}

public class SesionService : ServiceAsync<Sesion, SesionDto>, ISesionService
{
    private readonly SesionRepository _repo;

    public SesionService(SesionRepository repo, AutoMapper.IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<CrearSesionResult>> CrearSesionAsync(int idUsuario, int idTenant, int idApp, string idTokenExt, DateTime fecExpira, string? hashRefresh, int? idDisp, int? idIP, Guid? idSesionPadre, CancellationToken ct = default)
    {
        var result = await _repo.CrearSesionAsync(idUsuario, idTenant, idApp, idTokenExt, fecExpira, hashRefresh, idDisp, idIP, idSesionPadre, ct);
        return result;
    }

    public async Task<Result<RevocarSesionesResult>> RevocarTodasAsync(int idUsuario, int idTenant, Guid? idSesionExcluir, CancellationToken ct = default)
    {
        var result = await _repo.RevocarTodasAsync(idUsuario, idTenant, idSesionExcluir, ct);
        return result;
    }

    public async Task<Result> RevocarSesionAsync(Guid idSesion, CancellationToken ct = default)
    {
        var repoResult = await _repo.RevocarSesionAsync(idSesion, ct);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<SesionDto>>> ObtenerSesionesActivasAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        var sesionesResult = await _repo.ObtenerSesionesActivasPorUsuarioAsync(idUsuario, idTenant, ct);
        if (sesionesResult.IsFailure) return Result<IReadOnlyList<SesionDto>>.Failure(sesionesResult.Error!);
        var dtos = Mapper.Map<IReadOnlyList<SesionDto>>(sesionesResult.Value);
        return Result<IReadOnlyList<SesionDto>>.Success(dtos);
    }

    // ETAPA 5: sesiones activas de todo el tenant para administración
    public async Task<Result<IReadOnlyList<SesionDto>>> ObtenerSesionesActivasTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var sesionesResult = await _repo.ObtenerSesionesActivasPorTenantAsync(idTenant, ct);
        if (sesionesResult.IsFailure) return Result<IReadOnlyList<SesionDto>>.Failure(sesionesResult.Error!);
        var dtos = Mapper.Map<IReadOnlyList<SesionDto>>(sesionesResult.Value);
        foreach (var (s, d) in sesionesResult.Value.Zip(dtos, (s, d) => (s, d)))
        {
            d.Navegador = s.Disp?.Navegador;
            d.SO = s.Disp?.SO;
            d.Pais = s.Disp?.Pais;
            d.ProveedorAuth = s.Disp?.ProveedorAuth;
        }
        return Result<IReadOnlyList<SesionDto>>.Success(dtos);
    }

    // ETAPA 5: revocar todas las sesiones del tenant (administración)
    public async Task<Result<int>> RevocarTodasPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.RevocarTodasPorTenantAsync(idTenant, ct);
        return result.IsFailure ? Result<int>.Failure(result.Error!) : Result<int>.Success(result.Value);
    }

    public async Task<Result<int>> ContarSesionesActivasAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        var countResult = await _repo.ContarSesionesActivasAsync(idUsuario, idTenant, ct);
        if (countResult.IsFailure) return Result<int>.Failure(countResult.Error!);
        return Result<int>.Success(countResult.Value);
    }

    public async Task<Result<int>> ContarSesionesActivasPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var countResult = await _repo.ContarSesionesActivasPorTenantAsync(idTenant, ct);
        if (countResult.IsFailure) return Result<int>.Failure(countResult.Error!);
        return Result<int>.Success(countResult.Value);
    }

    public async Task<Result<SesionDto?>> ObtenerPorIdTokenExtAsync(string idTokenExt, CancellationToken ct = default)
    {
        var sesionResult = await _repo.ObtenerPorIdTokenExtAsync(idTokenExt, ct);
        if (sesionResult.IsFailure) return Result<SesionDto?>.Failure(sesionResult.Error!);
        var dto = sesionResult.Value != null ? Mapper.Map<SesionDto>(sesionResult.Value) : null;
        return Result<SesionDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<SesionDto?>> ObtenerPorHashRefreshAsync(string hashRefresh, CancellationToken ct = default)
    {
        var sesionResult = await _repo.ObtenerPorHashRefreshAsync(hashRefresh, ct);
        if (sesionResult.IsFailure) return Result<SesionDto?>.Failure(sesionResult.Error!);
        var dto = sesionResult.Value != null ? Mapper.Map<SesionDto>(sesionResult.Value) : null;
        return Result<SesionDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<bool>> IntentarRotarHashRefreshAsync(Guid idSesion, string? hashRefreshEsperado, string? nuevoHashRefresh, DateTime nuevaFecExpira, CancellationToken ct = default)
    {
        return await _repo.IntentarRotarHashRefreshAsync(idSesion, hashRefreshEsperado, nuevoHashRefresh, nuevaFecExpira, ct);
    }
}
