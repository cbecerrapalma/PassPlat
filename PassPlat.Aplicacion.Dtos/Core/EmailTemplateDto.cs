namespace PassPlat.Aplicacion.Dtos.Core;

public class EmailTemplateDto
{
    public int Id { get; set; }
    public int? IdTenant { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Cultura { get; set; } = "es";
    public string Asunto { get; set; } = string.Empty;
    public string CuerpoHtml { get; set; } = string.Empty;
    public string? CuerpoTexto { get; set; }
    public string? Descripcion { get; set; }
    public string Categoria { get; set; } = "transaccional";
    public string Estado { get; set; } = "borrador";
    public int Version { get; set; } = 1;
    public string? VariablesDoc { get; set; }
    public string? TenantNombre { get; set; }
    public int? IdUsrMod { get; set; }
    public DateTime FecCrea { get; set; }
    public DateTime? FecMod { get; set; }
}

public class CrearEmailTemplateDto
{
    public int? IdTenant { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Cultura { get; set; } = "es";
    public string Asunto { get; set; } = string.Empty;
    public string CuerpoHtml { get; set; } = string.Empty;
    public string? CuerpoTexto { get; set; }
    public string? Descripcion { get; set; }
    public string Categoria { get; set; } = "transaccional";
}

public class ActualizarEmailTemplateDto
{
    public int Id { get; set; }
    public string? Asunto { get; set; }
    public string? CuerpoHtml { get; set; }
    public string? CuerpoTexto { get; set; }
    public string? Descripcion { get; set; }
    public string? Categoria { get; set; }
    public string? Estado { get; set; }
    public string? VariablesDoc { get; set; }
}

public class PreviewTemplateDto
{
    public string TemplateCode { get; set; } = string.Empty;
    public string? CuerpoHtml { get; set; }
    public string? Asunto { get; set; }
    public Dictionary<string, object> Variables { get; set; } = [];
}

public class PreviewTemplateResultDto
{
    public string Asunto { get; set; } = string.Empty;
    public string CuerpoHtml { get; set; } = string.Empty;
    public bool InlineCss { get; set; }
    public string? Layout { get; set; }
}

public class TestEmailDto
{
    public string TemplateCode { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = [];
}

public class PublicarTemplateDto
{
    public int Id { get; set; }
    public string? Motivo { get; set; }
}
