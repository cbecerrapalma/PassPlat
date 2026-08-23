namespace PassPlat.Aplicacion.Dtos.Core;

public class AudIdenExtDto
{
    public long Id { get; set; }
    public int IdTenant { get; set; }
    public string? TenantNombre { get; set; }
    public int IdProvIden { get; set; }
    public string? ProvIdenNombre { get; set; }
    public int? IdUsuario { get; set; }
    public string? UsuarioNombre { get; set; }
    public string? SubExterno { get; set; }
    public string Evento { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public string? IP { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime FecEvento { get; set; }

    public string? TraceId { get; set; }
    public Guid? SessionId { get; set; }
    public string? RefreshTokenId { get; set; }
    public string? JwtId { get; set; }
    public int? HttpStatus { get; set; }
    public int? TiempoRespuesta { get; set; }
    public string? Scopes { get; set; }
    public string? MetodoAutenticacion { get; set; }
    public string? TipoLogin { get; set; }
    public string? Origen { get; set; }
    public string? Destino { get; set; }
    public string? Codigo { get; set; }
    public string? Excepcion { get; set; }
    public string? StackResumido { get; set; }
    public int? IdDevice { get; set; }
    public string? Browser { get; set; }
    public string? OS { get; set; }
}

public class AudIdenExtResumenDto
{
    public string? Evento { get; set; }
    public string? Resultado { get; set; }
    public string? MetodoAutenticacion { get; set; }
    public string? Origen { get; set; }
    public string? IP { get; set; }
    public DateTime FecEvento { get; set; }
    public int? HttpStatus { get; set; }
    public int? TiempoRespuesta { get; set; }
    public string? ProvIdenNombre { get; set; }
}
