namespace PassPlat.Dominio.Entities.Core;

public class GrupoUsuario
{
    public int Id { get; set; }
    public int IdGrupo { get; set; }
    public int IdUsuario { get; set; }
    public DateTime FecAlta { get; set; } = DateTime.Now;
    public int? IdUsrMod { get; set; }

    public Grupo? Grupo { get; set; }
    public Usuario? Usuario { get; set; }
    public Usuario? UsrMod { get; set; }

    public static GrupoUsuario Crear(int idGrupo, int idUsuario, int? idUsrMod = null)
    {
        return new GrupoUsuario
        {
            IdGrupo = idGrupo,
            IdUsuario = idUsuario,
            FecAlta = DateTime.Now,
            IdUsrMod = idUsrMod
        };
    }
}
