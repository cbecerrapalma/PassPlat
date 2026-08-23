namespace PassPlat.Aplicacion.Dtos.Core;

public class ProviderAvailabilityResultDto
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public bool Disponible { get; set; }
    public string? Motivo { get; set; }
}
