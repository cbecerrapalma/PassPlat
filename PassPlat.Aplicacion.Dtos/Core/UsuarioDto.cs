namespace PassPlat.Aplicacion.Dtos.Core;

public class UsuarioDto
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public int IdEstado { get; set; }
    public string NomUsuario { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailVerificado { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public bool ReqCambioPwd { get; set; }
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
    public string? TenantNombre { get; set; }
    public string? EstadoNombre { get; set; }
}

public class CrearUsuarioDto
{
    public int IdTenant { get; set; }
    public int IdEstado { get; set; }
    public int IdApp { get; set; }
    public string NomUsuario { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Password { get; set; }
}

public class ActualizarUsuarioDto
{
    public int Id { get; set; }
    public int? IdEstado { get; set; }
    public string? Nombre { get; set; }
    public string? Apellido { get; set; }
    public string? Email { get; set; }
    public bool? EmailVerificado { get; set; }
}
