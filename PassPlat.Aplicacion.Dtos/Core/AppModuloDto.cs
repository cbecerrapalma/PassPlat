namespace PassPlat.Aplicacion.Dtos.Core;

public class AppModuloDto
{
    public int Id { get; set; }
    public int IdApp { get; set; }
    public int IdModulo { get; set; }
    public bool Activo { get; set; }
    public DateTime FecCrea { get; set; }

    public string? AppNombre { get; set; }
    public string? ModuloNombre { get; set; }
    public string? ModuloCodigo { get; set; }
}

public class CrearAppModuloDto
{
    public int IdApp { get; set; }
    public int IdModulo { get; set; }
}
