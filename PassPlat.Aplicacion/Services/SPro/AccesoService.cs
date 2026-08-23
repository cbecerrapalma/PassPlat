using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IAccesoService : IServiceAsync<Acceso, AccesoDto>
{
    Task<Result<bool>> TieneAccesoAsync(int idUsuario, int idApp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AccesoDto>>> ObtenerAccesosUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AccesoDto>>> ObtenerAccesosPorTenantYAppAsync(int idTenant, int idApp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AccesoDto>>> ObtenerAccesosPorRolAsync(int idRol, CancellationToken ct = default);
    Task<Result<AccesoDto>> AsignarAccesoAsync(AsignarAccesoDto dto, CancellationToken ct = default);
    Task<Result> RevocarAccesoAsync(int idUsuario, int idApp, CancellationToken ct = default);
}

public class AccesoService : ServiceAsync<Acceso, AccesoDto>, IAccesoService
{
    private readonly AccesoRepository _repo;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IRolRepository _rolRepo;
    private readonly IEmailQueue _emailQueue;
    private readonly ILogger<AccesoService> _logger;
    private readonly IUnitOfWorkAsync _uow;

    public AccesoService(AccesoRepository repo, IMapper mapper,
        IUsuarioRepository usuarioRepo, IRolRepository rolRepo,
        IEmailQueue emailQueue, ILogger<AccesoService> logger,
        IUnitOfWorkAsync uow)
        : base(repo, mapper)
    {
        _repo = repo;
        _usuarioRepo = usuarioRepo;
        _rolRepo = rolRepo;
        _emailQueue = emailQueue;
        _logger = logger;
        _uow = uow;
    }

    public async Task<Result<bool>> TieneAccesoAsync(int idUsuario, int idApp, CancellationToken ct = default)
    {
        var result = await _repo.TieneAccesoAsync(idUsuario, idApp, ct);
        if (result.IsFailure) return Result<bool>.Failure(result.Error!);
        return Result<bool>.Success(result.Value);
    }

    public async Task<Result<IReadOnlyList<AccesoDto>>> ObtenerAccesosUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        var accesosResult = await _repo.ObtenerAccesosUsuarioAsync(idUsuario, ct);
        if (accesosResult.IsFailure) return Result<IReadOnlyList<AccesoDto>>.Failure(accesosResult.Error!);
        return Result<IReadOnlyList<AccesoDto>>.Success(Mapper.Map<IReadOnlyList<AccesoDto>>(accesosResult.Value));
    }

    public async Task<Result<IReadOnlyList<AccesoDto>>> ObtenerAccesosPorTenantYAppAsync(int idTenant, int idApp, CancellationToken ct = default)
    {
        var accesosResult = await _repo.ObtenerAccesosPorTenantYAppAsync(idTenant, idApp, ct);
        if (accesosResult.IsFailure) return Result<IReadOnlyList<AccesoDto>>.Failure(accesosResult.Error!);
        return Result<IReadOnlyList<AccesoDto>>.Success(Mapper.Map<IReadOnlyList<AccesoDto>>(accesosResult.Value));
    }

    public async Task<Result<IReadOnlyList<AccesoDto>>> ObtenerAccesosPorRolAsync(int idRol, CancellationToken ct = default)
    {
        var accesosResult = await _repo.ObtenerAccesosPorRolAsync(idRol, ct);
        if (accesosResult.IsFailure) return Result<IReadOnlyList<AccesoDto>>.Failure(accesosResult.Error!);
        return Result<IReadOnlyList<AccesoDto>>.Success(Mapper.Map<IReadOnlyList<AccesoDto>>(accesosResult.Value));
    }

    public async Task<Result<AccesoDto>> AsignarAccesoAsync(AsignarAccesoDto dto, CancellationToken ct = default)
    {
        var accesoResult = await _repo.AsignarAccesoAsync(dto.IdUsuario, dto.IdTenant, dto.IdApp, dto.IdRol, ct);
        if (accesoResult.IsFailure) return Result<AccesoDto>.Failure(accesoResult.Error!);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (EsViolacionIndiceUnicoAcceso(ex))
        {
            return Result<AccesoDto>.Failure("ACCESO_DUPLICADO",
                "El usuario ya tiene un acceso activo para esta aplicación y rol");
        }

        await NotificarAccesoAsync(dto.IdUsuario, dto.IdRol, EmailJobKind.RoleAssigned, dto.IdTenant, dto.IdApp, ct);
        return Result<AccesoDto>.Success(Mapper.Map<AccesoDto>(accesoResult.Value));
    }

    public async Task<Result> RevocarAccesoAsync(int idUsuario, int idApp, CancellationToken ct = default)
    {
        var repoResult = _repo.RevocarAcceso(idUsuario, idApp);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        await NotificarAccesoAsync(idUsuario, 0, EmailJobKind.RoleRemoved, idTenant: null, idApp, ct);
        return Result.Success();
    }

    private static bool EsViolacionIndiceUnicoAcceso(DbUpdateException ex)
    {
        for (var e = ex.InnerException; e != null; e = e.InnerException)
        {
            if (e is Microsoft.Data.SqlClient.SqlException sqlex &&
                (sqlex.Number == 2601 || sqlex.Number == 2627) &&
                EsIndiceUnicoDeAcceso(sqlex))
                return true;
        }
        return false;
    }

    private static bool EsIndiceUnicoDeAcceso(Microsoft.Data.SqlClient.SqlException sqlex)
    {
        if (sqlex.Message.Contains("Accesos", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private async Task NotificarAccesoAsync(int idUsuario, int idRol, EmailJobKind kind, int? idTenant = null, int? idApp = null, CancellationToken ct = default)
    {
        try
        {
            var usuarioResult = await _usuarioRepo.ObtenerPorIdAsync(idUsuario, ct);
            if (usuarioResult.IsFailure || usuarioResult.Value == null) return;
            var usuario = usuarioResult.Value;
            if (string.IsNullOrWhiteSpace(usuario.Email)) return;

            var rolResult = await _rolRepo.GetByIdAsync(idRol, ct);
            var rolNombre = rolResult.IsSuccess ? rolResult.Value?.Nombre : $"Rol #{idRol}";
            var accion = kind == EmailJobKind.RoleAssigned ? "asignado" : "removido";

            await _emailQueue.EnqueueAsync(new EmailJob(
                kind,
                usuario.Email,
                usuario.NomUsuario,
                new Dictionary<string, object?> { ["RolNombre"] = rolNombre, ["Accion"] = accion },
                idTenant,
                usuario.Id,
                idApp,
                null), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar notificación de acceso {Kind} para usuario {IdUsuario}", kind, idUsuario);
        }
    }
}
