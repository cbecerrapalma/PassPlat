namespace PassPlat.Aplicacion.Dtos.Core;

public class AppEmailAccountDto
{
    public int Id { get; set; }
    public int IdApp { get; set; }
    public int IdEmailAccount { get; set; }
    public bool EsPredeterminada { get; set; }
    public bool Activo { get; set; }
    public string? EmailAccountNombre { get; set; }
    public string? AppNombre { get; set; }
}

public class CrearAppEmailAccountDto
{
    public int IdApp { get; set; }
    public int IdEmailAccount { get; set; }
    public bool EsPredeterminada { get; set; }
}
