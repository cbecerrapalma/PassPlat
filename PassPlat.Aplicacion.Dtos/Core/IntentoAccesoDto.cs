namespace PassPlat.Aplicacion.Dtos.Core;

public class IntentoAccesoDto
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
    public DateTime FecIntento { get; set; }
    public bool Exitoso { get; set; }
    public int? TpoRespuesta { get; set; }
    public int? CodRespuesta { get; set; }
    public string? ResultadoNombre { get; set; }
    public string? IPDireccion { get; set; }
}

public class RegistrarIntentoAccesoDto
{
    public string NomUsuarioIntentado { get; set; } = string.Empty;
    public int IdResultado { get; set; }
    public bool Exitoso { get; set; }
    public int? IdUsuario { get; set; }
    public int? IdTenant { get; set; }
    public int? IdApp { get; set; }
    public int? IdDisp { get; set; }
    public int? IdAgente { get; set; }
    public int? IdIP { get; set; }
    public string? DetResultado { get; set; }
    public int? TpoRespuesta { get; set; }
    public int? CodRespuesta { get; set; }
    public string MetodoAutenticacion { get; set; } = "Local";
}
