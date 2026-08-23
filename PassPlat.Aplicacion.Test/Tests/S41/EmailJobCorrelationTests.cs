using CBP.Logging;
using CBP.Logging.Interfaces;
using Microsoft.AspNetCore.Http;
using Moq;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Aplicacion.Test.Tests.Framework.S17;

namespace PassPlat.Aplicacion.Test.Tests.S41;

/// <summary>
/// G3 — Contrato S41 (política A+B de CorrelationId resolvida en EmailQueue.EnqueueAsync):
/// 1. Sin HttpContext → fallback local (Guid), NUNCA null.
/// 2. Con HttpContext y job sin CorrelationId → hereda HttpContext.Items[HttpCorrelationIdKey].
/// 3. Con job.CorrelationId explícito → respeta job (precedencia #1).
/// </summary>
public class EmailJobCorrelationTests
{
    private static EmailQueue CrearCola(IHttpContextAccessor? accessor = null)
    {
        return new EmailQueue(
            new CapturingLoggerService(),
            accessor ?? new Mock<IHttpContextAccessor>().Object);
    }

    private static EmailJob EncolarYLeer(EmailQueue cola, EmailJob? job = null)
    {
        cola.EnqueueAsync(job ?? CrearJob()).GetAwaiter().GetResult();
        var reader = cola.ReadAllAsync().GetAsyncEnumerator();
        reader.MoveNextAsync().GetAwaiter().GetResult();
        return reader.Current;
    }

    private static EmailJob CrearJob(string? correlationId = null, EmailJobKind kind = EmailJobKind.SecurityAlert) =>
        new(kind, "destino@test.com", "Usuario",
            new Dictionary<string, object?> { ["AlertMessage"] = "test" },
            CorrelationId: correlationId);

    [Fact]
    public void Enqueue_SinHttpContext_JobPersisteCorrelationIdLocalNoNull()
    {
        var cola = CrearCola();
        var leido = EncolarYLeer(cola);

        Assert.False(string.IsNullOrWhiteSpace(leido.CorrelationId), "Sin HttpContext el job debe tener fallback local (nunca null)");
    }

    [Fact]
    public void Enqueue_ConHttpContext_SinJobCorrelationId_HeredaRequestW3C()
    {
        const string w3c = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
        accessor.Object.HttpContext!.Items[LoggingPropertyNames.HttpCorrelationIdKey] = w3c;

        var cola = CrearCola(accessor.Object);
        var leido = EncolarYLeer(cola, CrearJob(correlationId: null));

        Assert.Equal(w3c, leido.CorrelationId);
    }

    [Fact]
    public void Enqueue_ConJobExplicitYHttpContext_RespetaCorrelationIdDelJob()
    {
        const string explicito = "corr-explicito-job";
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
        accessor.Object.HttpContext!.Items[LoggingPropertyNames.HttpCorrelationIdKey] = "corr-request-diferente";

        var cola = CrearCola(accessor.Object);
        var leido = EncolarYLeer(cola, CrearJob(correlationId: explicito));

        Assert.Equal(explicito, leido.CorrelationId);
    }
}