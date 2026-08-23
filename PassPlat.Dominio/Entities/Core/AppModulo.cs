using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Dominio.Entities.Core;

public class AppModulo
{
    public int Id { get; set; }
    public int IdApp { get; set; }
    public int IdModulo { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;

    public App? App { get; set; }
    public Modulo? Modulo { get; set; }

    public static AppModulo Crear(int idApp, int idModulo)
    {
        return new AppModulo
        {
            IdApp = idApp,
            IdModulo = idModulo,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }

    public void Desactivar() => Activo = false;
}
