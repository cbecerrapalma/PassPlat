namespace PassPlat.Aplicacion.Dtos.Core;

public class RolPoliticaPwdDto
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public int IdRol { get; set; }
    public int IdPolitica { get; set; }
    public bool Activo { get; set; }
    public DateTime FecCrea { get; set; }
    public DateTime FecMod { get; set; }
    public int? IdUsrMod { get; set; }
    public string? RolNombre { get; set; }
    public string? PoliticaNombre { get; set; }
}

public class CrearRolPoliticaPwdDto
{
    public int IdTenant { get; set; }
    public int IdRol { get; set; }
    public int IdPolitica { get; set; }
}
