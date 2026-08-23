namespace PassPlat.Dominio.Entities.Contexto;

public class Disp
{
    public int Id { get; set; }
    public int IdTipoDisp { get; set; }
    public string? Fabricante { get; set; }
    public string? Modelo { get; set; }
    public DateTime FecPrimerReg { get; set; } = DateTime.Now;
    public DateTime? UltActividad { get; set; }
    public int CantidadLogins { get; set; }
    public string? IP { get; set; }
    public string? Pais { get; set; }
    public string? Navegador { get; set; }
    public string? SO { get; set; }
    public string? ProveedorAuth { get; set; }

    public TipoDisp? TipoDisp { get; set; }
    public ICollection<HistorialPwd> HistorialPwd { get; set; } = [];
    public ICollection<Sesion> Sesiones { get; set; } = [];
    public ICollection<TokenRest> TokensRest { get; set; } = [];
    public ICollection<IntentoAcceso> IntentosAcceso { get; set; } = [];
    public ICollection<AuditoriaPwd> AuditoriaPwd { get; set; } = [];
    public ICollection<DispConfiable> DispConfiables { get; set; } = [];

    public static Disp Crear(int idTipoDisp, string? fabricante = null, string? modelo = null)
    {
        return new Disp
        {
            IdTipoDisp = idTipoDisp,
            Fabricante = fabricante,
            Modelo = modelo,
            FecPrimerReg = DateTime.Now
        };
    }
}
