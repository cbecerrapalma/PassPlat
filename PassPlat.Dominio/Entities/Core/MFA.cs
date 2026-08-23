namespace PassPlat.Dominio.Entities.Core;

public class MFA
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdTipoMFA { get; set; }
    public string IdMFA { get; set; } = string.Empty;
    public string? ClavePublica { get; set; }
    public bool EsPrincipal { get; set; }
    public DateTime FecAlta { get; set; } = DateTime.Now;
    public DateTime? UltUso { get; set; }
    public string? Metadatos { get; set; }
    public int IdEstado { get; set; }

    public Usuario? Usuario { get; set; }
    public Tenant? Tenant { get; set; }
    public TipoMFA? TipoMFA { get; set; }
    public EstadoMFA? Estado { get; set; }

    public static MFA Crear(int idUsuario, int idTenant, int idTipoMFA, string idMFA, int idEstado, bool esPrincipal = false)
    {
        return new MFA
        {
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            IdTipoMFA = idTipoMFA,
            IdMFA = idMFA,
            IdEstado = idEstado,
            EsPrincipal = esPrincipal,
            FecAlta = DateTime.Now
        };
    }

    public void RegistrarUso()
    {
        UltUso = DateTime.Now;
    }
}
