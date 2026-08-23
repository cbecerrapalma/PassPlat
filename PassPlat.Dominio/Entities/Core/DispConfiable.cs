namespace PassPlat.Dominio.Entities.Core;

public class DispConfiable
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdDisp { get; set; }
    public string? Nombre { get; set; }
    public DateTime FecAlta { get; set; } = DateTime.Now;
    public DateTime? UltUso { get; set; }
    public bool Confiable { get; set; }
    public int? IdAgente { get; set; }

    public Usuario? Usuario { get; set; }
    public Tenant? Tenant { get; set; }
    public Disp? Disp { get; set; }
    public UserAgent? Agente { get; set; }

    public static DispConfiable Crear(int idUsuario, int idTenant, int idDisp, string? nombre = null)
    {
        return new DispConfiable
        {
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            IdDisp = idDisp,
            Nombre = nombre,
            Confiable = true,
            FecAlta = DateTime.Now
        };
    }

    public void RegistrarUso()
    {
        UltUso = DateTime.Now;
    }
}
