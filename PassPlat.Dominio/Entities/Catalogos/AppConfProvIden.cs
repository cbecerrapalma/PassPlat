namespace PassPlat.Dominio.Entities.Catalogos;

/// <summary>S126 · D2 — enlace App ↔ ConfProvIden. Una configuración sin enlaces sirve a todas las apps.</summary>
public class AppConfProvIden
{
    public int Id { get; set; }
    public int IdApp { get; set; }
    public int IdConfProvIden { get; set; }
    public DateTime FecCrea { get; set; } = DateTime.Now;

    public App? App { get; set; }
    public ConfProvIden? ConfProvIden { get; set; }

    public static AppConfProvIden Crear(int idApp)
    {
        return new AppConfProvIden
        {
            IdApp = idApp,
            FecCrea = DateTime.Now
        };
    }
}
