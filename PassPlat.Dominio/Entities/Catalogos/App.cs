namespace PassPlat.Dominio.Entities.Catalogos;

public class App
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? UrlBase { get; set; }
    public bool Activa { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;

    public ICollection<Acceso> Accesos { get; set; } = [];
    public ICollection<PoliticaPwd> PoliticasPwd { get; set; } = [];
    public ICollection<Sesion> Sesiones { get; set; } = [];
    public ICollection<TokenRest> TokensRest { get; set; } = [];
    public ICollection<IntentoAcceso> IntentosAcceso { get; set; } = [];
    public ICollection<AuditoriaPwd> AuditoriaPwd { get; set; } = [];
    public ICollection<AppModulo> AppsModulos { get; set; } = [];
    public ICollection<AppEmailAccount> AppEmailAccounts { get; set; } = [];
    public ICollection<EmailLog> EmailLogs { get; set; } = [];

    public static App Crear(string codigo, string nombre, string? urlBase = null)
    {
        return new App
        {
            Codigo = codigo,
            Nombre = nombre,
            UrlBase = urlBase,
            Activa = true,
            FecCrea = DateTime.Now
        };
    }

    public void Desactivar()
    {
        Activa = false;
    }
}
