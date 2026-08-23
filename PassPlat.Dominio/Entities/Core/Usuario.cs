namespace PassPlat.Dominio.Entities.Core;

public class Usuario
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public int IdEstado { get; set; }
    public string NomUsuario { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailVerificado { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public bool ReqCambioPwd { get; set; } = true;
    public byte IntentosFallidos { get; set; }
    public DateTime? FecUltIntentoFallido { get; set; }
    public DateTime? FecUltCambioPwd { get; set; }
    public DateTime? FecVerifBrecha { get; set; }
    public bool EsSistema { get; set; }
    public bool TienePasswordLocal { get; set; }
    public bool Eliminado { get; set; }
    public DateTime? FecEliminacion { get; set; }
    public DateTime? FecCrea { get; set; }
    public DateTime? FecMod { get; set; }

    public Tenant? Tenant { get; set; }
    public EstadoUsr? Estado { get; set; }
    public ICollection<Acceso> Accesos { get; set; } = [];
    public ICollection<UsuarioTenant> UsuarioTenants { get; set; } = [];
    public ICollection<HistorialPwd> HistorialPwd { get; set; } = [];
    public ICollection<Sesion> Sesiones { get; set; } = [];
    public ICollection<TokenRest> TokensRest { get; set; } = [];
    public ICollection<IntentoAcceso> IntentosAcceso { get; set; } = [];
    public ICollection<Bloqueo> Bloqueos { get; set; } = [];
    public ICollection<Bloqueo> BloqueosRealizados { get; set; } = [];
    public ICollection<MFA> MFA { get; set; } = [];
    public ICollection<AuditoriaPwd> AuditoriaPwd { get; set; } = [];
    public ICollection<AuditoriaPwd> AuditoriaEjecutada { get; set; } = [];
    public ICollection<RolPoliticaPwd> RolesPoliticasPwdModificadas { get; set; } = [];
    public ICollection<RolPermiso> RolesPermisosModificados { get; set; } = [];
    public ICollection<DispConfiable> DispConfiables { get; set; } = [];
    public ICollection<Notificacion> Notificaciones { get; set; } = [];
    public ICollection<UsuarioPermiso> UsuariosPermisos { get; set; } = [];
    public ICollection<UsuarioPermiso> UsuariosPermisosCreados { get; set; } = [];
    public ICollection<UsuarioPermiso> UsuariosPermisosModificados { get; set; } = [];
    public ICollection<GrupoUsuario> GruposUsuarios { get; set; } = [];
    public ICollection<GrupoUsuario> GruposUsuariosModificados { get; set; } = [];
    public ICollection<EmailLog> EmailLogs { get; set; } = [];
    public ICollection<IdenExt> IdenExt { get; set; } = [];
    public ICollection<AudIdenExt> AuditoriasIdenExt { get; set; } = [];

    public static Usuario Crear(int idTenant, int idEstado, string nomUsuario, string? email, string nombre, string apellido)
    {
        return new Usuario
        {
            IdTenant = idTenant,
            IdEstado = idEstado,
            NomUsuario = nomUsuario,
            Email = email,
            Nombre = nombre,
            Apellido = apellido,
            ReqCambioPwd = true,
            IntentosFallidos = 0,
            Eliminado = false,
            FecCrea = DateTime.Now,
            FecMod = DateTime.Now
        };
    }

    public bool TieneEmail => !string.IsNullOrWhiteSpace(Email);

    public void RegistrarIntentoFallido()
    {
        if (IntentosFallidos < byte.MaxValue) IntentosFallidos++;
        FecUltIntentoFallido = DateTime.Now;
    }

    public void LimpiarIntentosFallidos()
    {
        IntentosFallidos = 0;
        FecUltIntentoFallido = null;
    }

    public void MarcarEliminado()
    {
        Eliminado = true;
        FecEliminacion = DateTime.Now;
        FecMod = DateTime.Now;
    }

    public void SolicitarCambioPassword()
    {
        ReqCambioPwd = true;
        FecMod = DateTime.Now;
    }

    public void ConfirmarCambioPassword()
    {
        ReqCambioPwd = false;
        FecUltCambioPwd = DateTime.Now;
        FecMod = DateTime.Now;
    }

    public void VerificarEmail()
    {
        EmailVerificado = true;
        FecMod = DateTime.Now;
    }
}
