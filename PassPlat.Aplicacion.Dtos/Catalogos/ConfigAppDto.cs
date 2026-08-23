namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class ConfigAppDto
{
    public int Id { get; set; }
    public int? IdTenant { get; set; }
    public string Grupo { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsEncriptado { get; set; }
    public bool Activo { get; set; }
    public int? IdUsrMod { get; set; }
    public DateTime FecCrea { get; set; }
    public DateTime? FecMod { get; set; }
    public string? TenantNombre { get; set; }
}

public class CrearConfigAppDto
{
    public int? IdTenant { get; set; }
    public string Grupo { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsEncriptado { get; set; }
}

public class ActualizarConfigAppDto
{
    public string Valor { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
    public bool EsEncriptado { get; set; }
}
