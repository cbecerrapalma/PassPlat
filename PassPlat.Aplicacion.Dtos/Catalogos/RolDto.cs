namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class RolDto
{
    public int Id { get; set; }
    public int? IdTenant { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
    public DateTime FecCrea { get; set; }
    public string? TenantNombre { get; set; }
}

public class CrearRolDto
{
    public int? IdTenant { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int? IdPolitica { get; set; }
    public List<int>? IdsPermisos { get; set; }
}

public class ActualizarRolDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
}
