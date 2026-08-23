namespace PassPlat.Dominio.Entities.Core;

public class LdapSyncLog
{
    public long Id { get; set; }
    public int IdTenant { get; set; }
    public int? IdUsuario { get; set; }
    public string Operacion { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public string? LdapUid { get; set; }
    public string? Detalle { get; set; }
    public int? UsuariosCreados { get; set; }
    public int? UsuariosActualizados { get; set; }
    public int? UsuariosDesactivados { get; set; }
    public int? Errores { get; set; }
    public DateTime FecOperacion { get; set; } = DateTime.Now;

    public Tenant? Tenant { get; set; }
    public Usuario? Usuario { get; set; }
}
