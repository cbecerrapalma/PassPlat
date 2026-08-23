using PassPlat.Dominio.Models;

namespace PassPlat.Aplicacion.Dtos.Catalogos;

public sealed class ProvIdenCatalogDto
{
    public required ProvIdenDto Proveedor { get; init; }
    public required OAuthConfigurationStatus Status { get; init; }
}
