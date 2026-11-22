namespace PassPlat.Datos.SPResults;

/// <summary>
/// S60 - D10: fila unica de dbo.SP_Pwd_EstadoVencimiento. La regla es la misma que
/// usa SP_Auth_Login (vigencia efectiva y FecExpira/FecRegistro del historial actual).
/// </summary>
public class PwdEstadoVencimientoResult
{
    public bool PwdExpirada { get; set; }
}
