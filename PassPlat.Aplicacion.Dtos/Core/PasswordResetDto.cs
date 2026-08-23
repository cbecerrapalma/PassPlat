using System.ComponentModel.DataAnnotations;

namespace PassPlat.Aplicacion.Dtos.Core;

public class SolicitarResetPasswordDto
{
    public int IdTenant { get; init; }

    public string? Email { get; init; }

    public string? NomUsuario { get; init; }

    public int? IdApp { get; init; }

    public string? ResetUrl { get; init; }
}

public class PasswordResetResponseDto
{
    public string Mensaje { get; init; } = "Si el correo existe o el usuario está registrado, recibirás instrucciones para restablecer tu contraseña.";
    public bool RequiresEmail { get; init; } = true;
    public string? Message { get; init; }
    public bool RequiresExternalAuth { get; init; }
}

public class RestablecerPasswordDto
{
    [Required(AllowEmptyStrings = false)]
    public string Token { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string NuevaPassword { get; init; } = string.Empty;

    public int? IdApp { get; init; }
}

public class RestablecerPasswordResponseDto
{
    public string Mensaje { get; init; } = "Contraseña restablecida correctamente.";
}
