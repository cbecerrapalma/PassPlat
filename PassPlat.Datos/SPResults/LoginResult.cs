namespace PassPlat.Datos.SPResults;

public class LoginResult
{
    /// <summary>
    /// S60 · D4 — Id de la fila 'PwdExpirada' de dbo.ResultadosAcceso (21). El SP lo
    /// devuelve en la columna Resultado cuando la contrasena esta vencida.
    /// </summary>
    public const int IdPwdExpirada = 21;

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
