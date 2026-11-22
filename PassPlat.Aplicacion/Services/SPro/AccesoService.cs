using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Results.Errors;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using Microsoft.AspNetCore.Http;
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
    Task<Result<IReadOnlyList<AccesoDto>>> ObtenerAccesosUsuarioAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default);
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
    private readonly IUsuarioTenantRepository _usuarioTenantRepo;
    private readonly IAppRepository _appRepo;
    private readonly IEmailQueue _emailQueue;
    private readonly ILogger<AccesoService> _logger;
    private readonly IUnitOfWorkAsync _uow;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccesoService(AccesoRepository repo, IMapper mapper,
        IUsuarioRepository usuarioRepo, IRolRepository rolRepo,
        IUsuarioTenantRepository usuarioTenantRepo,
        IAppRepository appRepo,
        IEmailQueue emailQueue, ILogger<AccesoService> logger,
        IUnitOfWorkAsync uow, IHttpContextAccessor httpContextAccessor)
        : base(repo, mapper)
    {
        _repo = repo;
        _usuarioRepo = usuarioRepo;
        _rolRepo = rolRepo;
        _usuarioTenantRepo = usuarioTenantRepo;
        _appRepo = appRepo;
        _emailQueue = emailQueue;
        _logger = logger;
        _uow = uow;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<bool>> TieneAccesoAsync(int idUsuario, int idApp, CancellationToken ct = default)
    {
        var result = await _repo.TieneAccesoAsync(idUsuario, idApp, ct);
        if (result.IsFailure) return Result<bool>.Failure(result.Error!);
        return Result<bool>.Success(result.Value);
    }

    // S105 · D4 — alcanceTenant null = sin acotar (sistema o el propio usuario); si no, solo los Accesos de ese tenant
    public async Task<Result<IReadOnlyList<AccesoDto>>> ObtenerAccesosUsuarioAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default)
    {
        var accesosResult = await _repo.ObtenerAccesosUsuarioAsync(idUsuario, ct);
        if (accesosResult.IsFailure) return Result<IReadOnlyList<AccesoDto>>.Failure(accesosResult.Error!);
        var accesos = accesosResult.Value!.Where(a => alcanceTenant is null || a.IdTenant == alcanceTenant).ToList();
        return Result<IReadOnlyList<AccesoDto>>.Success(Mapper.Map<IReadOnlyList<AccesoDto>>(accesos));
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
        var autoedicion = AutoEdicion(dto.IdUsuario);
        if (autoedicion.IsFailure) return Result<AccesoDto>.Failure(autoedicion.Error!);

        // S61 D2: rol existente/activo y del tenant de la sesión.
        var rolResult = await _rolRepo.GetByIdAsync(dto.IdRol, ct);
        if (rolResult.IsFailure) return Result<AccesoDto>.Failure(rolResult.Error!);
        var rol = rolResult.Value;
        if (rol is null || !rol.Activo)
            return Result<AccesoDto>.Failure(new Error("ROL_NO_ENCONTRADO",
                "El rol no existe o no está activo", null, 400, ErrorType.Validation));
        if (rol.IdTenant is not null && rol.IdTenant != dto.IdTenant)
            return Result<AccesoDto>.Failure(new Error("ROL_OTRO_TENANT",
                "El rol seleccionado pertenece a otro tenant", null, 400, ErrorType.Validation));

        // S61 D2: sin membresía activa en el tenant no se escribe nada.
        var membresiaResult = await _usuarioTenantRepo.ObtenerActivoPorTenantAsync(dto.IdUsuario, dto.IdTenant, ct);
        if (membresiaResult.IsFailure) return Result<AccesoDto>.Failure(membresiaResult.Error!);
        var membresia = membresiaResult.Value;
        if (membresia is null)
            return Result<AccesoDto>.Failure(new Error("SIN_MEMBRESIA_TENANT",
                "El usuario no pertenece a este tenant", null, 400, ErrorType.Validation));

        // S61 D1/D2: plataforma solo en el tenant EsSistema, con rol global y asignador is_system.
        var esPlataforma = membresia.Tenant?.EsSistema == true
                           && rol.IdTenant is null
                           && _httpContextAccessor.HttpContext?.User.HasClaim("is_system", "true") == true;
        var idUsuarioTenant = esPlataforma ? (int?)null : membresia.Id;

        var accesoResult = await _repo.AsignarAccesoAsync(
            dto.IdUsuario, dto.IdTenant, dto.IdApp, dto.IdRol, idUsuarioTenant, ct);
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

    // S107 · D3 — nadie se asigna ni se revoca accesos a sí mismo, salvo el usuario de sistema
    private Result AutoEdicion(int idUsuario)
    {
        var usuario = _httpContextAccessor.HttpContext?.User;
        if (usuario is null || usuario.HasClaim("is_system", "true")) return Result.Success();
        return int.TryParse(usuario.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) && id == idUsuario
            ? Result.Failure(new Error("AUTOEDICION", "No puede cambiar sus propios accesos", null, 400, ErrorType.Validation))
            : Result.Success();
    }

    public async Task<Result> RevocarAccesoAsync(int idUsuario, int idApp, CancellationToken ct = default)
    {
        var autoedicion = AutoEdicion(idUsuario);
        if (autoedicion.IsFailure) return autoedicion;

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
            // S59 · D12 — las plantillas de rol usan {{AppName}}; si no se puede leer la app,
            // se manda "PassPlat" para no cortar el envio.
            var appNombre = await ObtenerNombreAppAsync(idApp, ct);

            await _emailQueue.EnqueueAsync(new EmailJob(
                kind,
                usuario.Email,
                usuario.NomUsuario,
                new Dictionary<string, object?> { ["RolNombre"] = rolNombre, ["Accion"] = accion, ["AppName"] = appNombre },
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

    /// <summary>
    /// S59 · D12 — nombre de la app para las plantillas de rol; "PassPlat" si no hay
    /// idApp, si no existe o si falla la lectura (el envio nunca se corta por esto).
    /// </summary>
    private async Task<string> ObtenerNombreAppAsync(int? idApp, CancellationToken ct)
    {
        if (idApp is null or <= 0) return "PassPlat";
        try
        {
            var result = await _appRepo.GetByIdAsync(idApp.Value, ct);
            var nombre = result.IsSuccess ? result.Value?.Nombre : null;
            return string.IsNullOrWhiteSpace(nombre) ? "PassPlat" : nombre;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer el nombre de la app {IdApp} para la notificacion", idApp);
            return "PassPlat";
        }
    }
}
