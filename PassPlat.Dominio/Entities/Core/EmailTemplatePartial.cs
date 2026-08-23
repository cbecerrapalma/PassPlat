namespace PassPlat.Dominio.Entities.Core;

public class EmailTemplatePartial
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string CuerpoHtml { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public int? IdUsrMod { get; set; }
    public DateTime FecCrea { get; set; }
    public DateTime? FecMod { get; set; }

    public Usuario? Usuario { get; set; }

    public static EmailTemplatePartial Crear(string nombre, string cuerpoHtml, string? descripcion = null)
    {
        return new EmailTemplatePartial
        {
            Nombre = nombre,
            CuerpoHtml = cuerpoHtml,
            Descripcion = descripcion,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
