namespace PassPlat.Datos.SPResults;

public class MatrizPermisosResult
{
    public int IdRol { get; set; }
    public string RolNombre { get; set; } = string.Empty;
    public string RolCodigo { get; set; } = string.Empty;
    public int IdPermiso { get; set; }
    public string CodigoPermiso { get; set; } = string.Empty;
    public string NombrePermiso { get; set; } = string.Empty;
    public byte OrdenPermiso { get; set; }
    public int IdModulo { get; set; }
    public string NombreModulo { get; set; } = string.Empty;
    public string CodigoModulo { get; set; } = string.Empty;
    public byte OrdenModulo { get; set; }
    public string Estado { get; set; } = "off";
    public bool EsDirecto { get; set; }
    public bool EsHeredado { get; set; }
}
