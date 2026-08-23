namespace PassPlat.Aplicacion.Options;

public class MfaOptions
{
    public const string SectionName = "Mfa";

    public int TiempoValidezCodigoMFA { get; set; } = 5;
    public int LongitudCodigoMFA { get; set; } = 6;
}
