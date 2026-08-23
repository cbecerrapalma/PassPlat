namespace PassPlat.Aplicacion.Dtos.Core;

public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public string NomUsuario { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public bool ReqCambioPwd { get; set; }
    public int? IdMFAPrincipal { get; set; }
    public int? IdTipoMFA { get; set; }
}

public class RefreshTokenRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}
