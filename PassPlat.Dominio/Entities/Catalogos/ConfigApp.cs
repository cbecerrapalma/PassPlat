namespace PassPlat.Dominio.Entities.Catalogos;

public class ConfigApp
{
    public int Id { get; set; }
    public int? IdTenant { get; set; }
    public string Grupo { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsEncriptado { get; set; }
    public bool Activo { get; set; } = true;
    public int? IdUsrMod { get; set; }
    public DateTime FecCrea { get; set; }
    public DateTime? FecMod { get; set; }

    public Tenant? Tenant { get; set; }
    public Usuario? Usuario { get; set; }

    public static ConfigApp Crear(string grupo, string clave, string valor, string tipo, string? descripcion = null, int? idTenant = null)
    {
        return new ConfigApp
        {
            Grupo = grupo,
            Clave = clave,
            Valor = valor,
            Tipo = tipo,
            Descripcion = descripcion,
            IdTenant = idTenant,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
