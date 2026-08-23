namespace PassPlat.Datos.SPResults;

public class LoginResult
{
    public int Resultado { get; set; }
    public string? Mensaje { get; set; }
    public int? IdUsuario { get; set; }
    public int? IdTenant { get; set; }
    public bool EsSistema { get; set; }
    public bool? ReqCambioPwd { get; set; }
    public bool? PwdExpirada { get; set; }
    public bool RequiereReHash { get; set; }
    public int? IdMFAPrincipal { get; set; }
    public int? IdBloqueo { get; set; }
    public DateTime? FecFinBloqueo { get; set; }
    public int? IntentosRestantes { get; set; }
}
