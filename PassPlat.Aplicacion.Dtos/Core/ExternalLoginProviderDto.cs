namespace PassPlat.Aplicacion.Dtos.Core;

public class ExternalLoginProviderDto
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Icono { get; set; } = "";
    public string? Color { get; set; }
    public string? Tooltip { get; set; }
    public int OrdenVisual { get; set; }
    public bool EsDePlataforma { get; set; }
}
