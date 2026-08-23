using System.ComponentModel.DataAnnotations;

namespace PassPlat.Aplicacion.Dtos.Core;

public class TokenRestDto
{
    public long Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int? IdApp { get; set; }
    public int? IdDisp { get; set; }
    public int? IdAgente { get; set; }
    public int? IdIP { get; set; }
    public DateTime FecGeneracion { get; set; }
    public DateTime FecVence { get; set; }
    public bool EsUtilizado { get; set; }
    public byte IntentosFallidos { get; set; }
    public DateTime? FecUso { get; set; }
    public string? UsuarioNombre { get; set; }
    public string? TenantNombre { get; set; }
    public string? AppNombre { get; set; }
}

public class GenerarTokenRestDto
{
    [Required] public int IdUsuario { get; set; }
    [Required] public int IdTenant { get; set; }
    [Required] public int IdApp { get; set; }
    [Required] public DateTime FecVence { get; set; }
    public int? IdDisp { get; set; }
    public int? IdAgente { get; set; }
    public int? IdIP { get; set; }
    [Required(AllowEmptyStrings = false)] public string HashToken { get; set; } = string.Empty;
}
