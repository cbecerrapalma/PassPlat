using CBP.Results;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Enums;

namespace PassPlat.Aplicacion.Services.Dashboard;

public interface IInicioService
{
    Task<Result<InicioDto>> ObtenerAsync(int idUsuario, int? idTenant, IReadOnlySet<string> permisos, CancellationToken ct = default);
}

/// <summary>
/// S77 · D3 — inicio por perfil: cuenta (siempre), tenant (con tenant) y plataforma (sin tenant).
/// Todos los conteos se resuelven en la BD con CountAsync; no se descargan listas.
/// Un campo null en el DTO significa que el actor no tiene el permiso correspondiente.
/// </summary>
public class InicioService : IInicioService
{
    // Mismos umbrales que los endpoints existentes: UsuariosController.cs:110 (5) y :114 (90).
    private const byte MaxIntentosFallidos = 5;
    private const int DiasVigenciaPwd = 90;
    private const int EventosActividad = 6;

    private readonly IUsuarioRepository _usuarioRepo;
    private readonly ISesionRepository _sesionRepo;
    private readonly IDispConfiableRepository _dispConfRepo;
    private readonly IMFARepository _mfaRepo;
    private readonly IHistorialPwdRepository _histPwdRepo;
    private readonly ITenantRepository _tenantRepo;
    private readonly IAppRepository _appRepo;
    private readonly IAuditoriaPwdService _auditoriaPwd;
    private readonly IBackgroundStatusService _bgStatus;

    public InicioService(
        IUsuarioRepository usuarioRepo, ISesionRepository sesionRepo,
        IDispConfiableRepository dispConfRepo, IMFARepository mfaRepo,
        IHistorialPwdRepository histPwdRepo, ITenantRepository tenantRepo,
        IAppRepository appRepo, IAuditoriaPwdService auditoriaPwd,
        IBackgroundStatusService bgStatus)
    {
        _usuarioRepo = usuarioRepo;
        _sesionRepo = sesionRepo;
        _dispConfRepo = dispConfRepo;
        _mfaRepo = mfaRepo;
        _histPwdRepo = histPwdRepo;
        _tenantRepo = tenantRepo;
        _appRepo = appRepo;
        _auditoriaPwd = auditoriaPwd;
        _bgStatus = bgStatus;
    }

    // Las llamadas van una tras otra: comparten el mismo DbContext, nada de Task.WhenAll.
    public async Task<Result<InicioDto>> ObtenerAsync(int idUsuario, int? idTenant, IReadOnlySet<string> permisos, CancellationToken ct = default)
    {
        var dto = new InicioDto();

        // ── Mi cuenta: siempre, sin permiso adicional ────────────────────────
        // S77 · D3 — CBP FirstOrDefaultAsync devuelve DATABASE_ERROR cuando no hay fila
        // (crea Success(null)); WhereAsync + FirstOrDefault deja FecExpiraPwd en null.
        var pwdResult = await _histPwdRepo.WhereAsync(h => h.IdUsuario == idUsuario && h.EsActual, ct: ct);
        if (pwdResult.IsFailure) return Result<InicioDto>.Failure(pwdResult.Error!);
        dto.Cuenta.FecExpiraPwd = pwdResult.Value.FirstOrDefault()?.FecExpira;

        var mfaResult = await _mfaRepo.CountAsync(m => m.IdUsuario == idUsuario && m.IdEstado == (int)EEstadoMFA.Activo, ct);
        if (mfaResult.IsFailure) return Result<InicioDto>.Failure(mfaResult.Error!);
        dto.Cuenta.MetodosMfaActivos = mfaResult.Value;

        if (idTenant is int tenantCuenta)
        {
            var sesionesCuenta = await _sesionRepo.ContarSesionesActivasAsync(idUsuario, tenantCuenta, ct);
            if (sesionesCuenta.IsFailure) return Result<InicioDto>.Failure(sesionesCuenta.Error!);
            dto.Cuenta.SesionesActivas = sesionesCuenta.Value;
        }
        else
        {
            var sesionesCuenta = await _sesionRepo.CountAsync(s => s.IdUsuario == idUsuario && s.EsActiva && s.FecExpira > DateTime.Now, ct);
            if (sesionesCuenta.IsFailure) return Result<InicioDto>.Failure(sesionesCuenta.Error!);
            dto.Cuenta.SesionesActivas = sesionesCuenta.Value;
        }

        var dispCuenta = await _dispConfRepo.CountAsync(
            d => d.IdUsuario == idUsuario && d.Confiable && (idTenant == null || d.IdTenant == idTenant), ct);
        if (dispCuenta.IsFailure) return Result<InicioDto>.Failure(dispCuenta.Error!);
        dto.Cuenta.DispositivosConfiables = dispCuenta.Value;

        // ── Mi tenant: solo con tenant en el token ──────────────────────────
        if (idTenant is int idTenantValor)
        {
            var tenant = new InicioTenantDto();

            if (permisos.Contains("USUARIOS_VER"))
            {
                var usuarios = await _usuarioRepo.CountAsync(u => u.IdTenant == idTenantValor, ct);
                if (usuarios.IsFailure) return Result<InicioDto>.Failure(usuarios.Error!);
                tenant.Usuarios = usuarios.Value;

                var intentos = await _usuarioRepo.CountAsync(
                    u => u.IdTenant == idTenantValor && u.IntentosFallidos >= MaxIntentosFallidos && !u.Eliminado, ct);
                if (intentos.IsFailure) return Result<InicioDto>.Failure(intentos.Error!);
                tenant.ConIntentosExcedidos = intentos.Value;

                var fechaLimite = DateTime.Now.AddDays(-DiasVigenciaPwd);
                var expiradas = await _usuarioRepo.CountAsync(
                    u => u.IdTenant == idTenantValor && !u.Eliminado
                        && u.FecUltCambioPwd.HasValue && u.FecUltCambioPwd.Value <= fechaLimite, ct);
                if (expiradas.IsFailure) return Result<InicioDto>.Failure(expiradas.Error!);
                tenant.ConPasswordExpirada = expiradas.Value;
            }

            if (permisos.Contains("SESIONES_VER"))
            {
                var sesiones = await _sesionRepo.ContarSesionesActivasPorTenantAsync(idTenantValor, ct);
                if (sesiones.IsFailure) return Result<InicioDto>.Failure(sesiones.Error!);
                tenant.SesionesActivas = sesiones.Value;
            }

            if (permisos.Contains("USUARIOS_VERDISP"))
            {
                var confiables = await _dispConfRepo.CountAsync(d => d.IdTenant == idTenantValor && d.Confiable, ct);
                if (confiables.IsFailure) return Result<InicioDto>.Failure(confiables.Error!);
                tenant.DispConfiables = confiables.Value;

                var noConfiables = await _dispConfRepo.CountAsync(d => d.IdTenant == idTenantValor && !d.Confiable, ct);
                if (noConfiables.IsFailure) return Result<InicioDto>.Failure(noConfiables.Error!);
                tenant.DispNoConfiables = noConfiables.Value;
            }

            if (permisos.Contains("AUDITORIA_VER"))
            {
                // Corrige D-1: la ruta del controlador espera una cantidad, no el tenant.
                var actividad = await _auditoriaPwd.ObtenerPorTenantAsync(idTenantValor, EventosActividad, ct);
                if (actividad.IsFailure) return Result<InicioDto>.Failure(actividad.Error!);
                tenant.Actividad = actividad.Value;
            }

            dto.Tenant = tenant;
        }
        else
        {
            // ── Plataforma: sin tenant en el token, totales globales ────────
            var plataforma = new InicioPlataformaDto();

            if (permisos.Contains("TENANTS_VER"))
            {
                var tenants = await _tenantRepo.CountAsync(x => x.Activo, ct);
                if (tenants.IsFailure) return Result<InicioDto>.Failure(tenants.Error!);
                plataforma.TenantsActivos = tenants.Value;
            }

            if (permisos.Contains("APPS_VER"))
            {
                var apps = await _appRepo.CountAsync(ct);
                if (apps.IsFailure) return Result<InicioDto>.Failure(apps.Error!);
                plataforma.Apps = apps.Value;
            }

            if (permisos.Contains("USUARIOS_VER"))
            {
                var usuarios = await _usuarioRepo.CountAsync(ct);
                if (usuarios.IsFailure) return Result<InicioDto>.Failure(usuarios.Error!);
                plataforma.Usuarios = usuarios.Value;

                var servicios = await _bgStatus.GetBackgroundJobsAsync(ct);
                if (servicios.IsFailure) return Result<InicioDto>.Failure(servicios.Error!);
                plataforma.Servicios = servicios.Value;
            }

            dto.Plataforma = plataforma;
        }

        return Result<InicioDto>.Success(dto);
    }
}
