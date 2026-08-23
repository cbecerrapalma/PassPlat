namespace PassPlat.Dominio.Entities.Catalogos;

public class Tenant
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public bool EsSistema { get; set; }
    public DateTime FecCrea { get; set; } = DateTime.Now;

    public ICollection<ConfigTenant> Configs { get; set; } = [];
    public ICollection<UsuarioTenant> UsuarioTenants { get; set; } = [];
    public ICollection<DominioTenant> Dominios { get; set; } = [];
    public ICollection<Rol> Roles { get; set; } = [];
    public ICollection<Usuario> Usuarios { get; set; } = [];
    public ICollection<Acceso> Accesos { get; set; } = [];
    public ICollection<PoliticaPwd> PoliticasPwd { get; set; } = [];
    public ICollection<RolPoliticaPwd> RolesPoliticasPwd { get; set; } = [];
    public ICollection<Sesion> Sesiones { get; set; } = [];
    public ICollection<TokenRest> TokensRest { get; set; } = [];
    public ICollection<IntentoAcceso> IntentosAcceso { get; set; } = [];
    public ICollection<Bloqueo> Bloqueos { get; set; } = [];
    public ICollection<MFA> MFA { get; set; } = [];
    public ICollection<AuditoriaPwd> AuditoriaPwd { get; set; } = [];
    public ICollection<DispConfiable> DispConfiables { get; set; } = [];
    public ICollection<Notificacion> Notificaciones { get; set; } = [];
    public ICollection<EmailTemplate> EmailTemplates { get; set; } = [];
    public ICollection<TenantEmailAccount> TenantEmailAccounts { get; set; } = [];
    public ICollection<ConfProvIden> ConfProvIden { get; set; } = [];
    public ICollection<IdenExt> IdenExt { get; set; } = [];
    public ICollection<AudIdenExt> AuditoriasIdenExt { get; set; } = [];
    public ICollection<ConfLdap> ConfLdaps { get; set; } = [];
    public ICollection<ConfSaml> ConfSamls { get; set; } = [];
    public ICollection<LdapSyncLog> LdapSyncLogs { get; set; } = [];
    public ICollection<SamlSession> SamlSessions { get; set; } = [];

    public static Tenant Crear(string codigo, string nombre)
    {
        return new Tenant
        {
            Codigo = codigo,
            Nombre = nombre,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }

    public void Desactivar()
    {
        Activo = false;
    }

    public void Activar()
    {
        Activo = true;
    }
}
