namespace PassPlat.Aplicacion.Dtos.Core;

public class AuditoriaPwdDto
{
    public long Id { get; set; }
    public int IdUsuario { get; set; }
    public int? IdTenant { get; set; }
    public int? IdApp { get; set; }
    public int IdTipoAccion { get; set; }
    public int? IdUsrEjecutor { get; set; }
    public int? IdDisp { get; set; }
    public int? IdAgente { get; set; }
    public int? IdIP { get; set; }
    public long? IdHistPwd { get; set; }
    public string? Detalles { get; set; }
    public int? NivelRiesgo { get; set; }
    public string? Metadata { get; set; }
    public DateTime? FecAccion { get; set; }
    public string? TipoAccionNombre { get; set; }
    public string? UsuarioNombre { get; set; }
}

public class RegistrarAuditoriaPwdDto
{
    public int IdUsuario { get; set; }
    public int IdTipoAccion { get; set; }
    public int? IdTenant { get; set; }
    public int? IdApp { get; set; }
    public int? IdUsrEjecutor { get; set; }
    public int? IdDisp { get; set; }
    public int? IdAgente { get; set; }
    public int? IdIP { get; set; }
    public long? IdHistPwd { get; set; }
    public string? Detalles { get; set; }
    public int? NivelRiesgo { get; set; }
    public string? Metadata { get; set; }
}
