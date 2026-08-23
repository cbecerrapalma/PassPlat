namespace PassPlat.Aplicacion.Dtos.Core;

public class GrupoUsuarioDto
{
    public int Id { get; set; }
    public int IdGrupo { get; set; }
    public int IdUsuario { get; set; }
    public DateTime FecAlta { get; set; }
    public int? IdUsrMod { get; set; }
    public string? GrupoNombre { get; set; }
    public string? UsuarioNombre { get; set; }
}

public class CrearGrupoUsuarioDto
{
    public int IdGrupo { get; set; }
    public int IdUsuario { get; set; }
}
