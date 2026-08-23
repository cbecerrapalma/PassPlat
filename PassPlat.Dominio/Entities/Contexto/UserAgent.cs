using System.Security.Cryptography;
using System.Text;

namespace PassPlat.Dominio.Entities.Contexto;

public class UserAgent
{
    public int Id { get; set; }
    public string Agente { get; set; } = string.Empty;
    public string HashAgente { get; set; } = string.Empty;
    public string? Navegador { get; set; }
    public string? Version { get; set; }
    public string? SistemaOperativo { get; set; }
    public bool? EsMovil { get; set; }
    public DateTime FecPrimerUso { get; set; } = DateTime.Now;
    public DateTime? FecUltUso { get; set; }
    public int VecesUsado { get; set; } = 1;

    public ICollection<TokenRest> TokensRest { get; set; } = [];
    public ICollection<IntentoAcceso> IntentosAcceso { get; set; } = [];
    public ICollection<Bloqueo> Bloqueos { get; set; } = [];
    public ICollection<AuditoriaPwd> AuditoriaPwd { get; set; } = [];
    public ICollection<DispConfiable> DispConfiables { get; set; } = [];

    public static UserAgent Crear(string agente, string? navegador = null, string? version = null, string? sistemaOperativo = null, bool? esMovil = null)
    {
        return new UserAgent
        {
            Agente = agente,
            HashAgente = CalcularHash(agente),
            Navegador = navegador,
            Version = version,
            SistemaOperativo = sistemaOperativo,
            EsMovil = esMovil,
            FecPrimerUso = DateTime.Now,
            VecesUsado = 1
        };
    }

    public static string CalcularHash(string agente)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(agente));
        return Convert.ToHexString(bytes).ToUpperInvariant();
    }
}
