namespace PassPlat.Dominio.Entities.Catalogos;

public class TipoMFA
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public byte Prioridad { get; set; }
    public bool ReqConfig { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;

    public ICollection<MFA> MFA { get; set; } = [];

    public static TipoMFA Crear(string nombre, byte prioridad, bool reqConfig, string? descripcion = null)
    {
        return new TipoMFA
        {
            Nombre = nombre,
            Prioridad = prioridad,
            ReqConfig = reqConfig,
            Descripcion = descripcion,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
