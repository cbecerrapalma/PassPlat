namespace PassPlat.Dominio.Entities.Core;

public class TokenRest
{
    public long Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int? IdApp { get; set; }
    public int? IdDisp { get; set; }
    public int? IdAgente { get; set; }
    public int? IdIP { get; set; }
    public string HashToken { get; set; } = string.Empty;
    public DateTime FecGeneracion { get; set; } = DateTime.Now;
    public DateTime FecVence { get; set; }
    public bool EsUtilizado { get; set; }
    public byte IntentosFallidos { get; set; }
    public DateTime? FecUso { get; set; }

    public Usuario? Usuario { get; set; }
    public Tenant? Tenant { get; set; }
    public App? App { get; set; }
    public Disp? Disp { get; set; }
    public UserAgent? Agente { get; set; }
    public IP? DireccionIP { get; set; }

    public static TokenRest Crear(int idUsuario, int idTenant, string hashToken, DateTime fecVence, int? idApp = null)
    {
        return new TokenRest
        {
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            IdApp = idApp,
            HashToken = hashToken,
            FecVence = fecVence,
            FecGeneracion = DateTime.Now,
            EsUtilizado = false,
            IntentosFallidos = 0
        };
    }

    public void MarcarUtilizado()
    {
        EsUtilizado = true;
        FecUso = DateTime.Now;
    }

    public void RegistrarIntentoFallido()
    {
        IntentosFallidos++;
    }

    public bool EstaVencido()
    {
        return FecVence <= DateTime.Now;
    }
}
