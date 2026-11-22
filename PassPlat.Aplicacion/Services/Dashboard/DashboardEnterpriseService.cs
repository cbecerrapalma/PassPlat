using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using CBP.Results.Errors;
using System.Diagnostics;
using System.Linq.Expressions;
using CBP.Caching.Interfaces;
using CBP.Caching.Models;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Enums;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services.Dashboard;

public interface IDashboardEnterpriseService
{
    Task<Result<DashboardEjecutivoDto>> GetEjecutivoAsync(int? idTenant = null, CancellationToken ct = default);
    Task<Result<DashboardSeguridadDto>> GetSeguridadAsync(int? idTenant = null, CancellationToken ct = default);
    Task<Result<DashboardOAuthDto>> GetOAuthAsync(int? idTenant = null, CancellationToken ct = default);
    Task<Result<DashboardEmailDto>> GetEmailAsync(int? idTenant = null, CancellationToken ct = default);
    Task<Result<DashboardOperacionalDto>> GetOperacionalAsync(int? idTenant = null, CancellationToken ct = default);
    Task<Result<DashboardAuditoriaDto>> GetAuditoriaAsync(int? idTenant = null, CancellationToken ct = default);
    Task<Result<DashboardDispositivosDto>> GetDispositivosAsync(int? idTenant = null, CancellationToken ct = default);
    Task<Result<DashboardTendenciasDto>> GetTendenciasAsync(int? idTenant = null, CancellationToken ct = default);
    Task<Result<DashboardEstadoGeneralDto>> GetEstadoGeneralAsync(int? idTenant = null, CancellationToken ct = default);
    Task<Result<DashboardEjecutivoAvanzadoDto>> GetEjecutivoAvanzadoAsync(int? idTenant = null, CancellationToken ct = default);
    Task<Result<List<BackgroundJobDto>>> GetBackgroundJobsAsync(CancellationToken ct = default);
}

public class DashboardEnterpriseService : IDashboardEnterpriseService
{
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly ITenantRepository _tenantRepo;
    private readonly ISesionRepository _sesionRepo;
    private readonly IDispConfiableRepository _dispConfRepo;
    private readonly IIdenExtRepository _idenExtRepo;
    private readonly IEmailLogRepository _emailLogRepo;
    private readonly IIntentoAccesoRepository _intentoRepo;
    private readonly IBloqueoRepository _bloqueoRepo;
    private readonly IMFARepository _mfaRepo;
    private readonly IHistorialPwdRepository _histPwdRepo;
    private readonly IAuditoriaPwdRepository _audRepo;
    private readonly IProvIdenRepository _provIdenRepo;
    private readonly IAudIdenExtRepository _audIdenExtRepo;
    private readonly IDispRepository _dispRepo;
    private readonly IIPRepository _ipRepo;
    private readonly IEmailTemplateRepository _templateRepo;
    private readonly IHistorialIdenExtRepository _histIdenRepo;
    private readonly IAppRepository _appRepo;
    private readonly IRolRepository _rolRepo;
    private readonly IPermisoRepository _permisoRepo;
    private readonly INotificacionRepository _notifRepo;
    private readonly IIdenExtTokensRepository _idenExtTokensRepo;
    private readonly IUsuarioTenantRepository _usuarioTenantRepo;
    private readonly IBackgroundStatusService _bgStatus;
    private readonly ICacheService _cache;

    public DashboardEnterpriseService(
        IUsuarioRepository usuarioRepo, ITenantRepository tenantRepo,
        ISesionRepository sesionRepo, IDispConfiableRepository dispConfRepo,
        IIdenExtRepository idenExtRepo, IEmailLogRepository emailLogRepo,
        IIntentoAccesoRepository intentoRepo, IBloqueoRepository bloqueoRepo,
        IMFARepository mfaRepo, IHistorialPwdRepository histPwdRepo,
        IAuditoriaPwdRepository audRepo, IProvIdenRepository provIdenRepo,
        IAudIdenExtRepository audIdenExtRepo, IDispRepository dispRepo,
        IIPRepository ipRepo, IEmailTemplateRepository templateRepo,
        IHistorialIdenExtRepository histIdenRepo, IAppRepository appRepo,
        IRolRepository rolRepo, IPermisoRepository permisoRepo,
         INotificacionRepository notifRepo, IIdenExtTokensRepository idenExtTokensRepo, IUsuarioTenantRepository usuarioTenantRepo, IBackgroundStatusService bgStatus, ICacheService cache)
    {
        _cache = cache; _usuarioRepo = usuarioRepo; _tenantRepo = tenantRepo;
        _sesionRepo = sesionRepo; _dispConfRepo = dispConfRepo;
        _idenExtRepo = idenExtRepo; _emailLogRepo = emailLogRepo;
        _intentoRepo = intentoRepo; _bloqueoRepo = bloqueoRepo;
        _mfaRepo = mfaRepo; _histPwdRepo = histPwdRepo;
        _audRepo = audRepo; _provIdenRepo = provIdenRepo;
        _audIdenExtRepo = audIdenExtRepo; _dispRepo = dispRepo;
        _ipRepo = ipRepo; _templateRepo = templateRepo;
        _histIdenRepo = histIdenRepo; _appRepo = appRepo;
        _rolRepo = rolRepo; _permisoRepo = permisoRepo;
        _notifRepo = notifRepo; _idenExtTokensRepo = idenExtTokensRepo; _usuarioTenantRepo = usuarioTenantRepo; _bgStatus = bgStatus;
    }

    public async Task<Result<DashboardEjecutivoDto>> GetEjecutivoAsync(int? idTenant = null, CancellationToken ct = default)
    {
        return await CachedAsync(CacheKey("Ejecutivo", idTenant), async () =>
        {
            try
            {
                var idsR = await IdsTenantAsync(idTenant, ct);
                if (idsR.IsFailure) return Result<DashboardEjecutivoDto>.Failure(idsR.Error!);
                var ids = idsR.Value;
                Expression<Func<Usuario, bool>>? fU = ids is null ? null : u => ids.Contains(u.Id);
                Expression<Func<IdenExt, bool>>? fI = ids is null ? null : i => ids.Contains(i.IdUsuario);
                Expression<Func<EmailLog, bool>>? fE = ids is null ? null : e => e.IdUsuario.HasValue && ids.Contains(e.IdUsuario.Value);
                Expression<Func<Sesion, bool>>? fS = ids is null ? null : s => ids.Contains(s.IdUsuario);
                Expression<Func<DispConfiable, bool>>? fD = ids is null ? null : d => ids.Contains(d.IdUsuario);
                Expression<Func<Notificacion, bool>>? fN = ids is null ? null : n => ids.Contains(n.IdUsuario);
                var hoy = DateTime.Today;

                var idenUsuarios = await _idenExtRepo.CountByAsync(i => i.IdUsuario, Y<IdenExt>(i => !i.Eliminado, fI), ct: ct);
                if (idenUsuarios.IsFailure) return Result<DashboardEjecutivoDto>.Failure(idenUsuarios.Error!);
                var idenUserIds = Claves(idenUsuarios.Value);

                var totalUsuarios = await _usuarioRepo.CountAsync(Y<Usuario>(u => true, fU), ct);
                var activos = await _usuarioRepo.CountAsync(Y<Usuario>(u => u.IdEstado == 1 && !u.Eliminado, fU), ct);
                var bloqueados = await _usuarioRepo.CountAsync(Y<Usuario>(u => u.IdEstado == 3, fU), ct);
                var eliminados = await _usuarioRepo.CountAsync(Y<Usuario>(u => u.Eliminado, fU), ct);
                var locales = await _usuarioRepo.CountAsync(Y<Usuario>(u => u.TienePasswordLocal && !u.Eliminado && !idenUserIds.Contains(u.Id), fU), ct);
                var externos = await _usuarioRepo.CountAsync(Y<Usuario>(u => !u.TienePasswordLocal && !u.Eliminado && idenUserIds.Contains(u.Id), fU), ct);
                var mixtos = await _usuarioRepo.CountAsync(Y<Usuario>(u => u.TienePasswordLocal && !u.Eliminado && idenUserIds.Contains(u.Id), fU), ct);
                var tenants = idTenant.HasValue ? Result<int>.Success(1) : await _tenantRepo.CountAsync(t => t.Activo, ct);
                var apps = await _appRepo.CountAsync(ct);
                var roles = idTenant.HasValue && idTenant.Value > 0
                    ? await _rolRepo.CountAsync(RolesDelTenant(idTenant.Value), ct)
                    : await _rolRepo.CountAsync(ct);
                var permisos = await _permisoRepo.CountAsync(ct);
                var sesiones = await _sesionRepo.CountAsync(Y<Sesion>(s => s.EsActiva, fS), ct);
                var dispConf = await _dispConfRepo.CountAsync(Y<DispConfiable>(d => d.Confiable, fD), ct);
                var identidades = await _idenExtRepo.CountAsync(Y<IdenExt>(i => !i.Eliminado, fI), ct);
                var emailsHoy = await _emailLogRepo.CountAsync(Y<EmailLog>(e => e.FecCrea >= hoy, fE), ct);
                var emailsFallidos = await _emailLogRepo.CountAsync(Y<EmailLog>(e => e.Estado == "Error", fE), ct);
                var colaEmail = await _emailLogRepo.CountAsync(Y<EmailLog>(e => e.Estado == "pendiente", fE), ct);
                var notifs = await _notifRepo.CountAsync(Y<Notificacion>(n => !n.Leida, fN), ct);

                var error = PrimerError(totalUsuarios, activos, bloqueados, eliminados, locales, externos, mixtos, tenants, apps,
                    roles, permisos, sesiones, dispConf, identidades, emailsHoy, emailsFallidos, colaEmail, notifs);
                if (error is not null) return Result<DashboardEjecutivoDto>.Failure(error);

                return Result<DashboardEjecutivoDto>.Success(new()
                {
                    TotalUsuarios = totalUsuarios.Value,
                    UsuariosActivos = activos.Value,
                    UsuariosBloqueados = bloqueados.Value,
                    UsuariosEliminados = eliminados.Value,
                    UsuariosLocales = locales.Value,
                    UsuariosExternos = externos.Value,
                    UsuariosMixtos = mixtos.Value,
                    TotalTenants = tenants.Value,
                    TotalApps = apps.Value,
                    TotalRoles = roles.Value,
                    TotalPermisos = permisos.Value,
                    SesionesActivas = sesiones.Value,
                    DispositivosConfiables = dispConf.Value,
                    IdentidadesExternas = identidades.Value,
                    EmailsEnviadosHoy = emailsHoy.Value,
                    EmailsFallidos = emailsFallidos.Value,
                    ColaEmailPendiente = colaEmail.Value,
                    NotificacionesNoLeidas = notifs.Value
                });
            }
            catch (Exception ex) { return Result<DashboardEjecutivoDto>.Failure("DB_ERROR", ex.Message); }
        });
    }

    public async Task<Result<DashboardSeguridadDto>> GetSeguridadAsync(int? idTenant = null, CancellationToken ct = default)
    {
        return await CachedAsync(CacheKey("Seguridad", idTenant), async () =>
        {
            try
            {
                var idsR = await IdsTenantAsync(idTenant, ct);
                if (idsR.IsFailure) return Result<DashboardSeguridadDto>.Failure(idsR.Error!);
                var ids = idsR.Value;
                Expression<Func<Usuario, bool>>? fU = ids is null ? null : u => ids.Contains(u.Id);
                Expression<Func<IntentoAcceso, bool>>? fIn = ids is null ? null : i => i.IdUsuario.HasValue && ids.Contains(i.IdUsuario.Value);
                Expression<Func<Bloqueo, bool>>? fB = ids is null ? null : b => ids.Contains(b.IdUsuario);
                Expression<Func<MFA, bool>>? fM = ids is null ? null : m => ids.Contains(m.IdUsuario);
                Expression<Func<HistorialPwd, bool>>? fH = ids is null ? null : h => ids.Contains(h.IdUsuario);
                Expression<Func<IdenExt, bool>>? fI = ids is null ? null : i => ids.Contains(i.IdUsuario);
                Expression<Func<DispConfiable, bool>>? fD = ids is null ? null : d => ids.Contains(d.IdUsuario);

                var hoy = DateTime.Today;
                var en7 = hoy.AddDays(7);
                var ult24h = hoy.AddHours(-24);
                var hace30 = hoy.AddDays(-30);

                var mfaUsuarios = await _mfaRepo.CountByAsync(m => m.IdUsuario, Y<MFA>(m => m.EsPrincipal && m.IdEstado == 1, fM), ct: ct);
                var idenUsuarios = await _idenExtRepo.CountByAsync(i => i.IdUsuario, Y<IdenExt>(i => !i.Eliminado, fI), ct: ct);
                var ipsSospechosas = await _intentoRepo.CountByAsync(i => i.DireccionIP!.Direccion, Y<IntentoAcceso>(i => !i.Exitoso && i.DireccionIP != null, fIn), ct: ct);
                var paises = await _ipRepo.CountByAsync(i => i.Pais, i => i.Pais != null, ct: ct);
                var serie = await _intentoRepo.CountByAsync(i => i.FecIntento.Date, Y<IntentoAcceso>(i => i.FecIntento >= hace30, fIn), ct: ct);
                var agrupados = PrimerError(mfaUsuarios, idenUsuarios, ipsSospechosas, paises, serie);
                if (agrupados is not null) return Result<DashboardSeguridadDto>.Failure(agrupados);
                var mfaIds = Claves(mfaUsuarios.Value);
                var idenUserIds = Claves(idenUsuarios.Value);

                var intentosTotal = await _intentoRepo.CountAsync(Y<IntentoAcceso>(i => true, fIn), ct);
                var correctos = await _intentoRepo.CountAsync(Y<IntentoAcceso>(i => i.Exitoso, fIn), ct);
                var fallidos = await _intentoRepo.CountAsync(Y<IntentoAcceso>(i => !i.Exitoso, fIn), ct);
                var bloqueosActivos = await _bloqueoRepo.CountAsync(Y<Bloqueo>(b => b.Activo, fB), ct);
                var desbloqueosHoy = await _bloqueoRepo.CountAsync(Y<Bloqueo>(b => !b.Activo && b.FecFin >= hoy, fB), ct);
                var mfaHabilitado = await _mfaRepo.CountAsync(Y<MFA>(m => m.EsPrincipal && m.IdEstado == 1, fM), ct);
                var mfaPendiente = await _mfaRepo.CountAsync(Y<MFA>(m => m.IdEstado == 3, fM), ct);
                var pwdExpiradas = await _histPwdRepo.CountAsync(Y<HistorialPwd>(h => h.EsActual && h.FecExpira < hoy, fH), ct);
                var pwdProximas = await _histPwdRepo.CountAsync(Y<HistorialPwd>(h => h.EsActual && h.FecExpira >= hoy && h.FecExpira <= en7, fH), ct);
                var alertas = await _bloqueoRepo.CountAsync(Y<Bloqueo>(b => b.Activo && b.FecInicio >= ult24h, fB), ct);
                var nuevosDisp = await _dispConfRepo.CountAsync(Y<DispConfiable>(d => d.FecAlta >= ult24h, fD), ct);
                var nuevosProv = await _idenExtRepo.CountAsync(Y<IdenExt>(i => !i.Eliminado && i.FecCrea >= ult24h, fI), ct);
                var sinMfa = await _usuarioRepo.CountAsync(Y<Usuario>(u => !u.Eliminado && !mfaIds.Contains(u.Id), fU), ct);
                var sinEmail = await _usuarioRepo.CountAsync(Y<Usuario>(u => string.IsNullOrWhiteSpace(u.Email), fU), ct);
                var hibridos = await _usuarioRepo.CountAsync(Y<Usuario>(u => u.TienePasswordLocal && !u.Eliminado && idenUserIds.Contains(u.Id), fU), ct);

                var error = PrimerError(intentosTotal, correctos, fallidos, bloqueosActivos, desbloqueosHoy, mfaHabilitado, mfaPendiente,
                    pwdExpiradas, pwdProximas, alertas, nuevosDisp, nuevosProv, sinMfa, sinEmail, hibridos);
                if (error is not null) return Result<DashboardSeguridadDto>.Failure(error);

                return Result<DashboardSeguridadDto>.Success(new()
                {
                    IntentosLoginTotal = intentosTotal.Value,
                    LoginsCorrectos = correctos.Value,
                    LoginsFallidos = fallidos.Value,
                    BloqueosActivos = bloqueosActivos.Value,
                    DesbloqueosHoy = desbloqueosHoy.Value,
                    MFAHabilitado = mfaHabilitado.Value,
                    MFAPendiente = mfaPendiente.Value,
                    PasswordsExpiradas = pwdExpiradas.Value,
                    PasswordsProximasVencer = pwdProximas.Value,
                    AlertasSeguridad = alertas.Value,
                    IPsSospechosas = ipsSospechosas.Value.Count,
                    PaisesDetectados = paises.Value.Count,
                    NuevosDispositivos24h = nuevosDisp.Value,
                    NuevosProveedoresOAuth24h = nuevosProv.Value,
                    UsuariosSinMFA = sinMfa.Value,
                    UsuariosSinEmail = sinEmail.Value,
                    UsuariosHibridos = hibridos.Value,
                    IntentosUltimos30Dias = Serie(serie.Value)
                });
            }
            catch (Exception ex) { return Result<DashboardSeguridadDto>.Failure("DB_ERROR", ex.Message); }
        });
    }

    public async Task<Result<DashboardOAuthDto>> GetOAuthAsync(int? idTenant = null, CancellationToken ct = default)
    {
        return await CachedAsync(CacheKey("OAuth", idTenant), async () =>
        {
            try
            {
                var provResult = await _provIdenRepo.GetAllAsync(true, ct);
                if (provResult.IsFailure) return Result<DashboardOAuthDto>.Failure(provResult.Error!);
                var prov = provResult.Value ?? [];

                var idsR = await IdsTenantAsync(idTenant, ct);
                if (idsR.IsFailure) return Result<DashboardOAuthDto>.Failure(idsR.Error!);
                var ids = idsR.Value;
                Expression<Func<IdenExt, bool>>? fI = ids is null ? null : i => ids.Contains(i.IdUsuario);
                Expression<Func<AudIdenExt, bool>>? fA = ids is null ? null : a => a.IdUsuario.HasValue && ids.Contains(a.IdUsuario.Value);

                List<long>? idenIds = null;
                if (ids is not null)
                {
                    var idenR = await _idenExtRepo.CountByAsync(i => i.Id, fI, ct: ct);
                    if (idenR.IsFailure) return Result<DashboardOAuthDto>.Failure(idenR.Error!);
                    idenIds = Claves(idenR.Value);
                }
                Expression<Func<HistorialIdenExt, bool>>? fH = idenIds is null ? null : h => idenIds.Contains(h.IdIdenExt);
                Expression<Func<IdenExtTokens, bool>>? fT = idenIds is null ? null : t => idenIds.Contains(t.IdIdenExt);

                var hoy = DateTime.Today;
                var semana = hoy.AddDays(-7);
                var ult24h = hoy.AddHours(-24);
                var hace30 = hoy.AddDays(-30);

                var porProvR = await _idenExtRepo.CountByAsync(i => i.IdProvIden, Y<IdenExt>(i => !i.Eliminado, fI), ct: ct);
                var serie = await _audIdenExtRepo.CountByAsync(a => a.FecEvento.Date, Y<AudIdenExt>(a => a.FecEvento >= hace30, fA), ct: ct);
                var agrupados = PrimerError(porProvR, serie);
                if (agrupados is not null) return Result<DashboardOAuthDto>.Failure(agrupados);
                var porProv = porProvR.Value.ToDictionary(kv => kv.Key, kv => kv.Value);
                var topProv = prov.OrderByDescending(p => porProv.GetValueOrDefault(p.Id, 0)).FirstOrDefault();

                var tokenRefresh = await _idenExtTokensRepo.CountAsync(Y<IdenExtTokens>(t => t.FechaRenovacion.HasValue && t.FechaRenovacion >= ult24h, fT), ct);
                var tokenRevocados = await _idenExtTokensRepo.CountAsync(Y<IdenExtTokens>(t => t.Revocado && t.FechaRevocacion >= ult24h, fT), ct);
                var totalLogins = await _audIdenExtRepo.CountAsync(Y<AudIdenExt>(a => a.Evento == "LOGIN_EXTERNO", fA), ct);
                var exitosos = await _audIdenExtRepo.CountAsync(Y<AudIdenExt>(a => a.Evento == "LOGIN_EXTERNO" && (a.Resultado == "Exitoso" || a.Resultado == "Success"), fA), ct);
                var loginsHoy = await _histIdenRepo.CountAsync(Y<HistorialIdenExt>(h => h.FecCambio >= ult24h, fH), ct);
                var loginsSemana = await _histIdenRepo.CountAsync(Y<HistorialIdenExt>(h => h.FecCambio >= semana, fH), ct);
                var consActivos = await _idenExtRepo.CountAsync(Y<IdenExt>(i => !i.Eliminado && i.Activo, fI), ct);
                var consRevocados = await _idenExtRepo.CountAsync(Y<IdenExt>(i => !i.Eliminado && !i.Activo, fI), ct);
                var primerLogin = await _histIdenRepo.CountAsync(Y<HistorialIdenExt>(h => h.TipoCambio == "PrimerLogin", fH), ct);
                var autoProv = await _histIdenRepo.CountAsync(Y<HistorialIdenExt>(h => h.TipoCambio == "AutoProvision", fH), ct);
                var errores = await _audIdenExtRepo.CountAsync(Y<AudIdenExt>(a => a.Resultado == "Error" || a.Resultado == "ProviderDisabled" || a.Resultado == "Timeout" || a.Resultado == "Unavailable", fA), ct);
                var deshabilitado = await _audIdenExtRepo.CountAsync(Y<AudIdenExt>(a => a.Resultado == "ProviderDisabled", fA), ct);
                var timeout = await _audIdenExtRepo.CountAsync(Y<AudIdenExt>(a => a.Resultado == "Timeout", fA), ct);
                var unavailable = await _audIdenExtRepo.CountAsync(Y<AudIdenExt>(a => a.Resultado == "Unavailable", fA), ct);
                var revConsent = await _audIdenExtRepo.CountAsync(Y<AudIdenExt>(a => a.Evento == "REVOCAR_CONSENTIMIENTO", fA), ct);

                var error = PrimerError(tokenRefresh, tokenRevocados, totalLogins, exitosos, loginsHoy, loginsSemana, consActivos, consRevocados,
                    primerLogin, autoProv, errores, deshabilitado, timeout, unavailable, revConsent);
                if (error is not null) return Result<DashboardOAuthDto>.Failure(error);

                return Result<DashboardOAuthDto>.Success(new()
                {
                    UsuariosGoogle = porProv.GetValueOrDefault(1, 0),
                    UsuariosGithub = porProv.GetValueOrDefault(3, 0),
                    UsuariosFacebook = porProv.GetValueOrDefault(6, 0),
                    UsuariosInstagram = porProv.GetValueOrDefault(7, 0),
                    ProveedorMasUtilizado = topProv?.Nombre ?? "N/A",
                    LoginsOAuthHoy = loginsHoy.Value,
                    LoginsOAuthSemana = loginsSemana.Value,
                    UsuariosVinculados = porProv.Values.Sum(),
                    ConsentimientosActivos = consActivos.Value,
                    ConsentimientosRevocados = consRevocados.Value,
                    DesgloseProveedores = prov.Select(p => new ProveedorConteoDto
                    {
                        Codigo = p.Codigo, Nombre = p.Nombre,
                        Vinculaciones = porProv.GetValueOrDefault(p.Id, 0)
                    }).OrderByDescending(p => p.Vinculaciones).ToList(),
                    PrimerLogin = primerLogin.Value,
                    UsuariosAutoProvisionados = autoProv.Value,
                    ErroresOAuth = errores.Value,
                    ProviderDeshabilitado = deshabilitado.Value,
                    ProviderTimeout = timeout.Value,
                    ProviderUnavailable = unavailable.Value,
                    TokenRefreshUsage = tokenRefresh.Value,
                    RevocacionesTokens = tokenRevocados.Value,
                    RevocacionesConsent = revConsent.Value,
                    TasaExito = totalLogins.Value > 0 ? Math.Round((double)exitosos.Value / totalLogins.Value * 100, 1) : 100.0,
                    OAuthUltimos30Dias = Serie(serie.Value)
                });
            }
            catch (Exception ex) { return Result<DashboardOAuthDto>.Failure("DB_ERROR", ex.Message); }
        });
    }

    public async Task<Result<DashboardEmailDto>> GetEmailAsync(int? idTenant = null, CancellationToken ct = default)
    {
        return await CachedAsync(CacheKey("Email", idTenant), async () =>
        {
            try
            {
                var templatesR = await _templateRepo.GetAllAsync(true, ct);
                if (templatesR.IsFailure) return Result<DashboardEmailDto>.Failure(templatesR.Error!);
                var templates = templatesR.Value ?? [];

                var idsR = await IdsTenantAsync(idTenant, ct);
                if (idsR.IsFailure) return Result<DashboardEmailDto>.Failure(idsR.Error!);
                var ids = idsR.Value;
                Expression<Func<EmailLog, bool>>? fE = ids is null ? null : l => l.IdUsuario.HasValue && ids.Contains(l.IdUsuario.Value);

                var hoy = DateTime.Today;
                var semana = hoy.AddDays(-7);
                var mes = hoy.AddDays(-30);

                var porTemplateR = await _emailLogRepo.CountByAsync(l => new { l.IdTemplate, l.Estado }, Y<EmailLog>(l => l.IdTemplate != null, fE), ct: ct);
                var enviados = await _emailLogRepo.CountAsync(Y<EmailLog>(l => l.Estado == "Enviado" || l.Estado == "Sent", fE), ct);
                var pendientes = await _emailLogRepo.CountAsync(Y<EmailLog>(l => l.Estado == "pendiente" || l.Estado == "Pending", fE), ct);
                var errores = await _emailLogRepo.CountAsync(Y<EmailLog>(l => l.Estado == "Error", fE), ct);
                var emailsHoy = await _emailLogRepo.CountAsync(Y<EmailLog>(l => l.FecCrea >= hoy, fE), ct);
                var emailsSemana = await _emailLogRepo.CountAsync(Y<EmailLog>(l => l.FecCrea >= semana, fE), ct);
                var emailsMes = await _emailLogRepo.CountAsync(Y<EmailLog>(l => l.FecCrea >= mes, fE), ct);

                var error = PrimerError(porTemplateR, enviados, pendientes, errores, emailsHoy, emailsSemana, emailsMes);
                if (error is not null) return Result<DashboardEmailDto>.Failure(error);

                var porTemplate = porTemplateR.Value;
                var topTemplates = porTemplate.GroupBy(kv => kv.Key.IdTemplate!.Value)
                    .Select(g => new EmailTemplateStatsDto
                    {
                        TemplateCode = g.Key.ToString(),
                        TemplateNombre = templates.FirstOrDefault(t => t.Id == g.Key)?.Nombre ?? g.Key.ToString(),
                        Enviados = g.Where(kv => kv.Key.Estado is "Enviado" or "Sent").Sum(kv => kv.Value),
                        Fallidos = g.Where(kv => kv.Key.Estado == "Error").Sum(kv => kv.Value)
                    }).OrderByDescending(t => t.Enviados).Take(10).ToList();

                return Result<DashboardEmailDto>.Success(new()
                {
                    EmailsEnviados = enviados.Value,
                    EmailsPendientes = pendientes.Value,
                    EmailsError = errores.Value,
                    TemplatesUtilizados = porTemplate.Select(kv => kv.Key.IdTemplate).Distinct().Count(),
                    TemplatesFallidos = porTemplate.Where(kv => kv.Key.Estado == "Error").Select(kv => kv.Key.IdTemplate).Distinct().Count(),
                    SMTPConectado = 1,
                    TiempoPromedioEnvioMs = 0,
                    ColaEmail = pendientes.Value,
                    EmailsHoy = emailsHoy.Value,
                    EmailsSemana = emailsSemana.Value,
                    EmailsMes = emailsMes.Value,
                    TopTemplates = topTemplates
                });
            }
            catch (Exception ex) { return Result<DashboardEmailDto>.Failure("DB_ERROR", ex.Message); }
        });
    }

    public async Task<Result<DashboardOperacionalDto>> GetOperacionalAsync(int? idTenant = null, CancellationToken ct = default)
    {
        return await CachedAsync(CacheKey("Operacional", idTenant), async () =>
        {
            try
            {
                var idsR = await IdsTenantAsync(idTenant, ct);
                if (idsR.IsFailure) return Result<DashboardOperacionalDto>.Failure(idsR.Error!);
                var ids = idsR.Value;
                Expression<Func<IntentoAcceso, bool>>? fIn = ids is null ? null : i => i.IdUsuario.HasValue && ids.Contains(i.IdUsuario.Value);

                // Promedio ponderado por valor: GROUP BY TpoRespuesta en la BD en vez de cargar los intentos.
                var tpoR = await _intentoRepo.CountByAsync(i => i.TpoRespuesta, Y<IntentoAcceso>(i => i.TpoRespuesta > 0, fIn), ct: ct);
                if (tpoR.IsFailure) return Result<DashboardOperacionalDto>.Failure(tpoR.Error!);
                var tpoCantidad = tpoR.Value.Sum(kv => kv.Value);
                var tpoSuma = tpoR.Value.Sum(kv => (long)kv.Key!.Value * kv.Value);

                var proceso = Process.GetCurrentProcess();
                var cpu = Math.Round((proceso.TotalProcessorTime.TotalMilliseconds / Math.Max(1, (DateTime.Now - proceso.StartTime).TotalMilliseconds)) * 100, 1);
                var ramMb = Math.Round(proceso.WorkingSet64 / 1024.0 / 1024.0, 1);

                var bgJobs = (await _bgStatus.GetBackgroundJobsAsync(ct)).Value ?? [];

                return Result<DashboardOperacionalDto>.Success(new()
                {
                    CpuPorcentaje = cpu,
                    RamMb = ramMb,
                    TiempoRespuestaLoginMs = tpoCantidad > 0 ? Math.Round((double)tpoSuma / tpoCantidad, 2) : 0,
                    TiempoRespuestaApiMs = 0,
                    TiempoSmtpMs = 0,
                    TiempoOAuthMs = 0,
                    TiempoSqlMs = 0,
                    BackgroundJobs = bgJobs,
                    HealthChecks =
                    [
                        new() { Nombre = "API", Saludable = true, Mensaje = "Operativo" },
                        new() { Nombre = "Base de Datos", Saludable = true, Mensaje = "Conectado" },
                        new() { Nombre = "Email Background", Saludable = true, Mensaje = "Activo" },
                        new() { Nombre = "Password Expiration", Saludable = true, Mensaje = "Activo" },
                        new() { Nombre = "Background Jobs", Saludable = bgJobs.All(j => j.Estado == "Activo"), Mensaje = $"{bgJobs.Count} jobs" }
                    ]
                });
            }
            catch (Exception ex) { return Result<DashboardOperacionalDto>.Failure("DB_ERROR", ex.Message); }
        });
    }



    public async Task<Result<DashboardDispositivosDto>> GetDispositivosAsync(int? idTenant = null, CancellationToken ct = default)
    {
        return await CachedAsync(CacheKey("Dispositivos", idTenant), async () =>
        {
            try
            {
                var ult24h = DateTime.Now.AddHours(-24);

                var idsR = await IdsTenantAsync(idTenant, ct);
                if (idsR.IsFailure) return Result<DashboardDispositivosDto>.Failure(idsR.Error!);
                var ids = idsR.Value;
                Expression<Func<DispConfiable, bool>>? fD = ids is null ? null : d => ids.Contains(d.IdUsuario);

                var activos = await _dispConfRepo.CountAsync(Y<DispConfiable>(d => d.Confiable, fD), ct);
                var bloqueados = await _dispConfRepo.CountAsync(Y<DispConfiable>(d => !d.Confiable, fD), ct);
                var nuevos = await _dispConfRepo.CountAsync(Y<DispConfiable>(d => d.FecAlta >= ult24h, fD), ct);
                var porSO = await _dispRepo.CountByAsync(d => d.SO!, d => d.SO != null, ct: ct);
                var porNavegador = await _dispRepo.CountByAsync(d => d.Navegador!, d => d.Navegador != null, ct: ct);
                var porPais = await _dispRepo.CountByAsync(d => d.Pais!, d => d.Pais != null, ct: ct);
                var totalIPs = await _ipRepo.CountAsync(ct);
                var ultimos = await _dispConfRepo.TopAsync(
                    d => new { d.Id, d.IdDisp, d.Disp!.Fabricante, d.Disp.Modelo, d.Disp.Navegador, d.Disp.SO, d.Disp.Pais, d.FecAlta, d.Confiable },
                    d => d.FecAlta, 10, predicate: Y<DispConfiable>(d => d.Disp != null, fD), ct: ct);

                var error = PrimerError(activos, bloqueados, nuevos, porSO, porNavegador, porPais, totalIPs, ultimos);
                if (error is not null) return Result<DashboardDispositivosDto>.Failure(error);

                return Result<DashboardDispositivosDto>.Success(new()
                {
                    DispositivosActivos = activos.Value,
                    DispositivosBloqueados = bloqueados.Value,
                    DispositivosEliminados = 0,
                    DispositivosNuevos24h = nuevos.Value,
                    PorSO = Items(porSO.Value),
                    PorNavegador = Items(porNavegador.Value),
                    PorPais = Items(porPais.Value),
                    TotalIPs = totalIPs.Value,
                    UltimosDispositivos = ultimos.Value.Select(d => new UltimoDispositivoDto
                    {
                        Id = d.Id, Nombre = d.Fabricante ?? d.Modelo ?? $"Disp#{d.IdDisp}",
                        Navegador = d.Navegador,
                        SO = d.SO,
                        Pais = d.Pais,
                        FecRegistro = d.FecAlta, Confiable = d.Confiable
                    }).ToList()
                });
            }
            catch (Exception ex) { return Result<DashboardDispositivosDto>.Failure("DB_ERROR", ex.Message); }
        });
    }

    public async Task<Result<DashboardAuditoriaDto>> GetAuditoriaAsync(int? idTenant = null, CancellationToken ct = default)
    {
        return await CachedAsync(CacheKey("Auditoria", idTenant), async () =>
        {
            try
            {
                var hoy = DateTime.Today;
                var semana = hoy.AddDays(-7);
                var hace30 = hoy.AddDays(-30);

                var idsR = await IdsTenantAsync(idTenant, ct);
                if (idsR.IsFailure) return Result<DashboardAuditoriaDto>.Failure(idsR.Error!);
                var ids = idsR.Value;
                Expression<Func<AuditoriaPwd, bool>>? fA = ids is null ? null : a => ids.Contains(a.IdUsuario);

                var tiposR = await _audRepo.CountByAsync(a => a.IdTipoAccion, Y<AuditoriaPwd>(a => a.FecAccion >= hace30, fA), ct: ct);
                var usuariosR = await _audRepo.CountByAsync(a => a.IdUsuario, fA, ct: ct);
                var serie = await _audRepo.CountByAsync(a => a.FecAccion!.Value.Date, Y<AuditoriaPwd>(a => a.FecAccion >= hace30, fA), ct: ct);
                var eventosHoy = await _audRepo.CountAsync(Y<AuditoriaPwd>(a => a.FecAccion >= hoy, fA), ct);
                var eventosSemana = await _audRepo.CountAsync(Y<AuditoriaPwd>(a => a.FecAccion >= semana, fA), ct);
                var eventosMes = await _audRepo.CountAsync(Y<AuditoriaPwd>(a => a.FecAccion >= hace30, fA), ct);
                var cambiosOAuth = await _audRepo.CountAsync(Y<AuditoriaPwd>(a => a.IdTipoAccion == 15 || a.IdTipoAccion == 16, fA), ct);

                var error = PrimerError(tiposR, usuariosR, serie, eventosHoy, eventosSemana, eventosMes, cambiosOAuth);
                if (error is not null) return Result<DashboardAuditoriaDto>.Failure(error);
                var tiposAccion = tiposR.Value.ToDictionary(kv => kv.Key, kv => kv.Value);

                return Result<DashboardAuditoriaDto>.Success(new()
                {
                    EventosHoy = eventosHoy.Value,
                    EventosSemana = eventosSemana.Value,
                    EventosMes = eventosMes.Value,
                    UsuariosAuditados = usuariosR.Value.Count,
                    CambiosPassword = tiposAccion.GetValueOrDefault(1, 0) + tiposAccion.GetValueOrDefault(3, 0),
                    CambiosOAuth = cambiosOAuth.Value,
                    CambiosRoles = tiposAccion.GetValueOrDefault(9, 0),
                    CambiosPermisos = tiposAccion.GetValueOrDefault(10, 0),
                    CambiosMFA = tiposAccion.GetValueOrDefault(11, 0),
                    CambiosEmail = tiposAccion.GetValueOrDefault(12, 0),
                    CambiosTenant = tiposAccion.GetValueOrDefault(13, 0),
                    CambiosAplicacion = tiposAccion.GetValueOrDefault(14, 0),
                    AuditoriaUltimos30Dias = Serie(serie.Value)
                });
            }
            catch (Exception ex) { return Result<DashboardAuditoriaDto>.Failure("DB_ERROR", ex.Message); }
        });
    }

    public async Task<Result<DashboardTendenciasDto>> GetTendenciasAsync(int? idTenant = null, CancellationToken ct = default)
    {
        return await CachedAsync(CacheKey("Tendencias", idTenant), async () =>
        {
            try
            {
                var idsR = await IdsTenantAsync(idTenant, ct);
                if (idsR.IsFailure) return Result<DashboardTendenciasDto>.Failure(idsR.Error!);
                var ids = idsR.Value;
                Expression<Func<Usuario, bool>>? fU = ids is null ? null : u => ids.Contains(u.Id);
                Expression<Func<IntentoAcceso, bool>>? fIn = ids is null ? null : i => i.IdUsuario.HasValue && ids.Contains(i.IdUsuario.Value);
                Expression<Func<AudIdenExt, bool>>? fA = ids is null ? null : a => a.IdUsuario.HasValue && ids.Contains(a.IdUsuario.Value);
                Expression<Func<MFA, bool>>? fM = ids is null ? null : m => ids.Contains(m.IdUsuario);
                Expression<Func<HistorialPwd, bool>>? fH = ids is null ? null : h => ids.Contains(h.IdUsuario);

                var hace30 = DateTime.Today.AddDays(-30);

                var usuarios = await _usuarioRepo.CountByAsync(u => u.FecCrea!.Value.Date, Y<Usuario>(u => u.FecCrea >= hace30, fU), ct: ct);
                var logins = await _intentoRepo.CountByAsync(i => i.FecIntento.Date, Y<IntentoAcceso>(i => i.FecIntento >= hace30 && i.Exitoso, fIn), ct: ct);
                var emails = await _emailLogRepo.CountByAsync(e => e.FecCrea.Date, e => e.FecCrea >= hace30, ct: ct);
                var oauth = await _audIdenExtRepo.CountByAsync(a => a.FecEvento.Date, Y<AudIdenExt>(a => a.FecEvento >= hace30, fA), ct: ct);
                var errores = await _intentoRepo.CountByAsync(i => i.FecIntento.Date, Y<IntentoAcceso>(i => i.FecIntento >= hace30 && !i.Exitoso, fIn), ct: ct);
                var mfa = await _mfaRepo.CountByAsync(m => m.FecAlta.Date, Y<MFA>(m => m.FecAlta >= hace30, fM), ct: ct);
                var passwords = await _histPwdRepo.CountByAsync(h => h.FecRegistro!.Value.Date, Y<HistorialPwd>(h => h.FecRegistro >= hace30, fH), ct: ct);

                var error = PrimerError(usuarios, logins, emails, oauth, errores, mfa, passwords);
                if (error is not null) return Result<DashboardTendenciasDto>.Failure(error);

                return Result<DashboardTendenciasDto>.Success(new()
                {
                    UsuariosUltimos30Dias = Serie(usuarios.Value),
                    LoginsUltimos30Dias = Serie(logins.Value),
                    EmailsUltimos30Dias = Serie(emails.Value),
                    OAuthUltimos30Dias = Serie(oauth.Value),
                    ErroresUltimos30Dias = Serie(errores.Value),
                    MFAUltimos30Dias = Serie(mfa.Value),
                    PasswordsUltimos30Dias = Serie(passwords.Value)
                });
            }
            catch (Exception ex) { return Result<DashboardTendenciasDto>.Failure("DB_ERROR", ex.Message); }
        });
    }

    public async Task<Result<DashboardEstadoGeneralDto>> GetEstadoGeneralAsync(int? idTenant = null, CancellationToken ct = default)
    {
        return await CachedAsync(CacheKey("EstadoGeneral", idTenant), async () =>
        {
            try
            {
                var idsR = await IdsTenantAsync(idTenant, ct);
                if (idsR.IsFailure) return Result<DashboardEstadoGeneralDto>.Failure(idsR.Error!);
                var ids = idsR.Value;
                Expression<Func<Bloqueo, bool>>? fB = ids is null ? null : b => ids.Contains(b.IdUsuario);
                Expression<Func<MFA, bool>>? fM = ids is null ? null : m => ids.Contains(m.IdUsuario);
                Expression<Func<HistorialPwd, bool>>? fH = ids is null ? null : h => ids.Contains(h.IdUsuario);
                Expression<Func<AuditoriaPwd, bool>>? fA = ids is null ? null : a => ids.Contains(a.IdUsuario);

                var ultHora = DateTime.Now.AddHours(-1);
                var ahora = DateTime.Now;

                var bloqueosActivos = await _bloqueoRepo.CountAsync(Y<Bloqueo>(b => b.Activo, fB), ct);
                var emailsErrorReciente = await _emailLogRepo.CountAsync(e => e.Estado == "Error" && e.FecCrea >= ultHora, ct);
                var emailsError = await _emailLogRepo.CountAsync(e => e.Estado == "Error", ct);
                var mfaPendientes = await _mfaRepo.CountAsync(Y<MFA>(m => m.IdEstado == 3, fM), ct);
                var pwdExpiradas = await _histPwdRepo.CountAsync(Y<HistorialPwd>(h => h.EsActual && h.FecExpira < ahora, fH), ct);
                var auditoria = await _audRepo.CountAsync(Y<AuditoriaPwd>(a => true, fA), ct);

                var error = PrimerError(bloqueosActivos, emailsErrorReciente, emailsError, mfaPendientes, pwdExpiradas, auditoria);
                if (error is not null) return Result<DashboardEstadoGeneralDto>.Failure(error);
                var nBloqueos = bloqueosActivos.Value;
                var nErrorReciente = emailsErrorReciente.Value;
                var nExpiradas = pwdExpiradas.Value;
                var nAuditoria = auditoria.Value;

                return Result<DashboardEstadoGeneralDto>.Success(new()
                {
                    Usuarios = new() { Estado = nBloqueos > 0 ? "yellow" : "green", Mensaje = nBloqueos > 0 ? $"{nBloqueos} bloqueos" : "OK" },
                    Email = new() { Estado = nErrorReciente > 0 ? (nErrorReciente > 5 ? "red" : "yellow") : "green", Mensaje = nErrorReciente > 0 ? $"{nErrorReciente} errores recientes" : "Disponible" },
                    MFA = new() { Estado = mfaPendientes.Value > 0 ? "yellow" : "green", Mensaje = mfaPendientes.Value > 0 ? "Pendientes" : "OK" },
                    Password = new() { Estado = nExpiradas > 0 ? (nExpiradas > 10 ? "red" : "yellow") : "green", Mensaje = nExpiradas > 0 ? $"{nExpiradas} expiradas" : "OK" },
                    Auditoria = new() { Estado = nAuditoria > 0 ? "green" : "yellow", Mensaje = nAuditoria > 0 ? $"{nAuditoria} registros" : "Sin datos" },
                    OAuth = new() { Estado = "green", Mensaje = "OK" },
                    Background = new() { Estado = "green", Mensaje = "OK" },
                    Dashboard = new() { Estado = "green", Mensaje = "Activo" },
                    API = new() { Estado = "green", Mensaje = "Operativo" },
                    BaseDatos = new() { Estado = "green", Mensaje = "Conectado", UltimaVerificacion = DateTime.Now.ToString("O") },
                    SMTP = new() { Estado = emailsError.Value > 10 ? "red" : "green", Mensaje = "Disponible" }
                });
            }
            catch (Exception ex) { return Result<DashboardEstadoGeneralDto>.Failure("DB_ERROR", ex.Message); }
        });
    }

    public async Task<Result<DashboardEjecutivoAvanzadoDto>> GetEjecutivoAvanzadoAsync(int? idTenant = null, CancellationToken ct = default)
    {
        return await CachedAsync(CacheKey("EjecutivoAvanzado", idTenant), async () =>
        {
            try
            {
                var provR = await _provIdenRepo.GetAllAsync(true, ct);
                if (provR.IsFailure) return Result<DashboardEjecutivoAvanzadoDto>.Failure(provR.Error!);
                var prov = provR.Value ?? [];

                var idsR = await IdsTenantAsync(idTenant, ct);
                if (idsR.IsFailure) return Result<DashboardEjecutivoAvanzadoDto>.Failure(idsR.Error!);
                var ids = idsR.Value;
                Expression<Func<Usuario, bool>>? fU = ids is null ? null : u => ids.Contains(u.Id);
                Expression<Func<IdenExt, bool>>? fI = ids is null ? null : i => ids.Contains(i.IdUsuario);
                Expression<Func<Rol, bool>>? fR = idTenant.HasValue && idTenant.Value > 0 ? RolesDelTenant(idTenant.Value) : null;

                var usuarios = await _usuarioRepo.TopAsync(u => u.NomUsuario, u => u.FecMod, 10, predicate: Y<Usuario>(u => !u.Eliminado, fU), ct: ct);
                var apps = await _appRepo.GetAllAsync(10, true, ct);
                var roles = await _rolRepo.TopAsync(r => r.Nombre, r => r.Id, 10, descending: false, predicate: fR, ct: ct);
                var porProv = await _idenExtRepo.CountByAsync(i => i.IdProvIden, Y<IdenExt>(i => !i.Eliminado, fI), ct: ct);
                var ips = await _ipRepo.TopAsync(i => i.Direccion, i => i.Id, 10, ct: ct);
                var porDetalle = await _audRepo.CountByAsync(a => a.Detalles!, a => a.Detalles != null, ct: ct);
                var porTemplate = await _emailLogRepo.CountByAsync(e => e.IdTemplate!.Value, e => e.IdTemplate != null, top: 10, ct: ct);
                var totalTemplates = await _emailLogRepo.CountAsync(e => e.IdTemplate != null, ct);
                var porPais = await _dispRepo.CountByAsync(d => d.Pais!, d => d.Pais != null, top: 10, ct: ct);

                var error = PrimerError(usuarios, apps, roles, porProv, ips, porDetalle, porTemplate, totalTemplates, porPais);
                if (error is not null) return Result<DashboardEjecutivoAvanzadoDto>.Failure(error);

                var totalActivas = porProv.Value.Sum(kv => kv.Value);
                var nTemplates = totalTemplates.Value;

                return Result<DashboardEjecutivoAvanzadoDto>.Success(new()
                {
                    Top10UsuariosActivos = usuarios.Value.Select(n => new CountItemDto { Nombre = n, Cantidad = 1 }).ToList(),
                    Top10Apps = (apps.Value ?? []).Select(a => new CountItemDto { Nombre = a.Nombre, Cantidad = 1 }).ToList(),
                    Top10Roles = roles.Value.Select(n => new CountItemDto { Nombre = n, Cantidad = 1 }).ToList(),
                    Top10ProveedoresOAuth = porProv.Value
                        .GroupBy(kv => prov.FirstOrDefault(p => p.Id == kv.Key)?.Nombre ?? "N/A", kv => kv.Value)
                        .Select(g => new CountItemDto { Nombre = g.Key, Cantidad = g.Sum(), Porcentaje = totalActivas > 0 ? Math.Round(g.Sum() * 100.0 / totalActivas, 1) : 0 })
                        .OrderByDescending(c => c.Cantidad).Take(10).ToList(),
                    Top10IPs = ips.Value.Select(d => new CountItemDto { Nombre = d, Cantidad = 1 }).ToList(),
                    // El recorte a 50 caracteres no se traduce a SQL: se agrupa por el texto crudo y se reagrupa aquí.
                    Top10Errores = porDetalle.Value.GroupBy(kv => kv.Key.Length > 50 ? kv.Key[..50] + "..." : kv.Key, kv => kv.Value)
                        .Select(g => new CountItemDto { Nombre = g.Key, Cantidad = g.Sum() })
                        .OrderByDescending(c => c.Cantidad).Take(10).ToList(),
                    Top10Templates = porTemplate.Value
                        .Select(kv => new CountItemDto { Nombre = kv.Key.ToString(), Cantidad = kv.Value, Porcentaje = nTemplates > 0 ? Math.Round(kv.Value * 100.0 / nTemplates, 1) : 0 })
                        .OrderByDescending(c => c.Cantidad).ToList(),
                    Top10Paises = porPais.Value
                        .Select(kv => new CountItemDto { Nombre = kv.Key, Cantidad = kv.Value })
                        .OrderByDescending(c => c.Cantidad).ToList()
                });
            }
            catch (Exception ex) { return Result<DashboardEjecutivoAvanzadoDto>.Failure("DB_ERROR", ex.Message); }
        });
    }

    public async Task<Result<List<BackgroundJobDto>>> GetBackgroundJobsAsync(CancellationToken ct)
    {
        return await CachedAsync("BackgroundJobs", async () =>
        {
            try
            {
                return await _bgStatus.GetBackgroundJobsAsync(ct);
            }
            catch (Exception ex) { return Result<List<BackgroundJobDto>>.Failure("DB_ERROR", ex.Message); }
        });
    }

    private async Task<Result<T>> CachedAsync<T>(string cacheKey, Func<Task<Result<T>>> factory, CancellationToken ct = default) where T : class
    {
        var cached = await _cache.GetAsync<T>(cacheKey, ct);
        if (cached is not null)
            return Result<T>.Success(cached);
        var result = await factory();
        if (result.IsSuccess && result.Value is not null)
            await _cache.SetAsync(cacheKey, result.Value, new CacheEntryOptions(TimeSpan.FromSeconds(30)), ct);
        return result;
    }

    /// <summary>Ids de los miembros activos del tenant; null sin tenant (modo Plataforma, sin filtro).</summary>
    private async Task<Result<List<int>?>> IdsTenantAsync(int? idTenant, CancellationToken ct)
    {
        if (!idTenant.HasValue) return Result<List<int>?>.Success(null, allowNull: true);
        var result = await _usuarioTenantRepo.ObtenerIdsUsuariosActivosPorTenantAsync(idTenant.Value, ct);
        if (result.IsFailure) return Result<List<int>?>.Failure(result.Error!);
        return Result<List<int>?>.Success([.. result.Value]);
    }

    private static Expression<Func<Rol, bool>> RolesDelTenant(int idTenant) =>
        r => r.IdTenant == idTenant || r.IdTenant == null;

    private static Expression<Func<T, bool>> Y<T>(Expression<Func<T, bool>> condicion, Expression<Func<T, bool>>? filtroTenant) =>
        filtroTenant is null ? condicion : condicion.And(filtroTenant);

    private static Error? PrimerError(params Result[] resultados) =>
        resultados.FirstOrDefault(r => r.IsFailure)?.Error;

    private static List<TKey> Claves<TKey>(IReadOnlyList<KeyValuePair<TKey, int>> filas) =>
        filas.Select(f => f.Key).ToList();

    private static List<TrendItemDto> Serie(IReadOnlyList<KeyValuePair<DateTime, int>> filas) =>
        filas.Select(f => new TrendItemDto { Fecha = f.Key, Cantidad = f.Value }).OrderBy(t => t.Fecha).ToList();

    private static List<CountItemDto> Items(IReadOnlyList<KeyValuePair<string, int>> filas) =>
        filas.Select(f => new CountItemDto { Nombre = f.Key, Cantidad = f.Value }).ToList();

    private string CacheKey(string baseKey, int? idTenant) =>
        idTenant.HasValue ? $"{baseKey}_{idTenant.Value}" : baseKey;

    private static string ExtractSO(string ua)
    {
        if (ua.Contains("Windows")) return "Windows";
        if (ua.Contains("Linux") && !ua.Contains("Android")) return "Linux";
        if (ua.Contains("Mac OS") || ua.Contains("macOS")) return "macOS";
        if (ua.Contains("Android")) return "Android";
        if (ua.Contains("iOS") || ua.Contains("iPhone")) return "iOS";
        return "Otro";
    }
}
