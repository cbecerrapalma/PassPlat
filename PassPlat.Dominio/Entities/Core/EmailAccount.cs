namespace PassPlat.Dominio.Entities.Core;

public class EmailAccount
{
    public int Id { get; set; }
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
    public bool Activo { get; set; } = true;
    public int? IdUsrMod { get; set; }
    public DateTime FecCrea { get; set; }
    public DateTime? FecMod { get; set; }

    public EmailProvider? Provider { get; set; }
    public Usuario? Usuario { get; set; }
    public ICollection<EmailLog> EmailLogs { get; set; } = [];
    public ICollection<TenantEmailAccount> TenantEmailAccounts { get; set; } = [];
    public ICollection<AppEmailAccount> AppEmailAccounts { get; set; } = [];

    public static EmailAccount Crear(byte idProvider, string nombre, string host, string smtpUsuario, string password, string fromAddress, int puerto = 587)
    {
        return new EmailAccount
        {
            IdProvider = idProvider,
            Nombre = nombre,
            Host = host,
            Puerto = puerto,
            SmtpUsuario = smtpUsuario,
            Password = password,
            FromAddress = fromAddress,
            UsaTLS = true,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
