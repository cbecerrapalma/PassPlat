namespace PassPlat.Aplicacion.Dtos.Core;

public class DispConfiableDto
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdDisp { get; set; }
    public string? Nombre { get; set; }
    public DateTime FecAlta { get; set; }
    public DateTime? UltUso { get; set; }
    public bool Confiable { get; set; }
    public int? IdAgente { get; set; }
    public string? DispModelo { get; set; }
    public string? DispFabricante { get; set; }
    public string? DispTipo { get; set; }
    public string? IP { get; set; }
    public string? Pais { get; set; }
    public string? Navegador { get; set; }
    public string? SO { get; set; }
    public string? ProveedorAuth { get; set; }
    public int CantidadLogins { get; set; }
    public string? UsuarioNombre { get; set; }
}

public class CrearDispConfiableDto
{
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdDisp { get; set; }
    public string? Nombre { get; set; }
    public int? IdAgente { get; set; }
}
