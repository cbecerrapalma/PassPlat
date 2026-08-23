using System.Threading.Channels;
using CBP.Logging;
using CBP.Logging.Models;
using CBP.Results;
using Microsoft.AspNetCore.Http;
using PassPlat.Aplicacion.Services.Dashboard;

namespace PassPlat.Aplicacion.Services.Email;

public enum EmailJobKind
{
    PasswordReset,
    MfaCode,
    Welcome,
    SecurityAlert,
    AccountLocked,
    PasswordChanged,
    UserActivated,
    UserDeactivated,
    UserUnblocked,
    PasswordExpired,
    FirstLogin,
    MfaEnabled,
    MfaDisabled,
    NewDevice,
    NewIp,
    DeviceRevoked,
    RoleAssigned,
    RoleRemoved,
    TenantCreated,
    TenantSuspended,
    TenantReactivated,
    AppRegistered,
    ExternalLogin,
    ExternalIdentityLinked,
    ExternalIdentityUnlinked,
    ProviderAdded,
    ProviderRemoved,
    AuthError,
    ProviderPrincipalChanged,
    PasswordLocalAdded,
    PasswordLocalRemoved,
    IdentityPrincipalChanged,
    IdentityLinkedByAdmin,
    IdentityRemovedByAdmin,
    ProviderDisabled,
    ProviderEnabled,
    ProviderAuthorizationRevoked,
    ProviderAuthorizationGranted,
    OAuthConsentExpired,
    SessionRevoked,
    SecurityNotification
}

public sealed record EmailJob(
    EmailJobKind Kind,
    string ToEmail,
    string UserName,
    IReadOnlyDictionary<string, object?> Extra,
    int? IdTenant = null,
    int? IdUsuario = null,
    int? IdApp = null,
    string? CorrelationId = null,
    long? EmailLogId = null);

public interface IEmailQueue
{
    ValueTask EnqueueAsync(EmailJob job, CancellationToken ct = default);
    IAsyncEnumerable<EmailJob> ReadAllAsync(CancellationToken ct = default);
    int Pending { get; }
}

public sealed class EmailQueue : IEmailQueue, IBackgroundJobStatus
{
    private readonly Channel<EmailJob> _channel;
    private readonly CBP.Logging.Interfaces.ILoggerService _olog;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public string Nombre => nameof(EmailQueue);

    // La cola NO es un BackgroundService: no tiene ciclo con arranque/detención
    // ni una última ejecución discreta. Es una fuente de estado operacional del
    // Dashboard: siempre activa mientras el host está en marcha, y su métrica
    // observable es el número de mensajes pendientes en el canal.
    public Task<Result<BackgroundJobStatus>> ObtenerEstadoAsync(CancellationToken ct = default) =>
        Task.FromResult(Result<BackgroundJobStatus>.Success(
            new BackgroundJobStatus(
                Ejecutando: true,
                UltimaEjecucion: null,
                ItemsProcesados: Pending,
                Detalle: "Cola de email - no es BackgroundService")));

    public EmailQueue(CBP.Logging.Interfaces.ILoggerService olog, IHttpContextAccessor httpContextAccessor)
    {
        _olog = olog;
        _httpContextAccessor = httpContextAccessor;
        _channel = Channel.CreateBounded<EmailJob>(new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async ValueTask EnqueueAsync(EmailJob job, CancellationToken ct = default)
    {
        var correlationId = job.CorrelationId
            ?? _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string
            ?? Guid.NewGuid().ToString("N");

        await _channel.Writer.WriteAsync(job with { CorrelationId = correlationId }, ct);
        _olog.LogInformation(new LogEvent
        {
            EventName = LoggingEvents.EmailQueued,
            Scope = LoggingScopes.Email,
            Message = "Email encolado",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.Application,
                [LoggingPropertyNames.Operation] = LoggingOperations.Queue,
                [LoggingPropertyNames.CorrelationId] = correlationId,
                [LoggingPropertyNames.UserId] = job.IdUsuario?.ToString(),
                [LoggingPropertyNames.TenantId] = job.IdTenant,
            }
        });
    }

    public IAsyncEnumerable<EmailJob> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);

    public int Pending => _channel.Reader.Count;
}
