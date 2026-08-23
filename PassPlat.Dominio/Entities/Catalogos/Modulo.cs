namespace PassPlat.Dominio.Entities.Catalogos;

public class Modulo
{
    public int Id { get; set; }
    public int? IdModuloPadre { get; set; }
    public int IdTipoModulo { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Ruta { get; set; }
    public string? Icono { get; set; }
    public short Orden { get; set; }
    public bool EsVisibleMenu { get; set; } = true;
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;
    public DateTime? FecMod { get; set; }

    public Modulo? ModuloPadre { get; set; }
    public ICollection<Modulo> SubModulos { get; set; } = [];
    public TipoModulo? TipoModulo { get; set; }
    public ICollection<Permiso> Permisos { get; set; } = [];
    public ICollection<AppModulo> AppsModulos { get; set; } = [];

    public static Modulo Crear(string codigo, string nombre, int idTipoModulo, int? idModuloPadre = null, string? ruta = null, string? icono = null, short orden = 0, bool esVisibleMenu = true)
    {
        return new Modulo
        {
            Codigo = codigo,
            Nombre = nombre,
            IdTipoModulo = idTipoModulo,
            IdModuloPadre = idModuloPadre,
            Ruta = ruta,
            Icono = icono,
            Orden = orden,
            EsVisibleMenu = esVisibleMenu,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }

    public void Desactivar() => Activo = false;
    public void Activar() => Activo = true;
}
