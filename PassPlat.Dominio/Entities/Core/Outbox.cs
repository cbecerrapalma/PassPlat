namespace PassPlat.Dominio.Entities.Core;

public class Outbox
{
    public long Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public int? IdTenant { get; private set; }
    public int? IdUsuario { get; private set; }
    public string Status { get; private set; } = "pending";
    public int Attempts { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ProcessingStartedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }

    public Tenant? Tenant { get; set; }
    public Usuario? Usuario { get; set; }

    public static Outbox Crear(string eventType, string payload, string correlationId,
        int? idTenant = null, int? idUsuario = null)
    {
        return new Outbox
        {
            EventType = eventType,
            Payload = payload,
            CorrelationId = correlationId,
            IdTenant = idTenant,
            IdUsuario = idUsuario,
            Status = "pending",
            Attempts = 0
        };
    }

    public void MarcarProcessing(DateTime processingStartedAt)
    {
        Status = "processing";
        ProcessingStartedAt = processingStartedAt;
    }

    public void MarcarPublished(DateTime processedAt)
    {
        Status = "published";
        ProcessingStartedAt = null;
        ProcessedAt = processedAt;
        LastError = null;
        NextAttemptAt = null;
    }

    public void MarcarFailed(string error, DateTime nextAttempt, int attempts)
    {
        Status = "failed";
        LastError = error;
        NextAttemptAt = nextAttempt;
        Attempts = attempts;
    }

    public void Reprogramar(DateTime nextAttempt, int attempts)
    {
        Status = "pending";
        LastError = null;
        NextAttemptAt = nextAttempt;
        Attempts = attempts;
    }

    public void ResetStale()
    {
        Status = "pending";
        ProcessingStartedAt = null;
    }
}
