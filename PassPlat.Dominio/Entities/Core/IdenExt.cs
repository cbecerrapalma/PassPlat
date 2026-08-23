namespace PassPlat.Dominio.Entities.Core;

public class IdenExt
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
    public byte[]? AccessToken { get; set; }
    public byte[]? RefreshToken { get; set; }
    public byte[]? IdToken { get; set; }
    public DateTime? TokenExpiration { get; set; }
    public Guid? CorrelationId { get; set; }
    public bool EsPrincipal { get; set; }
    public bool Activo { get; set; } = true;
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
    public DateTime? FecEliminacion { get; set; }
    public int? IdUsuarioElimina { get; set; }
    public DateTime? UltimoLogin { get; set; }
    public DateTime? FecCrea { get; set; }
    public DateTime? FecMod { get; set; }

    public Usuario? Usuario { get; set; }
    public Catalogos.ProvIden? ProvIden { get; set; }
    public Tenant? Tenant { get; set; }
    public Usuario? UsuarioElimina { get; set; }
    public Usuario? UsuarioRevoca { get; set; }
    public Catalogos.EstIdenExt? Estado { get; set; }
    public Contexto.Disp? Dispositivo { get; set; }
    public Tenant? UltimoTenantNav { get; set; }

    public static IdenExt Crear(int idUsuario, int idProvIden, int idTenant, string subExterno, string? emailExterno = null, string? nombreExterno = null, string? avatar = null)
    {
        return new IdenExt
        {
            IdUsuario = idUsuario,
            IdProvIden = idProvIden,
            IdTenant = idTenant,
            SubExterno = subExterno,
            EmailExterno = emailExterno,
            NombreExterno = nombreExterno,
            Avatar = avatar,
            Activo = true,
            Eliminado = false,
            IdEstado = (byte)Enums.EEstIdenExt.Autorizada,
            FecCrea = DateTime.Now
        };
    }
}
