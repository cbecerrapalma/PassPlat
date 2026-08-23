namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class GrupoDto
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
    public DateTime FecCrea { get; set; }
    public string? TenantNombre { get; set; }
}

public class CrearGrupoDto
{
    public int IdTenant { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}

public class ActualizarGrupoDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
}
