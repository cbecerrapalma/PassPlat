namespace PassPlat.Dominio.Entities.Core;

public class AppEmailAccount
{
    public int Id { get; set; }
    public int IdApp { get; set; }
    public int IdEmailAccount { get; set; }
    public bool EsPredeterminada { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; }

    public App? App { get; set; }
    public EmailAccount? EmailAccount { get; set; }

    public static AppEmailAccount Crear(int idApp, int idEmailAccount, bool esPredeterminada = false)
    {
        return new AppEmailAccount
        {
            IdApp = idApp,
            IdEmailAccount = idEmailAccount,
            EsPredeterminada = esPredeterminada,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
