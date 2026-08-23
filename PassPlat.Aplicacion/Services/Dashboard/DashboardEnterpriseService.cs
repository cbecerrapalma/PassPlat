using CBP.Results;
using System.Diagnostics;
using CBP.Caching.Interfaces;
using CBP.Caching.Models;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Enums;

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
    Task<Result> InvalidateCacheAsync(CancellationToken ct = default);
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
    private static readonly string[] _cacheKeyBases = ["Ejecutivo", "Seguridad", "OAuth", "Email", "Operacional", "Auditoria", "Dispositivos", "Tendencias", "EstadoGeneral", "EjecutivoAvanzado"];

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
                var users = (await _usuarioRepo.GetAllAsync(true, ct)).Value ?? [];
            var tenants = (await _tenantRepo.GetAllAsync(true, ct)).Value ?? [];
            var apps = (await _appRepo.GetAllAsync(true, ct)).Value ?? [];
            var idenExt = (await _idenExtRepo.GetAllAsync(true, ct)).Value ?? [];
            var emails = (await _emailLogRepo.GetAllAsync(true, ct)).Value ?? [];
            var sesiones = (await _sesionRepo.GetAllAsync(true, ct)).Value ?? [];
            var dispConf = (await _dispConfRepo.GetAllAsync(true, ct)).Value ?? [];
            var roles = (await _rolRepo.GetAllAsync(true, ct)).Value ?? [];
            var permisos = (await _permisoRepo.GetAllAsync(true, ct)).Value ?? [];
            var notifs = (await _notifRepo.GetAllAsync(true, ct)).Value ?? [];

            HashSet<int>? userIds = null;
            if (idTenant.HasValue)
                userIds = await ObtenerIdsUsuariosEnTenantAsync(idTenant.Value, ct);

            var usersF = userIds != null ? users.Where(u => userIds!.Contains(u.Id)).ToList() : users;
            var rolesF = idTenant.HasValue && idTenant.Value > 0 ? roles.Where(r => r.IdTenant == idTenant.Value || r.IdTenant == null).ToList() : roles;
            var idenExtF = userIds != null ? idenExt.Where(i => userIds!.Contains(i.IdUsuario)).ToList() : idenExt;
            var emailsF = userIds != null ? emails.Where(e => e.IdUsuario.HasValue && userIds!.Contains(e.IdUsuario.Value)).ToList() : emails;
            var sesionesF = userIds != null ? sesiones.Where(s => userIds!.Contains(s.IdUsuario)).ToList() : sesiones;
            var dispConfF = userIds != null ? dispConf.Where(d => userIds!.Contains(d.IdUsuario)).ToList() : dispConf;
            var notifsF = userIds != null ? notifs.Where(n => userIds!.Contains(n.IdUsuario)).ToList() : notifs;
            var idenUserIds = idenExtF.Where(i => !i.Eliminado).Select(i => i.IdUsuario).Distinct().ToHashSet();

            return Result<DashboardEjecutivoDto>.Success(new()
            {
                TotalUsuarios = usersF.Count,
                UsuariosActivos = usersF.Count(u => u.IdEstado == 1 && !u.Eliminado),
                UsuariosBloqueados = usersF.Count(u => u.IdEstado == 3),
                UsuariosEliminados = usersF.Count(u => u.Eliminado),
                UsuariosLocales = usersF.Count(u => u.TienePasswordLocal && !u.Eliminado && !idenUserIds.Contains(u.Id)),
                UsuariosExternos = usersF.Count(u => !u.TienePasswordLocal && !u.Eliminado && idenUserIds.Contains(u.Id)),
                UsuariosMixtos = usersF.Count(u => u.TienePasswordLocal && !u.Eliminado && idenUserIds.Contains(u.Id)),
                TotalTenants = idTenant.HasValue ? 1 : tenants.Count(t => t.Activo),
                TotalApps = apps.Count,
                TotalRoles = rolesF.Count,
                TotalPermisos = permisos.Count,
                SesionesActivas = sesionesF.Count(s => s.EsActiva),
                DispositivosConfiables = dispConfF.Count(d => d.Confiable),
                IdentidadesExternas = idenExtF.Count(i => !i.Eliminado),
                EmailsEnviadosHoy = emailsF.Count(e => e.FecCrea >= DateTime.Today),
                EmailsFallidos = emailsF.Count(e => e.Estado == "Error"),
                ColaEmailPendiente = emailsF.Count(e => e.Estado == "pendiente"),
                NotificacionesNoLeidas = notifsF.Count(n => !n.Leida)
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
            var users = (await _usuarioRepo.GetAllAsync(true, ct)).Value ?? [];
            var intentos = (await _intentoRepo.GetAllAsync(true, ct)).Value ?? [];
            var bloqueos = (await _bloqueoRepo.GetAllAsync(true, ct)).Value ?? [];
            var mfas = (await _mfaRepo.GetAllAsync(true, ct)).Value ?? [];
            var histPwd = (await _histPwdRepo.GetAllAsync(true, ct)).Value ?? [];
            var idenExt = (await _idenExtRepo.GetAllAsync(true, ct)).Value ?? [];
            var dispConf = (await _dispConfRepo.GetAllAsync(true, ct)).Value ?? [];
            var ips = (await _ipRepo.GetAllAsync(true, ct)).Value ?? [];
            var disp = (await _dispRepo.GetAllAsync(true, ct)).Value ?? [];

            HashSet<int>? userIds = null;
            if (idTenant.HasValue)
                userIds = await ObtenerIdsUsuariosEnTenantAsync(idTenant.Value, ct);

            var usersF = userIds != null ? users.Where(u => userIds!.Contains(u.Id)).ToList() : users;
            var intentosF = userIds != null ? intentos.Where(i => i.IdUsuario.HasValue && userIds!.Contains(i.IdUsuario.Value)).ToList() : intentos;
            var bloqueosF = userIds != null ? bloqueos.Where(b => userIds!.Contains(b.IdUsuario)).ToList() : bloqueos;
            var mfasF = userIds != null ? mfas.Where(m => userIds!.Contains(m.IdUsuario)).ToList() : mfas;
            var histPwdF = userIds != null ? histPwd.Where(h => userIds!.Contains(h.IdUsuario)).ToList() : histPwd;
            var idenExtF = userIds != null ? idenExt.Where(i => userIds!.Contains(i.IdUsuario)).ToList() : idenExt;
            var dispConfF = userIds != null ? dispConf.Where(d => userIds!.Contains(d.IdUsuario)).ToList() : dispConf;

            var pwdActual = histPwdF.Where(h => h.EsActual).ToList();
            var hoy = DateTime.Today;
            var ult24h = hoy.AddHours(-24);
            var hace30 = hoy.AddDays(-30);
            var idenUserIds = idenExtF.Where(i => !i.Eliminado).Select(i => i.IdUsuario).Distinct().ToHashSet();

            return Result<DashboardSeguridadDto>.Success(new()
            {
                IntentosLoginTotal = intentosF.Count,
                LoginsCorrectos = intentosF.Count(i => i.Exitoso),
                LoginsFallidos = intentosF.Count(i => !i.Exitoso),
                BloqueosActivos = bloqueosF.Count(b => b.Activo),
                DesbloqueosHoy = bloqueosF.Count(b => !b.Activo && b.FecFin >= hoy),
                MFAHabilitado = mfasF.Count(m => m.EsPrincipal && m.IdEstado == 1),
                MFAPendiente = mfasF.Count(m => m.IdEstado == 3),
                PasswordsExpiradas = pwdActual.Count(h => h.FecExpira < hoy),
                PasswordsProximasVencer = pwdActual.Count(h => h.FecExpira >= hoy && h.FecExpira <= hoy.AddDays(7)),
                AlertasSeguridad = bloqueosF.Count(b => b.Activo && b.FecInicio >= ult24h),
                IPsSospechosas = intentosF.Where(i => !i.Exitoso && i.DireccionIP != null).Select(i => i.DireccionIP!.Direccion).Distinct().Count(),
                PaisesDetectados = ips.Select(i => i.Pais).Where(p => p != null).Distinct().Count(),
                NuevosDispositivos24h = dispConfF.Count(d => d.FecAlta >= ult24h),
                NuevosProveedoresOAuth24h = idenExtF.Count(i => !i.Eliminado && i.FecCrea >= ult24h),
                UsuariosSinMFA = usersF.Count(u => !u.Eliminado && !mfasF.Any(m => m.IdUsuario == u.Id && m.EsPrincipal && m.IdEstado == 1)),
                UsuariosSinEmail = usersF.Count(u => string.IsNullOrWhiteSpace(u.Email)),
                UsuariosHibridos = usersF.Count(u => u.TienePasswordLocal && !u.Eliminado && idenUserIds.Contains(u.Id)),
                IntentosUltimos30Dias = intentosF.Where(i => i.FecIntento >= hace30)
                    .GroupBy(i => i.FecIntento.Date).Select(g => new TrendItemDto { Fecha = g.Key, Cantidad = g.Count() }).OrderBy(t => t.Fecha).ToList()
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
            var idenExtResult = await _idenExtRepo.GetAllAsync(true, ct);
            if (idenExtResult.IsFailure) return Result<DashboardOAuthDto>.Failure(idenExtResult.Error!);
            var idenExt = idenExtResult.Value ?? [];

            var provResult = await _provIdenRepo.GetAllAsync(true, ct);
            if (provResult.IsFailure) return Result<DashboardOAuthDto>.Failure(provResult.Error!);
            var prov = provResult.Value ?? [];

            var audResult = await _audIdenExtRepo.GetAllAsync(true, ct);
            if (audResult.IsFailure) return Result<DashboardOAuthDto>.Failure(audResult.Error!);
            var aud = audResult.Value ?? [];

            var histResult = await _histIdenRepo.GetAllAsync(true, ct);
            if (histResult.IsFailure) return Result<DashboardOAuthDto>.Failure(histResult.Error!);
            var hist = histResult.Value ?? [];

            HashSet<int>? userIds = null;
            if (idTenant.HasValue)
                userIds = await ObtenerIdsUsuariosEnTenantAsync(idTenant.Value, ct);

            var idenExtF = userIds != null ? idenExt.Where(i => userIds!.Contains(i.IdUsuario)).ToList() : idenExt;
            var idenIds = idenExtF.Select(i => i.Id).ToHashSet();
            var audF = userIds != null ? aud.Where(a => a.IdUsuario.HasValue && userIds!.Contains(a.IdUsuario.Value)).ToList() : aud;
            var histF = userIds != null ? hist.Where(h => idenIds.Contains(h.IdIdenExt)).ToList() : hist;
            var tokensResult = await _idenExtTokensRepo.GetAllAsync(true, ct);
            var tokens = tokensResult.IsSuccess ? tokensResult.Value ?? [] : [];
            var tokensF = userIds != null ? tokens.Where(t => idenIds.Contains(t.IdIdenExt)).ToList() : tokens;

            var activas = idenExtF.Where(i => !i.Eliminado).ToList();
            var porProv = activas.GroupBy(i => i.IdProvIden).ToDictionary(g => g.Key, g => g.Count());
            var topProv = prov.OrderByDescending(p => porProv.GetValueOrDefault(p.Id, 0)).FirstOrDefault();

            var hoy = DateTime.Today;
            var semana = hoy.AddDays(-7);
            var ult24h = hoy.AddHours(-24);

            var tokenRefreshUsage = tokensF.Count(t => t.FechaRenovacion.HasValue && t.FechaRenovacion >= ult24h);
            var tokenRevocations = tokensF.Count(t => t.Revocado && t.FechaRevocacion >= ult24h);
            var totalLogins = audF.Count(a => a.Evento == "LOGIN_EXTERNO");
            var exitosos = audF.Count(a => a.Evento == "LOGIN_EXTERNO" && (a.Resultado == "Exitoso" || a.Resultado == "Success"));

            return Result<DashboardOAuthDto>.Success(new()
            {
                UsuariosGoogle = activas.Count(i => i.IdProvIden == 1),
                UsuariosGithub = activas.Count(i => i.IdProvIden == 2),
                UsuariosLinkedIn = activas.Count(i => i.IdProvIden == 3),
                UsuariosFacebook = activas.Count(i => i.IdProvIden == 4),
                UsuariosInstagram = activas.Count(i => i.IdProvIden == 5),
                ProveedorMasUtilizado = topProv?.Nombre ?? "N/A",
                LoginsOAuthHoy = histF.Count(h => h.FecCambio >= ult24h),
                LoginsOAuthSemana = histF.Count(h => h.FecCambio >= semana),
                UsuariosVinculados = activas.Count,
                ConsentimientosActivos = activas.Count(i => i.Activo),
                ConsentimientosRevocados = activas.Count(i => !i.Activo && !i.Eliminado),
                DesgloseProveedores = prov.Select(p => new ProveedorConteoDto
                {
                    Codigo = p.Codigo, Nombre = p.Nombre,
                    Vinculaciones = porProv.GetValueOrDefault(p.Id, 0)
                }).OrderByDescending(p => p.Vinculaciones).ToList(),
                PrimerLogin = histF.Count(h => h.TipoCambio == "PrimerLogin"),
                UsuariosAutoProvisionados = histF.Count(h => h.TipoCambio == "AutoProvision"),
                ErroresOAuth = audF.Count(a => a.Resultado is "Error" or "ProviderDisabled" or "Timeout" or "Unavailable"),
                ProviderDeshabilitado = audF.Count(a => a.Resultado == "ProviderDisabled"),
                ProviderTimeout = audF.Count(a => a.Resultado == "Timeout"),
                ProviderUnavailable = audF.Count(a => a.Resultado == "Unavailable"),
                TokenRefreshUsage = tokenRefreshUsage,
                RevocacionesTokens = tokenRevocations,
                RevocacionesConsent = audF.Count(a => a.Evento == "REVOCAR_CONSENTIMIENTO"),
                TasaExito = totalLogins > 0 ? Math.Round((double)exitosos / totalLogins * 100, 1) : 100.0,
                OAuthUltimos30Dias = audF.Where(a => a.FecEvento >= hoy.AddDays(-30))
                    .GroupBy(a => a.FecEvento.Date)
                    .Select(g => new TrendItemDto { Fecha = g.Key, Cantidad = g.Count() })
                    .OrderBy(t => t.Fecha).ToList()
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
            var logs = (await _emailLogRepo.GetAllAsync(true, ct)).Value ?? [];
            var templates = (await _templateRepo.GetAllAsync(true, ct)).Value ?? [];

            HashSet<int>? userIds = null;
            if (idTenant.HasValue)
                userIds = await ObtenerIdsUsuariosEnTenantAsync(idTenant.Value, ct);

            var logsF = userIds != null ? logs.Where(l => l.IdUsuario.HasValue && userIds!.Contains(l.IdUsuario.Value)).ToList() : logs;

            var hoy = DateTime.Today;
            var semana = hoy.AddDays(-7);
            var mes = hoy.AddDays(-30);

            var enviados = logsF.Where(l => l.Estado is "Enviado" or "Sent").ToList();
            var errores = logsF.Where(l => l.Estado == "Error").ToList();

            var porTemp = logsF.Where(l => l.IdTemplate != null)
                .GroupBy(l => l.IdTemplate!.Value)
                .Select(g => new EmailTemplateStatsDto
                {
                    TemplateCode = g.Key.ToString(),
                    TemplateNombre = templates.FirstOrDefault(t => t.Id == g.Key)?.Nombre ?? g.Key.ToString(),
                    Enviados = g.Count(t => t.Estado is "Enviado" or "Sent"),
                    Fallidos = g.Count(t => t.Estado == "Error")
                }).OrderByDescending(t => t.Enviados).Take(10).ToList();

            return Result<DashboardEmailDto>.Success(new()
            {
                EmailsEnviados = enviados.Count,
                EmailsPendientes = logsF.Count(l => l.Estado is "pendiente" or "Pending"),
                EmailsError = errores.Count,
                TemplatesUtilizados = logsF.Where(l => l.IdTemplate != null).Select(l => l.IdTemplate).Distinct().Count(),
                TemplatesFallidos = errores.Where(l => l.IdTemplate != null).Select(l => l.IdTemplate).Distinct().Count(),
                SMTPConectado = 1,
                TiempoPromedioEnvioMs = 0,
                ColaEmail = logsF.Count(l => l.Estado is "pendiente" or "Pending"),
                EmailsHoy = logsF.Count(l => l.FecCrea >= hoy),
                EmailsSemana = logsF.Count(l => l.FecCrea >= semana),
                EmailsMes = logsF.Count(l => l.FecCrea >= mes),
                TopTemplates = porTemp
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
            var intentos = (await _intentoRepo.GetAllAsync(true, ct)).Value ?? [];
            var emails = (await _emailLogRepo.GetAllAsync(true, ct)).Value ?? [];

            HashSet<int>? userIds = null;
            if (idTenant.HasValue)
                userIds = await ObtenerIdsUsuariosEnTenantAsync(idTenant.Value, ct);

            var intentosF = userIds != null ? intentos.Where(i => i.IdUsuario.HasValue && userIds!.Contains(i.IdUsuario.Value)).ToList() : intentos;
            var tpoLogin = intentosF.Where(i => i.TpoRespuesta > 0).Select(i => i.TpoRespuesta).ToList();

            var proceso = Process.GetCurrentProcess();
            var cpu = Math.Round((proceso.TotalProcessorTime.TotalMilliseconds / Math.Max(1, (DateTime.Now - proceso.StartTime).TotalMilliseconds)) * 100, 1);
            var ramMb = Math.Round(proceso.WorkingSet64 / 1024.0 / 1024.0, 1);

            var bgJobs = (await _bgStatus.GetBackgroundJobsAsync(ct)).Value ?? [];

            return Result<DashboardOperacionalDto>.Success(new()
            {
                CpuPorcentaje = cpu,
                RamMb = ramMb,
                TiempoRespuestaLoginMs = tpoLogin.Count > 0 ? Math.Round(tpoLogin.Average() ?? 0, 2) : 0,
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
            var dispConf = (await _dispConfRepo.ObtenerTodosConDispositivoAsync(ct)).Value ?? [];
            var disp = (await _dispRepo.GetAllAsync(true, ct)).Value ?? [];
            var ips = (await _ipRepo.GetAllAsync(true, ct)).Value ?? [];
            var ult24h = DateTime.Now.AddHours(-24);

            HashSet<int>? userIds = null;
            if (idTenant.HasValue)
                userIds = await ObtenerIdsUsuariosEnTenantAsync(idTenant.Value, ct);

            var dispConfF = userIds != null ? dispConf.Where(d => userIds!.Contains(d.IdUsuario)).ToList() : dispConf;

            return Result<DashboardDispositivosDto>.Success(new()
            {
                DispositivosActivos = dispConfF.Count(d => d.Confiable),
                DispositivosBloqueados = dispConfF.Count(d => !d.Confiable),
                DispositivosEliminados = 0,
                DispositivosNuevos24h = dispConfF.Count(d => d.FecAlta >= ult24h),
                PorSO = disp.Where(d => d.SO != null).GroupBy(d => d.SO!)
                    .Select(g => new CountItemDto { Nombre = g.Key, Cantidad = g.Count() }).ToList(),
                PorNavegador = disp.Where(d => d.Navegador != null).GroupBy(d => d.Navegador!)
                    .Select(g => new CountItemDto { Nombre = g.Key, Cantidad = g.Count() }).ToList(),
                PorPais = disp.Where(d => d.Pais != null).GroupBy(d => d.Pais!)
                    .Select(g => new CountItemDto { Nombre = g.Key, Cantidad = g.Count() }).ToList(),
                TotalIPs = ips.Count,
                UltimosDispositivos = dispConfF.Where(d => d.Disp != null).OrderByDescending(d => d.FecAlta)
                    .Take(10).Select(d => new UltimoDispositivoDto
                    {
                        Id = d.Id, Nombre = d.Disp!.Fabricante ?? d.Disp!.Modelo ?? $"Disp#{d.IdDisp}",
                        Navegador = d.Disp!.Navegador,
                        SO = d.Disp!.SO,
                        Pais = d.Disp!.Pais,
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
                var aud = (await _audRepo.GetAllAsync(true, ct)).Value ?? [];
                var hace30 = DateTime.Today.AddDays(-30);

                HashSet<int>? userIds = null;
                if (idTenant.HasValue)
                    userIds = await ObtenerIdsUsuariosEnTenantAsync(idTenant.Value, ct);

                var audF = userIds != null ? aud.Where(a => userIds!.Contains(a.IdUsuario)).ToList() : aud;
                var tiposAccion = audF.Where(a => a.FecAccion >= hace30)
                    .GroupBy(a => a.IdTipoAccion)
                    .ToDictionary(g => g.Key, g => g.Count());

                return Result<DashboardAuditoriaDto>.Success(new()
                {
                    EventosHoy = audF.Count(a => a.FecAccion >= DateTime.Today),
                    EventosSemana = audF.Count(a => a.FecAccion >= DateTime.Today.AddDays(-7)),
                    EventosMes = audF.Count(a => a.FecAccion >= hace30),
                    UsuariosAuditados = audF.Select(a => a.IdUsuario).Distinct().Count(),
                    CambiosPassword = tiposAccion.GetValueOrDefault(1, 0) + tiposAccion.GetValueOrDefault(3, 0),
                    CambiosOAuth = audF.Count(a => a.IdTipoAccion == 15 || a.IdTipoAccion == 16),
                    CambiosRoles = tiposAccion.GetValueOrDefault(9, 0),
                    CambiosPermisos = tiposAccion.GetValueOrDefault(10, 0),
                    CambiosMFA = tiposAccion.GetValueOrDefault(11, 0),
                    CambiosEmail = tiposAccion.GetValueOrDefault(12, 0),
                    CambiosTenant = tiposAccion.GetValueOrDefault(13, 0),
                    CambiosAplicacion = tiposAccion.GetValueOrDefault(14, 0),
                    AuditoriaUltimos30Dias = audF.Where(a => a.FecAccion >= hace30)
                        .GroupBy(a => a.FecAccion!.Value.Date)
                        .Select(g => new TrendItemDto { Fecha = g.Key, Cantidad = g.Count() })
                        .OrderBy(t => t.Fecha).ToList()
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
                var users = (await _usuarioRepo.GetAllAsync(true, ct)).Value ?? [];
                var intentos = (await _intentoRepo.GetAllAsync(true, ct)).Value ?? [];
                var emails = (await _emailLogRepo.GetAllAsync(true, ct)).Value ?? [];
                var audExt = (await _audIdenExtRepo.GetAllAsync(true, ct)).Value ?? [];
                var mfas = (await _mfaRepo.GetAllAsync(true, ct)).Value ?? [];
                var histPwd = (await _histPwdRepo.GetAllAsync(true, ct)).Value ?? [];

                HashSet<int>? userIds = null;
                if (idTenant.HasValue)
                    userIds = await ObtenerIdsUsuariosEnTenantAsync(idTenant.Value, ct);

                var usersF = userIds != null ? users.Where(u => userIds!.Contains(u.Id)).ToList() : users;
                var intentosF = userIds != null ? intentos.Where(i => i.IdUsuario.HasValue && userIds!.Contains(i.IdUsuario.Value)).ToList() : intentos;
                var mfasF = userIds != null ? mfas.Where(m => userIds!.Contains(m.IdUsuario)).ToList() : mfas;
                var histPwdF = userIds != null ? histPwd.Where(h => userIds!.Contains(h.IdUsuario)).ToList() : histPwd;
                var idenIdsF = userIds != null ? (await _idenExtRepo.GetAllAsync(true, ct)).Value?.Where(i => userIds!.Contains(i.IdUsuario)).Select(i => i.Id).ToHashSet() ?? [] : null;
                var audExtF = idenIdsF != null ? audExt.Where(a => a.IdUsuario.HasValue && userIds!.Contains(a.IdUsuario.Value)).ToList() : audExt;

                var hace30 = DateTime.Today.AddDays(-30);

                return Result<DashboardTendenciasDto>.Success(new()
                {
                    UsuariosUltimos30Dias = usersF.Where(u => u.FecCrea >= hace30)
                        .GroupBy(u => u.FecCrea!.Value.Date).Select(g => new TrendItemDto { Fecha = g.Key, Cantidad = g.Count() }).OrderBy(t => t.Fecha).ToList(),
                    LoginsUltimos30Dias = intentosF.Where(i => i.FecIntento >= hace30 && i.Exitoso)
                        .GroupBy(i => i.FecIntento.Date).Select(g => new TrendItemDto { Fecha = g.Key, Cantidad = g.Count() }).OrderBy(t => t.Fecha).ToList(),
                    EmailsUltimos30Dias = emails.Where(e => e.FecCrea >= hace30)
                        .GroupBy(e => e.FecCrea.Date).Select(g => new TrendItemDto { Fecha = g.Key, Cantidad = g.Count() }).OrderBy(t => t.Fecha).ToList(),
                    OAuthUltimos30Dias = audExtF.Where(a => a.FecEvento >= hace30)
                        .GroupBy(a => a.FecEvento.Date).Select(g => new TrendItemDto { Fecha = g.Key, Cantidad = g.Count() }).OrderBy(t => t.Fecha).ToList(),
                    ErroresUltimos30Dias = intentosF.Where(i => i.FecIntento >= hace30 && !i.Exitoso)
                        .GroupBy(i => i.FecIntento.Date).Select(g => new TrendItemDto { Fecha = g.Key, Cantidad = g.Count() }).OrderBy(t => t.Fecha).ToList(),
                    MFAUltimos30Dias = mfasF.Where(m => m.FecAlta >= hace30)
                        .GroupBy(m => m.FecAlta.Date).Select(g => new TrendItemDto { Fecha = g.Key, Cantidad = g.Count() }).OrderBy(t => t.Fecha).ToList(),
                    PasswordsUltimos30Dias = histPwdF.Where(h => h.FecRegistro >= hace30)
                        .GroupBy(h => h.FecRegistro!.Value.Date).Select(g => new TrendItemDto { Fecha = g.Key, Cantidad = g.Count() }).OrderBy(t => t.Fecha).ToList()
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
                var bloqueos = (await _bloqueoRepo.GetAllAsync(true, ct)).Value ?? [];
                var emails = (await _emailLogRepo.GetAllAsync(true, ct)).Value ?? [];
                var mfas = (await _mfaRepo.GetAllAsync(true, ct)).Value ?? [];
                var histPwd = (await _histPwdRepo.GetAllAsync(true, ct)).Value ?? [];
                var aud = (await _audRepo.GetAllAsync(true, ct)).Value ?? [];

                HashSet<int>? userIds = null;
                if (idTenant.HasValue)
                    userIds = await ObtenerIdsUsuariosEnTenantAsync(idTenant.Value, ct);

                var bloqueosF = userIds != null ? bloqueos.Where(b => userIds!.Contains(b.IdUsuario)).ToList() : bloqueos;
                var mfasF = userIds != null ? mfas.Where(m => userIds!.Contains(m.IdUsuario)).ToList() : mfas;
                var histPwdF = userIds != null ? histPwd.Where(h => userIds!.Contains(h.IdUsuario)).ToList() : histPwd;
                var audF = userIds != null ? aud.Where(a => userIds!.Contains(a.IdUsuario)).ToList() : aud;

                var ultHora = DateTime.Now.AddHours(-1);
                var emailsErrorReciente = emails.Count(e => e.Estado == "Error" && e.FecCrea >= ultHora);
                var pwdExpiradas = histPwdF.Count(h => h.EsActual && h.FecExpira < DateTime.Now);

                return Result<DashboardEstadoGeneralDto>.Success(new()
                {
                    Usuarios = new() { Estado = bloqueosF.Any(b => b.Activo) ? "yellow" : "green", Mensaje = bloqueosF.Any(b => b.Activo) ? $"{bloqueosF.Count(b => b.Activo)} bloqueos" : "OK" },
                    Email = new() { Estado = emailsErrorReciente > 0 ? (emailsErrorReciente > 5 ? "red" : "yellow") : "green", Mensaje = emailsErrorReciente > 0 ? $"{emailsErrorReciente} errores recientes" : "Disponible" },
                    MFA = new() { Estado = mfasF.Any(m => m.IdEstado == 3) ? "yellow" : "green", Mensaje = mfasF.Any(m => m.IdEstado == 3) ? "Pendientes" : "OK" },
                    Password = new() { Estado = pwdExpiradas > 0 ? (pwdExpiradas > 10 ? "red" : "yellow") : "green", Mensaje = pwdExpiradas > 0 ? $"{pwdExpiradas} expiradas" : "OK" },
                    Auditoria = new() { Estado = audF.Count > 0 ? "green" : "yellow", Mensaje = audF.Count > 0 ? $"{audF.Count} registros" : "Sin datos" },
                    OAuth = new() { Estado = "green", Mensaje = "OK" },
                    Background = new() { Estado = "green", Mensaje = "OK" },
                    Dashboard = new() { Estado = "green", Mensaje = "Activo" },
                    API = new() { Estado = "green", Mensaje = "Operativo" },
                    BaseDatos = new() { Estado = "green", Mensaje = "Conectado", UltimaVerificacion = DateTime.Now.ToString("O") },
                    SMTP = new() { Estado = emails.Count(e => e.Estado == "Error") > 10 ? "red" : "green", Mensaje = "Disponible" }
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
                var users = (await _usuarioRepo.GetAllAsync(true, ct)).Value ?? [];
                var idenExt = (await _idenExtRepo.GetAllAsync(true, ct)).Value ?? [];
                var prov = (await _provIdenRepo.GetAllAsync(true, ct)).Value ?? [];
                var ips = (await _ipRepo.GetAllAsync(true, ct)).Value ?? [];
                var emails = (await _emailLogRepo.GetAllAsync(true, ct)).Value ?? [];
                var aud = (await _audRepo.GetAllAsync(true, ct)).Value ?? [];
                var apps = (await _appRepo.GetAllAsync(true, ct)).Value ?? [];
                var roles = (await _rolRepo.GetAllAsync(true, ct)).Value ?? [];
                var disp = (await _dispRepo.GetAllAsync(true, ct)).Value ?? [];

                HashSet<int>? userIds = null;
                if (idTenant.HasValue)
                    userIds = await ObtenerIdsUsuariosEnTenantAsync(idTenant.Value, ct);

                var usersF = userIds != null ? users.Where(u => userIds!.Contains(u.Id)).ToList() : users;
                var rolesF = idTenant.HasValue && idTenant.Value > 0 ? roles.Where(r => r.IdTenant == idTenant.Value || r.IdTenant == null).ToList() : roles;
                var idenExtF = userIds != null ? idenExt.Where(i => userIds!.Contains(i.IdUsuario)).ToList() : idenExt;

                var activas = idenExtF.Where(i => !i.Eliminado).ToList();
                var totalActivas = activas.Count;
                var emailsConTemplate = emails.Where(e => e.IdTemplate != null).ToList();
                var totalTemplates = emailsConTemplate.Count;

                return Result<DashboardEjecutivoAvanzadoDto>.Success(new()
                {
                    Top10UsuariosActivos = usersF.Where(u => !u.Eliminado).OrderByDescending(u => u.FecMod).Take(10)
                        .Select(u => new CountItemDto { Nombre = u.NomUsuario, Cantidad = 1 }).ToList(),
                    Top10Apps = apps.Take(10).Select(a => new CountItemDto { Nombre = a.Nombre, Cantidad = 1 }).ToList(),
                    Top10Roles = rolesF.Take(10).Select(r => new CountItemDto { Nombre = r.Nombre, Cantidad = 1 }).ToList(),
                    Top10ProveedoresOAuth = activas.GroupBy(i => i.ProvIden != null ? i.ProvIden.Nombre : "N/A")
                        .Select(g => new CountItemDto { Nombre = g.Key, Cantidad = g.Count(), Porcentaje = totalActivas > 0 ? Math.Round(g.Count() * 100.0 / totalActivas, 1) : 0 })
                        .OrderByDescending(c => c.Cantidad).Take(10).ToList(),
                    Top10IPs = ips.OrderByDescending(i => i.Id).Take(10).Select(i => new CountItemDto { Nombre = i.Direccion, Cantidad = 1 }).ToList(),
                    Top10Errores = aud.Where(a => a.Detalles != null).GroupBy(a => a.Detalles!.Length > 50 ? a.Detalles![..50] + "..." : a.Detalles!)
                        .Select(g => new CountItemDto { Nombre = g.Key, Cantidad = g.Count() })
                        .OrderByDescending(c => c.Cantidad).Take(10).ToList(),
                    Top10Templates = emailsConTemplate.GroupBy(e => e.IdTemplate!.Value)
                        .Select(g => new CountItemDto { Nombre = g.Key.ToString(), Cantidad = g.Count(), Porcentaje = totalTemplates > 0 ? Math.Round(g.Count() * 100.0 / totalTemplates, 1) : 0 })
                        .OrderByDescending(c => c.Cantidad).Take(10).ToList(),
                    Top10Paises = disp.Where(d => d.Pais != null).GroupBy(d => d.Pais!)
                        .Select(g => new CountItemDto { Nombre = g.Key, Cantidad = g.Count() })
                        .OrderByDescending(c => c.Cantidad).Take(10).ToList()
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

    private async Task<HashSet<int>> ObtenerIdsUsuariosEnTenantAsync(int idTenant, CancellationToken ct)
    {
        var result = await _usuarioTenantRepo.ObtenerIdsUsuariosActivosPorTenantAsync(idTenant, ct);
        if (result.IsFailure || result.Value.Count == 0) return [];
        return [.. result.Value];
    }

    private string CacheKey(string baseKey, int? idTenant) =>
        idTenant.HasValue ? $"{baseKey}_{idTenant.Value}" : baseKey;

    public async Task<Result> InvalidateCacheAsync(CancellationToken ct = default)
    {
        foreach (var key in _cacheKeyBases)
            await _cache.RemoveAsync(key, ct);
        await _cache.RemoveAsync("BackgroundJobs", ct);
        return Result.Success();
    }

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
