namespace PassPlat.Aplicacion.Dtos.Core;

public class EmailAccountDto
{
    public int Id { get; set; }
    public byte IdProvider { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Puerto { get; set; }
    public string SmtpUsuario { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public bool UsaSSL { get; set; }
    public bool UsaTLS { get; set; }
    public bool EsPredeterminada { get; set; }
    public bool Activo { get; set; }
    public string? ProviderNombre { get; set; }
    public DateTime FecCrea { get; set; }
    public DateTime? FecMod { get; set; }
}

public class CrearEmailAccountDto
{
    public byte IdProvider { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Puerto { get; set; } = 587;
    public string SmtpUsuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public bool UsaSSL { get; set; }
    public bool UsaTLS { get; set; } = true;
    public bool EsPredeterminada { get; set; }
}

public class ActualizarEmailAccountDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Puerto { get; set; } = 587;
    public string SmtpUsuario { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public bool UsaSSL { get; set; }
    public bool UsaTLS { get; set; } = true;
    public bool EsPredeterminada { get; set; }
    public bool Activo { get; set; } = true;
}
