namespace PassPlat.Aplicacion.Options;

public class OAuthMaintenanceOptions
{
    public const string SectionName = "OAuthMaintenance";

    public int JwksCacheTtlMinutes { get; set; } = 60;
    public int JwksRefreshMinutes { get; set; } = 45;
    public int JwksStaleMaxAgeHours { get; set; } = 24;
    public int JwksHttpTimeoutSeconds { get; set; } = 10;
    public bool TokenRotationEnabled { get; set; } = true;
    public int TokenRotationIntervalHours { get; set; } = 168;
    public string TokenStorageProvider { get; set; } = "Memory";
}
