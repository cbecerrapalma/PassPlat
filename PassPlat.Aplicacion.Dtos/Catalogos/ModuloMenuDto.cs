namespace PassPlat.Aplicacion.Dtos.Catalogos;

/// <summary>
/// S83 · D3 — nodo del menú de una app (<c>GET api/modulos/menu</c>): solo los módulos que el usuario puede ver.
/// </summary>
public class ModuloMenuDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Ruta { get; set; }
    public string? Icono { get; set; }
    public short Orden { get; set; }
    public bool EsVisibleMenu { get; set; }
    public List<ModuloMenuDto> Hijos { get; set; } = [];
}
