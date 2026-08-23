namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class PermisoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int IdModulo { get; set; }
    public byte Orden { get; set; }
    public bool Activo { get; set; }
    public DateTime FecCrea { get; set; }
    public string? ModuloNombre { get; set; }
    public string? ModuloCodigo { get; set; }
}

public class CrearPermisoDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int IdModulo { get; set; }
    public byte Orden { get; set; }
}

public class ActualizarPermisoDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int IdModulo { get; set; }
    public byte Orden { get; set; }
}
