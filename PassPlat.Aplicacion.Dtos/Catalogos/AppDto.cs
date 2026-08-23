namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class AppDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? UrlBase { get; set; }
    public bool Activa { get; set; }
    public DateTime FecCrea { get; set; }
}

public class CrearAppDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? UrlBase { get; set; }
}
