namespace PassPlat.Dominio.Entities.Core;

public class TenantEmailAccount
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public int IdEmailAccount { get; set; }
    public bool EsPredeterminada { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; }

    public Tenant? Tenant { get; set; }
    public EmailAccount? EmailAccount { get; set; }

    public static TenantEmailAccount Crear(int idTenant, int idEmailAccount, bool esPredeterminada = false)
    {
        return new TenantEmailAccount
        {
            IdTenant = idTenant,
            IdEmailAccount = idEmailAccount,
            EsPredeterminada = esPredeterminada,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
