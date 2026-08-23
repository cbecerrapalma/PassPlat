namespace PassPlat.Aplicacion.Options;

public class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int PollIntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 100;
    public int MaxRetries { get; set; } = 3;
    public int[] RetryDelayMinutes { get; set; } = [1, 5, 15];
    public int ProcessingTimeoutSeconds { get; set; } = 300;
}
