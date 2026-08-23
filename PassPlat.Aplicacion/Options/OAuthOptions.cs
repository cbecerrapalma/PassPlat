namespace PassPlat.Aplicacion.Options;

public class OAuthOptions
{
    public const string SectionName = "OAuth";

    public int StateExpirationMinutes { get; set; } = 10;
    public int CodeExpirationMinutes { get; set; } = 10;
    public int SessionExpirationMinutes { get; set; } = 10;
    public int JwtExpirationMinutes { get; set; } = 60;
    public int MaxClockSkewMinutes { get; set; } = 5;
}
