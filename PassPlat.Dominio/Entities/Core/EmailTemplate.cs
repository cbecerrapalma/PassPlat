namespace PassPlat.Dominio.Entities.Core;

public class EmailTemplate
{
    public int Id { get; set; }
    public int? IdTenant { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Cultura { get; set; } = "es";
    public string Asunto { get; set; } = string.Empty;
    public string CuerpoHtml { get; set; } = string.Empty;
    public string? CuerpoTexto { get; set; }
    public string? Descripcion { get; set; }
    public string Categoria { get; set; } = "transaccional";
    public string Estado { get; set; } = "borrador";
    public int Version { get; set; } = 1;
    public string? VariablesDoc { get; set; }
    public int? IdUsrMod { get; set; }
    public DateTime FecCrea { get; set; }
    public DateTime? FecMod { get; set; }

    public Tenant? Tenant { get; set; }
    public Usuario? Usuario { get; set; }
    public ICollection<EmailTemplateHistorial> Historial { get; set; } = [];

    public static EmailTemplate Crear(string nombre, string asunto, string cuerpoHtml, string cultura = "es", string? descripcion = null, string categoria = "transaccional", int? idTenant = null)
    {
        return new EmailTemplate
        {
            Nombre = nombre,
            Cultura = cultura,
            Asunto = asunto,
            CuerpoHtml = cuerpoHtml,
            Descripcion = descripcion,
            Categoria = categoria,
            Estado = "borrador",
            Version = 1,
            IdTenant = idTenant,
            FecCrea = DateTime.Now
        };
    }

    public void Publicar(int idUsrPublico, string? motivo = null)
    {
        Version++;
        Estado = "publicado";
        IdUsrMod = idUsrPublico;
        FecMod = DateTime.Now;
    }

    public void Desactivar(int idUsrMod)
    {
        Estado = "desactivado";
        IdUsrMod = idUsrMod;
        FecMod = DateTime.Now;
    }
}
