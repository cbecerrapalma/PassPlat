namespace PassPlat.Client.Models;

/// <summary>S127 D1 — proveedor externo que la App puede mostrar en su login (<c>GET api/auth/externo/proveedores-login</c>).</summary>
public sealed class ProveedorLoginExternoDto
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Icono { get; set; } = "";
    public string? Color { get; set; }
    public string? Tooltip { get; set; }
    public int OrdenVisual { get; set; }
    public bool EsDePlataforma { get; set; }
}

/// <summary>S127 D1 — sesión que entrega PassPlat al canjear el código de un solo uso (<c>POST api/auth/externo/canje</c>).</summary>
public sealed class SesionLoginExternoDto
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public string NomUsuario { get; set; } = "";
    public string Email { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public bool ReqCambioPwd { get; set; }
}

/// <summary>S127 D1 — códigos de error propios del SDK (los de PassPlat llegan tal cual).</summary>
public static class CodigosLoginExterno
{
    public const string ArgumentoInvalido = "ARGUMENTO_INVALIDO";
    public const string NoDisponible = "PASSPLAT_NO_DISPONIBLE";
    public const string RespuestaInvalida = "PASSPLAT_RESPUESTA_INVALIDA";
}
