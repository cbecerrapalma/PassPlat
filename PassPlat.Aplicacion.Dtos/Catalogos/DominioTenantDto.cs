namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class DominioTenantDto
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public string Dominio { get; set; } = string.Empty;
    public string? TenantNombre { get; set; }
}

public class CrearDominioTenantDto
{
    public int IdTenant { get; set; }
    public string Dominio { get; set; } = string.Empty;
}
