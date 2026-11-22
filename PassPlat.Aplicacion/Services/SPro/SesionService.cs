using CBP.Results;
using CBP.Results.Errors;
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
    Task<Result<IReadOnlyList<SesionDto>>> ObtenerSesionesVisiblesAsync(int idSolicitante, int idUsuario, int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SesionDto>>> ObtenerSesionesActivasTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<int>> RevocarTodasPorTenantAsync(int idTenant, CancellationToken ct = default);
}

public class SesionService : ServiceAsync<Sesion, SesionDto>, ISesionService
{
    private readonly SesionRepository _repo;
    private readonly IUsuarioTenantRepository _usuarioTenantRepo;
    private readonly CBP.Data.Abstractions.IUnitOfWorkAsync _uow;

    public SesionService(SesionRepository repo, AutoMapper.IMapper mapper, IUsuarioTenantRepository usuarioTenantRepo, CBP.Data.Abstractions.IUnitOfWorkAsync uow)
        : base(repo, mapper)
    {
        _repo = repo;
        _usuarioTenantRepo = usuarioTenantRepo;
        _uow = uow;
    }

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
        // S116 · D4 — el repositorio solo marca la entidad; este servicio es el dueño del commit (el controlador no guarda)
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<SesionDto>>> ObtenerSesionesActivasAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        var sesionesResult = await _repo.ObtenerSesionesActivasPorUsuarioAsync(idUsuario, idTenant, ct);
        if (sesionesResult.IsFailure) return Result<IReadOnlyList<SesionDto>>.Failure(sesionesResult.Error!);
        var dtos = Mapper.Map<IReadOnlyList<SesionDto>>(sesionesResult.Value);
        return Result<IReadOnlyList<SesionDto>>.Success(dtos);
    }

    // S69 · D1 — Visibilidad de sesiones para otro usuario del tenant
    public async Task<Result<IReadOnlyList<SesionDto>>> ObtenerSesionesVisiblesAsync(int idSolicitante, int idUsuario, int idTenant, CancellationToken ct = default)
    {
        if (idSolicitante != idUsuario)
        {
            var resMem = await _usuarioTenantRepo.ObtenerActivoPorTenantAsync(idUsuario, idTenant, ct);
            if (resMem.IsFailure) return Result<IReadOnlyList<SesionDto>>.Failure(resMem.Error!);
            if (resMem.Value is null) return Result<IReadOnlyList<SesionDto>>.Failure(CommonErrors.Authorization.Forbidden("sesiones del usuario"));
        }

        return await ObtenerSesionesActivasAsync(idUsuario, idTenant, ct);
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
}
