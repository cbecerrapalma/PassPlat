namespace PassPlat.Aplicacion.Dtos.Core;

public class IdenExtDto
{
    public long Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdProvIden { get; set; }
    public int IdTenant { get; set; }
    public string SubExterno { get; set; } = string.Empty;
    public string? ProviderUserName { get; set; }
    public string? EmailExterno { get; set; }
    public string? NombreExterno { get; set; }
    public string? Avatar { get; set; }
    public string? MetadataJson { get; set; }
    public string? ClaimsJson { get; set; }
    public DateTime? TokenExpiration { get; set; }
    public Guid? CorrelationId { get; set; }
    public bool EsPrincipal { get; set; }
    public bool Activo { get; set; }
    public bool Eliminado { get; set; }
    public byte? IdEstado { get; set; }
    public string? Scopes { get; set; }
    public string? UltimaIP { get; set; }
    public int? UltimoDisp { get; set; }
    public string? UltimoUserAgent { get; set; }
    public int? UltimoTenant { get; set; }
    public DateTime? FecRevocacion { get; set; }
    public int? IdUsuarioRevoca { get; set; }
    public string? MotivoRevocacion { get; set; }
    public DateTime? UltimoLogin { get; set; }
    public DateTime? FecCrea { get; set; }
    public DateTime? FecMod { get; set; }
    public string? ProvIdenNombre { get; set; }
    public string? ProvIdenCodigo { get; set; }
    public string? ProvIdenIcono { get; set; }
    public string? UsuarioNombre { get; set; }
    public string? EstadoNombre { get; set; }
    public string? EstadoColor { get; set; }
    public string? DispositivoModelo { get; set; }
    public string? UltimoTenantNombre { get; set; }
    public string? UsuarioRevocaNombre { get; set; }
}

public class CrearIdenExtDto
{
    public int IdUsuario { get; set; }
    public int IdProvIden { get; set; }
    public int IdTenant { get; set; }
    public string SubExterno { get; set; } = string.Empty;
    public string? ProviderUserName { get; set; }
    public string? EmailExterno { get; set; }
    public string? NombreExterno { get; set; }
    public string? Avatar { get; set; }
}
