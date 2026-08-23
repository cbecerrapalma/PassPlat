namespace PassPlat.Dominio.Entities.Contexto;

public class IP
{
    public int Id { get; set; }
    public string Direccion { get; set; } = string.Empty;
    public byte TipoIP { get; set; }
    public string? Pais { get; set; }
    public string? Ciudad { get; set; }
    public bool EsSospechosa { get; set; }
    public DateTime FecPrimerUso { get; set; } = DateTime.Now;
    public DateTime? UltUso { get; set; }

    public ICollection<Sesion> Sesiones { get; set; } = [];
    public ICollection<TokenRest> TokensRest { get; set; } = [];
    public ICollection<IntentoAcceso> IntentosAcceso { get; set; } = [];
    public ICollection<Bloqueo> Bloqueos { get; set; } = [];
    public ICollection<AuditoriaPwd> AuditoriaPwd { get; set; } = [];

    public static IP Crear(string direccion, byte tipoIP = 4, string? pais = null, string? ciudad = null)
    {
        return new IP
        {
            Direccion = direccion,
            TipoIP = tipoIP,
            Pais = pais,
            Ciudad = ciudad,
            EsSospechosa = false,
            FecPrimerUso = DateTime.Now
        };
    }
}
