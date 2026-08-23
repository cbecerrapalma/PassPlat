namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class ModuloDto
{
    public int Id { get; set; }
    public int? IdModuloPadre { get; set; }
    public int IdTipoModulo { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Ruta { get; set; }
    public string? Icono { get; set; }
    public byte Orden { get; set; }
    public bool EsVisibleMenu { get; set; }
    public bool Activo { get; set; }
    public DateTime FecCrea { get; set; }

    public string? ModuloPadreNombre { get; set; }
    public string? TipoModuloCodigo { get; set; }
    public List<ModuloDto> SubModulos { get; set; } = [];
}

public class CrearModuloDto
{
    public int? IdModuloPadre { get; set; }
    public int IdTipoModulo { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Ruta { get; set; }
    public string? Icono { get; set; }
    public byte Orden { get; set; }
    public bool EsVisibleMenu { get; set; } = true;
}

public class ActualizarModuloDto
{
    public int? IdModuloPadre { get; set; }
    public int IdTipoModulo { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Ruta { get; set; }
    public string? Icono { get; set; }
    public byte Orden { get; set; }
    public bool EsVisibleMenu { get; set; }
    public bool Activo { get; set; }
}
