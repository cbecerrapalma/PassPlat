namespace PassPlat.Dominio.Entities.Core;

public class IntentoAcceso
{
    public long Id { get; set; }
    public int? IdUsuario { get; set; }
    public int? IdTenant { get; set; }
    public int? IdApp { get; set; }
    public int IdResultado { get; set; }
    public int? IdDisp { get; set; }
    public int? IdAgente { get; set; }
    public int? IdIP { get; set; }
    public string? DetResultado { get; set; }
    public string NomUsuarioIntentado { get; set; } = string.Empty;
    public string MetodoAutenticacion { get; set; } = "Local";
    public DateTime FecIntento { get; set; } = DateTime.Now;
    public bool Exitoso { get; set; }
    public int? TpoRespuesta { get; set; }
    public int? CodRespuesta { get; set; }
    public DateTime? FecRetencion { get; private set; }

    public Usuario? Usuario { get; set; }
    public Tenant? Tenant { get; set; }
    public App? App { get; set; }
    public ResultadoAcceso? Resultado { get; set; }
    public Disp? Disp { get; set; }
    public UserAgent? Agente { get; set; }
    public IP? DireccionIP { get; set; }

    public static IntentoAcceso Crear(
        string nomUsuarioIntentado,
        int idResultado,
        bool exitoso,
        int? idUsuario = null,
        int? idTenant = null,
        int? idApp = null,
        int? idDisp = null,
        int? idAgente = null,
        int? idIP = null,
        string? detResultado = null,
        int? tpoRespuesta = null,
        int? codRespuesta = null,
        string metodoAutenticacion = "Local")
    {
        return new IntentoAcceso
        {
            NomUsuarioIntentado = nomUsuarioIntentado,
            IdResultado = idResultado,
            Exitoso = exitoso,
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            IdApp = idApp,
            IdDisp = idDisp,
            IdAgente = idAgente,
            IdIP = idIP,
            DetResultado = detResultado,
            TpoRespuesta = tpoRespuesta,
            CodRespuesta = codRespuesta,
            MetodoAutenticacion = metodoAutenticacion,
            FecIntento = DateTime.Now
        };
    }

    public void MarcarExitoso()
    {
        Exitoso = true;
    }
}
