namespace PassPlat.Aplicacion.Dtos.Core;

// ─── Dashboard 1: Ejecutivo ──────────────────────────────────────────────
public class DashboardEjecutivoDto
{
    public int TotalUsuarios { get; set; }
    public int UsuariosActivos { get; set; }
    public int UsuariosBloqueados { get; set; }
    public int UsuariosEliminados { get; set; }
    public int UsuariosLocales { get; set; }
    public int UsuariosExternos { get; set; }
    public int UsuariosMixtos { get; set; }
    public int TotalTenants { get; set; }
    public int TotalApps { get; set; }
    public int TotalRoles { get; set; }
    public int TotalPermisos { get; set; }
    public int SesionesActivas { get; set; }
    public int DispositivosConfiables { get; set; }
    public int IdentidadesExternas { get; set; }
    public int EmailsEnviadosHoy { get; set; }
    public int EmailsFallidos { get; set; }
    public int ColaEmailPendiente { get; set; }
    public int NotificacionesNoLeidas { get; set; }
    public List<BackgroundServiceStatusDto> BackgroundServices { get; set; } = [];
}

public class BackgroundServiceStatusDto
{
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string? UltimaEjecucion { get; set; }
    public string? Estado { get; set; }
}

// ─── Dashboard 2: Seguridad ─────────────────────────────────────────────
public class DashboardSeguridadDto
{
    public int IntentosLoginTotal { get; set; }
    public int LoginsCorrectos { get; set; }
    public int LoginsFallidos { get; set; }
    public int BloqueosActivos { get; set; }
    public int DesbloqueosHoy { get; set; }
    public int MFAHabilitado { get; set; }
    public int MFAPendiente { get; set; }
    public int PasswordsExpiradas { get; set; }
    public int PasswordsProximasVencer { get; set; }
    public int AlertasSeguridad { get; set; }
    public int IPsSospechosas { get; set; }
    public int PaisesDetectados { get; set; }
    public int NuevosDispositivos24h { get; set; }
    public int NuevosProveedoresOAuth24h { get; set; }
    public int UsuariosSinMFA { get; set; }
    public int UsuariosSinEmail { get; set; }
    public int UsuariosHibridos { get; set; }
    public List<TrendItemDto> IntentosUltimos30Dias { get; set; } = [];
}

// ─── Dashboard 3: OAuth ─────────────────────────────────────────────────
public class DashboardOAuthDto
{
    public int UsuariosGoogle { get; set; }
    public int UsuariosGithub { get; set; }
    public int UsuariosLinkedIn { get; set; }
    public int UsuariosFacebook { get; set; }
    public int UsuariosInstagram { get; set; }
    public string ProveedorMasUtilizado { get; set; } = string.Empty;
    public int LoginsOAuthHoy { get; set; }
    public int LoginsOAuthSemana { get; set; }
    public int PrimerLogin { get; set; }
    public int UsuariosAutoProvisionados { get; set; }
    public int UsuariosVinculados { get; set; }
    public int ConsentimientosActivos { get; set; }
    public int ConsentimientosRevocados { get; set; }
    public int ErroresOAuth { get; set; }
    public int ProviderDeshabilitado { get; set; }
    public int ProviderTimeout { get; set; }
    public int ProviderUnavailable { get; set; }
    public int TokenRefreshUsage { get; set; }
    public int RevocacionesTokens { get; set; }
    public int RevocacionesConsent { get; set; }
    public double TasaExito { get; set; }
    public List<ProveedorConteoDto> DesgloseProveedores { get; set; } = [];
    public List<TrendItemDto> OAuthUltimos30Dias { get; set; } = [];
}

// ─── Dashboard 4: Email ──────────────────────────────────────────────────
public class DashboardEmailDto
{
    public int EmailsEnviados { get; set; }
    public int EmailsPendientes { get; set; }
    public int EmailsError { get; set; }
    public int TemplatesUtilizados { get; set; }
    public int TemplatesFallidos { get; set; }
    public int SMTPConectado { get; set; }
    public double TiempoPromedioEnvioMs { get; set; }
    public int ColaEmail { get; set; }
    public int EmailsHoy { get; set; }
    public int EmailsSemana { get; set; }
    public int EmailsMes { get; set; }
    public List<EmailTemplateStatsDto> TopTemplates { get; set; } = [];
}

public class EmailTemplateStatsDto
{
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateNombre { get; set; } = string.Empty;
    public int Enviados { get; set; }
    public int Fallidos { get; set; }
}

// ─── Dashboard 5: Operacional ────────────────────────────────────────────
public class DashboardOperacionalDto
{
    public double CpuPorcentaje { get; set; }
    public double RamMb { get; set; }
    public double TiempoRespuestaApiMs { get; set; }
    public double TiempoRespuestaLoginMs { get; set; }
    public double TiempoSmtpMs { get; set; }
    public double TiempoOAuthMs { get; set; }
    public double TiempoSqlMs { get; set; }
    public List<BackgroundJobDto> BackgroundJobs { get; set; } = [];
    public List<HealthCheckDto> HealthChecks { get; set; } = [];
}

public class BackgroundJobDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime? UltimaEjecucion { get; set; }
    public int ItemsProcesados { get; set; }
}

public class HealthCheckDto
{
    public string Nombre { get; set; } = string.Empty;
    public bool Saludable { get; set; }
    public string? Mensaje { get; set; }
}

// ─── Dashboard 6: Auditoría ──────────────────────────────────────────────
public class DashboardAuditoriaDto
{
    public int EventosHoy { get; set; }
    public int EventosSemana { get; set; }
    public int EventosMes { get; set; }
    public int UsuariosAuditados { get; set; }
    public int CambiosRoles { get; set; }
    public int CambiosPermisos { get; set; }
    public int CambiosPassword { get; set; }
    public int CambiosOAuth { get; set; }
    public int CambiosMFA { get; set; }
    public int CambiosEmail { get; set; }
    public int CambiosTenant { get; set; }
    public int CambiosAplicacion { get; set; }
    public List<TrendItemDto> AuditoriaUltimos30Dias { get; set; } = [];
}

// ─── Dashboard 7: Dispositivos ───────────────────────────────────────────
public class DashboardDispositivosDto
{
    public int DispositivosActivos { get; set; }
    public int DispositivosBloqueados { get; set; }
    public int DispositivosEliminados { get; set; }
    public int DispositivosNuevos24h { get; set; }
    public List<CountItemDto> PorSO { get; set; } = [];
    public List<CountItemDto> PorNavegador { get; set; } = [];
    public List<CountItemDto> PorPais { get; set; } = [];
    public int TotalIPs { get; set; }
    public List<UltimoDispositivoDto> UltimosDispositivos { get; set; } = [];
}

public class UltimoDispositivoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Navegador { get; set; }
    public string? SO { get; set; }
    public string? Pais { get; set; }
    public DateTime FecRegistro { get; set; }
    public bool Confiable { get; set; }
}

// ─── Dashboard 8: Tendencias ─────────────────────────────────────────────
public class DashboardTendenciasDto
{
    public List<TrendItemDto> UsuariosUltimos30Dias { get; set; } = [];
    public List<TrendItemDto> LoginsUltimos30Dias { get; set; } = [];
    public List<TrendItemDto> EmailsUltimos30Dias { get; set; } = [];
    public List<TrendItemDto> OAuthUltimos30Dias { get; set; } = [];
    public List<TrendItemDto> ErroresUltimos30Dias { get; set; } = [];
    public List<TrendItemDto> MFAUltimos30Dias { get; set; } = [];
    public List<TrendItemDto> PasswordsUltimos30Dias { get; set; } = [];
}

public class TrendItemDto
{
    public DateTime Fecha { get; set; }
    public int Cantidad { get; set; }
}

// ─── Dashboard 9: Estado General ─────────────────────────────────────────
public class DashboardEstadoGeneralDto
{
    public ModuloEstadoDto Usuarios { get; set; } = new();
    public ModuloEstadoDto OAuth { get; set; } = new();
    public ModuloEstadoDto Email { get; set; } = new();
    public ModuloEstadoDto MFA { get; set; } = new();
    public ModuloEstadoDto Password { get; set; } = new();
    public ModuloEstadoDto Auditoria { get; set; } = new();
    public ModuloEstadoDto Background { get; set; } = new();
    public ModuloEstadoDto Dashboard { get; set; } = new();
    public ModuloEstadoDto API { get; set; } = new();
    public ModuloEstadoDto BaseDatos { get; set; } = new();
    public ModuloEstadoDto SMTP { get; set; } = new();
}

public class ModuloEstadoDto
{
    public string Modulo { get; set; } = "";
    public string Estado { get; set; } = "green";
    public string? Mensaje { get; set; }
    public string? UltimaVerificacion { get; set; }
}

// ─── Dashboard 10: Ejecutivo Avanzado ────────────────────────────────────
public class DashboardEjecutivoAvanzadoDto
{
    public List<CountItemDto> Top10UsuariosActivos { get; set; } = [];
    public List<CountItemDto> Top10Apps { get; set; } = [];
    public List<CountItemDto> Top10Roles { get; set; } = [];
    public List<CountItemDto> Top10ProveedoresOAuth { get; set; } = [];
    public List<CountItemDto> Top10IPs { get; set; } = [];
    public List<CountItemDto> Top10Errores { get; set; } = [];
    public List<CountItemDto> Top10Templates { get; set; } = [];
    public List<CountItemDto> Top10Paises { get; set; } = [];
}

public class CountItemDto
{
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public double Porcentaje { get; set; }
}

// ─── Filtros globales ────────────────────────────────────────────────────
public class DashboardFiltrosDto
{
    public int? IdTenant { get; set; }
    public int? IdApp { get; set; }
    public string? TenantNombre { get; set; }
    public string? AppNombre { get; set; }
    public int? IdProvIden { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public int? IdUsuario { get; set; }
    public byte? IdEstado { get; set; }
    public byte? IdTipoEvento { get; set; }
    public byte? IdTipoEmail { get; set; }
    public byte? IdTipoAuditoria { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
