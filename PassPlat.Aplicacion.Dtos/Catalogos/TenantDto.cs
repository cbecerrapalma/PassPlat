namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class TenantDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public bool EsSistema { get; set; }
    public DateTime FecCrea { get; set; }
}

public class CrearTenantDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}
