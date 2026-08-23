using System.ComponentModel.DataAnnotations;

namespace PassPlat.Aplicacion.Dtos.Core;

public class MFADto
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdTipoMFA { get; set; }
    public string IdMFA { get; set; } = string.Empty;
    public string? ClavePublica { get; set; }
    public bool EsPrincipal { get; set; }
    public DateTime FecAlta { get; set; }
    public DateTime? UltUso { get; set; }
    public string? Metadatos { get; set; }
    public int IdEstado { get; set; }
    public string? TipoMFANombre { get; set; }
    public string? EstadoMFANombre { get; set; }
}

public class RegistrarMFADto
{
    [Required] public int IdUsuario { get; set; }
    [Required] public int IdTenant { get; set; }
    [Required] public int IdTipoMFA { get; set; }
    [Required] public int IdEstado { get; set; }
    [Required(AllowEmptyStrings = false)] public string IdMFA { get; set; } = string.Empty;
    public string? ClavePublica { get; set; }
    public bool EsPrincipal { get; set; }
    public string? Metadatos { get; set; }
}
