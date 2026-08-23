namespace PassPlat.Dominio.Entities.Catalogos;

public class DominioTenant
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public string Dominio { get; set; } = string.Empty;

    public Tenant? Tenant { get; set; }

    public static DominioTenant Crear(int idTenant, string dominio)
    {
        return new DominioTenant
        {
            IdTenant = idTenant,
            Dominio = dominio
        };
    }
}
