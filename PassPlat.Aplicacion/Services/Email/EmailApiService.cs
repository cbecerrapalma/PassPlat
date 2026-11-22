using CBP.MultiTenant.Abstractions;
using CBP.Results;
using CBP.Results.Errors;
using Microsoft.Extensions.Configuration;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos.Repositories;
using System.Text.Encodings.Web;

namespace PassPlat.Aplicacion.Services.Email;

// S78: servicio de envío de email por plantilla para aplicaciones externas
// - Tenant siempre del JWT (ITenantContext), nunca del cuerpo
// - Solo plantillas publicadas de categorías permitidas (config EmailApi:CategoriasPermitidas, default ["app-externa"])
// - Valores HTML-codificados explícitamente (Fluid no codifica por defecto v2.40)
// - IdApp = null en el job (prioridad: App → Tenant → Global; queremos Tenant/Global)
// - TemplateCode en Extra para override
public interface IEmailApiService
{
    Task<Result<EnviarEmailPlantillaResponse>> EnviarAsync(EnviarEmailPlantillaRequest request, CancellationToken ct = default);
}

public sealed class EmailApiService : IEmailApiService
{
    private readonly ITenantContext _tenantContext;
    private readonly IEmailTemplateRepository _templateRepo;
    private readonly IEmailQueue _emailQueue;
    private readonly IConfiguration _cfg;

    public EmailApiService(
        ITenantContext tenantContext,
        IEmailTemplateRepository templateRepo,
        IEmailQueue emailQueue,
        IConfiguration cfg)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _templateRepo = templateRepo ?? throw new ArgumentNullException(nameof(templateRepo));
        _emailQueue = emailQueue ?? throw new ArgumentNullException(nameof(emailQueue));
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
    }

    public async Task<Result<EnviarEmailPlantillaResponse>> EnviarAsync(EnviarEmailPlantillaRequest request, CancellationToken ct = default)
    {
        // 1. Tenant del JWT (ITenantContext)
        if (!_tenantContext.HasTenant)
            return Result<EnviarEmailPlantillaResponse>.Failure(new Error("TENANT_REQUIRED", "Tenant no resuelto desde JWT", null, 403, ErrorType.Forbidden));

        var idTenant = _tenantContext.CurrentId;

        // 2. Categoría permitida (config, default ["app-externa"])
        var allowedCategories = _cfg.GetSection("EmailApi:CategoriasPermitidas").Get<string[]>() ?? ["app-externa"];

        // 3. Obtener plantilla por código (con fallback tenant → global)
        var templateResult = await _templateRepo.ObtenerPorNombreCulturaAsync(request.CodigoPlantilla, request.Cultura, idTenant, ct);
        if (templateResult.IsFailure || templateResult.Value == null)
            return Result<EnviarEmailPlantillaResponse>.Failure(new Error("EMAIL_PLANTILLA_NO_ENCONTRADA", $"Plantilla no encontrada: {request.CodigoPlantilla}", null, 404, ErrorType.NotFound));

        var template = templateResult.Value;

        // 4. Verificar estado publicado
        if (!string.Equals(template.Estado, "publicado", StringComparison.OrdinalIgnoreCase))
            return Result<EnviarEmailPlantillaResponse>.Failure(new Error("EMAIL_PLANTILLA_NO_PUBLICADA", "La plantilla no está publicada", null, 404, ErrorType.NotFound));

        // 5. Verificar categoría permitida (evita plantillas sensibles como reset pwd, MFA)
        if (!allowedCategories.Contains(template.Categoria, StringComparer.OrdinalIgnoreCase))
            return Result<EnviarEmailPlantillaResponse>.Failure(new Error("EMAIL_PLANTILLA_NO_PERMITIDA", $"Categoría '{template.Categoria}' no permitida para envío por API", null, 403, ErrorType.Forbidden));

        // 6. Codificar valores HTML (Fluid no codifica por defecto v2.40)
        // S62 D3: además se neutralizan { y } (HtmlEncoder no los escapa). El cuerpo se vuelve
        // a analizar al insertarlo en _layout; sin esto, un "{{ ... }}" o "{% ... %}" escrito por
        // el visitante se evaluaría. Como entidades HTML se ven igual en el correo.
        var encoder = HtmlEncoder.Default;
        var safeVariables = new Dictionary<string, object?>();
        if (request.Variables is not null)
        {
            foreach (var kv in request.Variables)
                safeVariables[kv.Key] = NeutralizarLlaves(encoder.Encode(kv.Value ?? ""));
        }

        // 7. Preparar job (IdApp = null para que resuelva Tenant → Global)
        var job = new EmailJob(
            Kind: EmailJobKind.ApiPlantilla,
            ToEmail: request.Destinatario,
            UserName: request.NombreDestinatario ?? "",
            Extra: new Dictionary<string, object?>
            {
                ["TemplateCode"] = request.CodigoPlantilla,
                ["Variables"] = safeVariables
            },
            IdTenant: idTenant,
            IdUsuario: null,
            IdApp: null, // prioridad Tenant → Global (no App)
            CorrelationId: Guid.NewGuid().ToString("N"));

        // 8. Encolar (fallo de infra → 503 ExternalService)
        try
        {
            await _emailQueue.EnqueueAsync(job, ct);
        }
        catch
        {
            return Result<EnviarEmailPlantillaResponse>.Failure(new Error("EMAIL_QUEUE_ERROR", "Error al encolar el email", null, 503, ErrorType.ExternalService));
        }

        return Result<EnviarEmailPlantillaResponse>.Success(new EnviarEmailPlantillaResponse(
            IdEmailLog: null, // EmailLogId se asigna en background
            TrackingId: job.CorrelationId!));
    }

    // S62 D3: { y } como entidades HTML: el motor de plantillas no puede evaluarlas.
    internal static string NeutralizarLlaves(string valor) =>
        valor.Replace("{", "&#123;").Replace("}", "&#125;");

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return "***";
        return email[..1] + new string('*', at - 1) + email[at..];
    }
}