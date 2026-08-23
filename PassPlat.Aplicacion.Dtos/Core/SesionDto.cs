namespace PassPlat.Aplicacion.Dtos.Core;

public class SesionDto
{
    public Guid Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdApp { get; set; }
    public string IdTokenExt { get; set; } = string.Empty;
    public int? IdDisp { get; set; }
    public int? IdIP { get; set; }
    public Guid? IdSesionPadre { get; set; }
    public DateTime FecInicio { get; set; }
    public DateTime UltActividad { get; set; }
    public DateTime FecExpira { get; set; }
    public bool EsActiva { get; set; }
    public string? AppNombre { get; set; }
    public string? UsuarioNombre { get; set; }
    public string? IPDireccion { get; set; }
    public string? DispModelo { get; set; }
    // ETAPA 5: campos de contexto de sesión (derivados de Disp)
    public string? Navegador { get; set; }
    public string? SO { get; set; }
    public string? Pais { get; set; }
    public string? ProveedorAuth { get; set; }
}

public class CrearSesionDto
{
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdApp { get; set; }
    public string IdTokenExt { get; set; } = string.Empty;
    public DateTime FecExpira { get; set; }
    public string? HashRefresh { get; set; }
    public int? IdDisp { get; set; }
    public int? IdIP { get; set; }
    public Guid? IdSesionPadre { get; set; }
}
