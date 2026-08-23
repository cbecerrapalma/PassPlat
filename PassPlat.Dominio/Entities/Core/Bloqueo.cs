namespace PassPlat.Dominio.Entities.Core;

public class Bloqueo
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdTipoBloqueo { get; set; }
    public int? IdAgente { get; set; }
    public int? IdIP { get; set; }
    public int? IdUsrBloqueador { get; set; }
    public DateTime FecInicio { get; set; } = DateTime.Now;
    public DateTime? FecFin { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string? CodDesbloqueo { get; set; }
    public int? IntentosGenerados { get; set; }
    public string? TipoDeteccion { get; set; }
    public bool Activo { get; set; } = true;

    public Usuario? Usuario { get; set; }
    public Tenant? Tenant { get; set; }
    public TipoBloqueo? TipoBloqueo { get; set; }
    public UserAgent? Agente { get; set; }
    public IP? DireccionIP { get; set; }
    public Usuario? UsrBloqueador { get; set; }

    public static Bloqueo Crear(int idUsuario, int idTenant, int idTipoBloqueo, string motivo, DateTime? fecFin = null)
    {
        return new Bloqueo
        {
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            IdTipoBloqueo = idTipoBloqueo,
            Motivo = motivo,
            FecInicio = DateTime.Now,
            FecFin = fecFin,
            Activo = true
        };
    }

    public void Desactivar()
    {
        Activo = false;
        FecFin ??= DateTime.Now;
    }

    public bool EstaVencido()
    {
        return FecFin.HasValue && FecFin.Value <= DateTime.Now;
    }
}
