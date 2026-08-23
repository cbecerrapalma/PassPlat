namespace PassPlat.Dominio.Entities.Core;

public class HistorialPwd
{
    public long Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdPolitica { get; set; }
    public int? IdDisp { get; set; }
    public int? IdTipoCambio { get; set; }
    public string HashPwd { get; set; } = string.Empty;
    public string Algoritmo { get; set; } = "Argon2id";
    public string? ParametrosAlgoritmo { get; set; }
    public byte PepperVersion { get; set; } = 1;
    public bool EsActual { get; set; }
    public bool EsForzado { get; set; }
    public bool EsComprometida { get; set; }
    public byte? Complejidad { get; set; }
    public decimal? Fortaleza { get; set; }
    public string OrigenRegistro { get; set; } = "LOCAL";
    public DateTime? FecRegistro { get; set; }
    public int? AnioMes { get; private set; }
    public DateTime? FecExpira { get; set; }
    public DateTime? FecRetencion { get; private set; }

    public Usuario? Usuario { get; set; }
    public PoliticaPwd? Politica { get; set; }
    public Disp? Disp { get; set; }
    public TipoCambioPwd? TipoCambio { get; set; }
    public ICollection<AuditoriaPwd> AuditoriaPwd { get; set; } = [];

    public static HistorialPwd Crear(int idUsuario, int idPolitica, string hashPwd, byte pepperVersion = 1, bool esForzado = false, string origenRegistro = "LOCAL")
    {
        return new HistorialPwd
        {
            IdUsuario = idUsuario,
            IdPolitica = idPolitica,
            HashPwd = hashPwd,
            PepperVersion = pepperVersion,
            EsActual = true,
            EsForzado = esForzado,
            Algoritmo = "Argon2id",
            OrigenRegistro = origenRegistro,
            FecRegistro = DateTime.Now
        };
    }
}
