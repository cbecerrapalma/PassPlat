namespace PassPlat.Aplicacion.Dtos.Core;

public class HistorialPwdDto
{
    public long Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdPolitica { get; set; }
    public int? IdDisp { get; set; }
    public int? IdTipoCambio { get; set; }
    public string Algoritmo { get; set; } = string.Empty;
    public string? ParametrosAlgoritmo { get; set; }
    public byte PepperVersion { get; set; }
    public bool EsActual { get; set; }
    public bool EsForzado { get; set; }
    public bool EsComprometida { get; set; }
    public byte? Complejidad { get; set; }
    public decimal? Fortaleza { get; set; }
    public DateTime? FecRegistro { get; set; }
    public DateTime? FecExpira { get; set; }
    public string? UsuarioNombre { get; set; }
    public string? PoliticaNombre { get; set; }
}
