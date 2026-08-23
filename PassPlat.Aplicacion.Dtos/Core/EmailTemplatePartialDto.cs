namespace PassPlat.Aplicacion.Dtos.Core;

public class EmailTemplatePartialDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string CuerpoHtml { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; }
    public DateTime? FecMod { get; set; }
}

public class CrearEmailTemplatePartialDto
{
    public string Nombre { get; set; } = string.Empty;
    public string CuerpoHtml { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}

public class ActualizarEmailTemplatePartialDto
{
    public int Id { get; set; }
    public string? CuerpoHtml { get; set; }
    public string? Descripcion { get; set; }
    public bool? Activo { get; set; }
}
