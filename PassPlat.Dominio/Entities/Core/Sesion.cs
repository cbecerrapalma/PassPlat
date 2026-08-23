namespace PassPlat.Dominio.Entities.Core;

public class Sesion
{
    public Guid Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdApp { get; set; }
    public string IdTokenExt { get; set; } = string.Empty;
    public int? IdDisp { get; set; }
    public int? IdIP { get; set; }
    public string? HashRefresh { get; set; }
    public Guid? IdSesionPadre { get; set; }
    public DateTime FecInicio { get; set; } = DateTime.Now;
    public DateTime UltActividad { get; set; } = DateTime.Now;
    public DateTime FecExpira { get; set; }
    public bool EsActiva { get; set; } = true;

    public Usuario? Usuario { get; set; }
    public Tenant? Tenant { get; set; }
    public App? App { get; set; }
    public Disp? Disp { get; set; }
    public IP? DireccionIP { get; set; }
    public Sesion? SesionPadre { get; set; }
    public ICollection<Sesion> SesionesHijas { get; set; } = [];

    public static Sesion Crear(int idUsuario, int idTenant, int idApp, string idTokenExt, DateTime fecExpira, Guid? idSesionPadre = null)
    {
        return new Sesion
        {
            Id = Guid.NewGuid(),
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            IdApp = idApp,
            IdTokenExt = idTokenExt,
            FecExpira = fecExpira,
            IdSesionPadre = idSesionPadre,
            FecInicio = DateTime.Now,
            UltActividad = DateTime.Now,
            EsActiva = true
        };
    }

    public void Revocar()
    {
        EsActiva = false;
        UltActividad = DateTime.Now;
    }

    public void ActualizarActividad()
    {
        UltActividad = DateTime.Now;
    }
}
