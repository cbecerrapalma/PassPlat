using System.Reflection;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Contexto;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos;

public class PassPlatDbContext : DbContext
{
    public PassPlatDbContext(DbContextOptions<PassPlatDbContext> options)
        : base(options)
    {
    }

    // Catalogos
    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<ConfigApp> ConfigApp { get; set; } = null!;
    public DbSet<EmailProvider> EmailProviders { get; set; } = null!;
    public DbSet<TipAsigPermiso> TipAsigPermiso { get; set; } = null!;
    public DbSet<RolesHerencia> RolesHerencia { get; set; } = null!;
    public DbSet<Grupo> Grupos { get; set; } = null!;
    public DbSet<ConfigTenant> ConfigTenants { get; set; } = null!;
    public DbSet<DominioTenant> DominiosTenant { get; set; } = null!;
    public DbSet<App> Apps { get; set; } = null!;
    public DbSet<EstadoUsr> EstadosUsr { get; set; } = null!;
    public DbSet<Rol> Roles { get; set; } = null!;
    public DbSet<ResultadoAcceso> ResultadosAcceso { get; set; } = null!;
    public DbSet<TipoMFA> TiposMFA { get; set; } = null!;
    public DbSet<EstadoMFA> EstadosMFA { get; set; } = null!;
    public DbSet<TipoDisp> TiposDisp { get; set; } = null!;
    public DbSet<TipoCambioPwd> TiposCambioPwd { get; set; } = null!;
    public DbSet<TipoBloqueo> TiposBloqueo { get; set; } = null!;
    public DbSet<TipoAuditoria> TiposAuditoria { get; set; } = null!;
    public DbSet<EstIdenExt> EstIdenExt { get; set; } = null!;
    public DbSet<HistorialIdenExt> HistorialIdenExt { get; set; } = null!;

    // Contexto
    public DbSet<Disp> Disp { get; set; } = null!;
    public DbSet<IP> IPs { get; set; } = null!;
    public DbSet<UserAgent> UserAgents { get; set; } = null!;

    // Core
    public DbSet<EmailAccount> EmailAccounts { get; set; } = null!;
    public DbSet<TenantEmailAccount> TenantEmailAccounts { get; set; } = null!;
    public DbSet<AppEmailAccount> AppEmailAccounts { get; set; } = null!;
    public DbSet<EmailLog> EmailLog { get; set; } = null!;
    public DbSet<GrupoUsuario> GruposUsuarios { get; set; } = null!;
    public DbSet<UsuarioPermiso> UsuariosPermisos { get; set; } = null!;
    public DbSet<Usuario> Usuarios { get; set; } = null!;
    public DbSet<Acceso> Accesos { get; set; } = null!;
    public DbSet<UsuarioTenant> UsuarioTenants => Set<UsuarioTenant>();
    public DbSet<PoliticaPwd> PoliticasPwd { get; set; } = null!;
    public DbSet<RolPoliticaPwd> RolesPoliticasPwd { get; set; } = null!;
    public DbSet<HistorialPwd> HistorialPwd { get; set; } = null!;
    public DbSet<Sesion> Sesiones { get; set; } = null!;
    public DbSet<TokenRest> TokensRest { get; set; } = null!;
    public DbSet<IntentoAcceso> IntentosAcceso { get; set; } = null!;
    public DbSet<Bloqueo> Bloqueos { get; set; } = null!;
    public DbSet<MFA> MFA { get; set; } = null!;
    public DbSet<AuditoriaPwd> AuditoriaPwd { get; set; } = null!;
    public DbSet<DispConfiable> DispConfiables { get; set; } = null!;
    public DbSet<Notificacion> Notificaciones { get; set; } = null!;
    public DbSet<EmailTemplate> EmailTemplates { get; set; } = null!;
    public DbSet<EmailTemplatePartial> EmailTemplatePartials { get; set; } = null!;
    public DbSet<EmailTemplateHistorial> EmailTemplateHistorial { get; set; } = null!;
    public DbSet<Permiso> Permisos { get; set; } = null!;
    public DbSet<RolPermiso> RolesPermisos { get; set; } = null!;
    public DbSet<Outbox> Outbox { get; set; } = null!;
    public DbSet<TipoModulo> TiposModulo { get; set; } = null!;
    public DbSet<Modulo> Modulos { get; set; } = null!;
    public DbSet<AppModulo> AppsModulos { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
