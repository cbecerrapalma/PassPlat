namespace PassPlat.Dominio.Entities.Catalogos;

public class EmailProvider
{
    public byte Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; }
    public DateTime? FecMod { get; set; }

    public ICollection<EmailAccount> EmailAccounts { get; set; } = [];

    public static EmailProvider Crear(string codigo, string nombre, string? descripcion = null)
    {
        return new EmailProvider
        {
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = descripcion,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
