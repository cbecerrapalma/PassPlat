namespace PassPlat.Aplicacion.Dtos.Core;

public class BloqueoDto
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdTipoBloqueo { get; set; }
    public int? IdAgente { get; set; }
    public int? IdIP { get; set; }
    public int? IdUsrBloqueador { get; set; }
    public DateTime FecInicio { get; set; }
    public DateTime? FecFin { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string? CodDesbloqueo { get; set; }
    public int? IntentosGenerados { get; set; }
    public string? TipoDeteccion { get; set; }
    public bool Activo { get; set; }
    public string? TipoBloqueoNombre { get; set; }
}

public class CrearBloqueoDto
{
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdTipoBloqueo { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime? FecFin { get; set; }
    public int? IdAgente { get; set; }
    public int? IdIP { get; set; }
    public int? IdUsrBloqueador { get; set; }
    public string? TipoDeteccion { get; set; }
}
