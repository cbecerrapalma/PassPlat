using AutoMapper;
using CBP.Events;
using CBP.Logging;
using CBP.Logging.Models;
using CBP.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using PassPlat.Aplicacion.Dtos.Contexto;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Aplicacion.Test.Tests.Framework.S17;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Contexto;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Test.Tests.S19;

/// <summary>
/// S19 + S21 — Detección determinista de IP nueva con publicación condicionada a EsNueva.
/// Desde S21 el evento NewIpDetectedEvent se encola vía Outbox (DetectarNuevaIPConOutboxAsync),
/// no se publica inline. Ver Docs/Architecture/S20-Concurrency-Discovery.md.
/// </summary>
public class IPServiceDetectionTests
{
    private const string DirNueva = "203.0.113.7";
    private const string CorrTest = "00-8baa-b1fb-5227b98f44e9-4923161c0c21c8c8";

    private static PassPlatDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PassPlatDbContext(options);
    }

    private static (
        PassPlatDbContext ctx,
        IPRepository repo,
        IPService service,
        Mock<IEventPublisher> publisher,
        CapturingLoggerService logger,
        DefaultHttpContext http) Build(string corre = CorrTest)
    {
        var ctx = CrearContext();
        var repo = new IPRepository(ctx);
        var mapper = new Mock<IMapper>();
        mapper
            .Setup(m => m.Map<IPDto>(It.IsAny<IP>()))
            .Returns((IP ip) => new IPDto
            {
                Id = ip.Id,
                Direccion = ip.Direccion,
                TipoIP = ip.TipoIP,
                Pais = ip.Pais,
                Ciudad = ip.Ciudad,
                EsSospechosa = ip.EsSospechosa,
                FecPrimerUso = ip.FecPrimerUso,
                UltUso = ip.UltUso
            });
        var publisher = new Mock<IEventPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var logger = new CapturingLoggerService();
        var http = new DefaultHttpContext();
        if (!string.IsNullOrEmpty(corre))
            http.Items[LoggingPropertyNames.HttpCorrelationIdKey] = corre;
        var httpAccessor = new Mock<IHttpContextAccessor>();
        httpAccessor.SetupGet(x => x.HttpContext).Returns(http);

        var sesionRepo = new Mock<ISesionRepository>();
        sesionRepo
            .Setup(r => r.WhereAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Sesion, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Sesion>>.Success(new List<Sesion>()));
        var service = new IPService(repo, sesionRepo.Object, mapper.Object, publisher.Object, httpAccessor.Object, logger);
        return (ctx, repo, service, publisher, logger, http);
    }

    private static Outbox ResultadoOutbox(Result<NewIpDetectionResult?> result)
    {
        Assert.True(result.IsSuccess);
        var value = result.Value;
        Assert.NotNull(value);
        Assert.NotNull(value!.Outbox);
        return value.Outbox!;
    }

    // S19-T1 — IP inexistente: crea entidad, EsNueva=true y prepara exactamente 1 Outbox.
    [Fact]
    public async Task T1_IPNueva_CreaYPersisteYPreparaOutboxUnaVez()
    {
        var (ctx, _, service, publisher, _, _) = Build();

        var result = await service.DetectarNuevaIPConOutboxAsync(7, 2, DirNueva, "Agent/1.0", "Device");

        var outbox = ResultadoOutbox(result);
        Assert.Equal("NewIpDetectedEvent", outbox.EventType);
        Assert.Equal(2, outbox.IdTenant);
        Assert.Equal(7, outbox.IdUsuario);

        publisher.Verify(
            p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await ctx.SaveChangesAsync();
        Assert.Equal(1, await ctx.IPs.CountAsync());
        var guardada = await ctx.IPs.FirstAsync();
        Assert.Equal(DirNueva, guardada.Direccion);
        Assert.False(guardada.EsSospechosa);
        Assert.NotNull(guardada.UltUso);
    }

    // S19-T2 — IP existente: no duplica ni prepara Outbox; refresca UltUso.
    [Fact]
    public async Task T2_IPExistente_NoPreparaOutboxYNodDuplica()
    {
        var (ctx, _, service, publisher, _, _) = Build();
        var ip = IP.Crear(DirNueva, 4, "Test", "TestCity");
        await ctx.IPs.AddAsync(ip);
        await ctx.SaveChangesAsync();

        var result = await service.DetectarNuevaIPConOutboxAsync(7, 2, DirNueva);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Null(result.Value!.Outbox);
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(1, await ctx.IPs.CountAsync());
        Assert.NotNull((await ctx.IPs.FirstAsync()).UltUso);
    }

    // S19-T3 — La decisión depende exclusivamente de la existencia real, nunca de FecPrimerUso/UltUso.
    [Fact]
    public async Task T3_TimestampsSonIrrelevantesParaLaDecision()
    {
        // A) IP existente con FecPrimerUso == UltUso -> EsNueva=false, 0 Outbox.
        var (ctxA, _, serviceA, publisherA, _, _) = Build();
        var a = IP.Crear("203.0.113.10", 4);
        var iguales = DateTime.Now;
        a.FecPrimerUso = iguales;
        a.UltUso = iguales;
        await ctxA.IPs.AddAsync(a);
        await ctxA.SaveChangesAsync();

        var resA = await serviceA.DetectarNuevaIPConOutboxAsync(1, 1, "203.0.113.10");
        Assert.True(resA.IsSuccess);
        Assert.Null(resA.Value!.Outbox);
        publisherA.Verify(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()), Times.Never);

        // B) IP existente con FecPrimerUso != UltUso -> EsNueva=false, 0 Outbox.
        var (ctxB, _, serviceB, publisherB, _, _) = Build();
        var b = IP.Crear("203.0.113.11", 4);
        b.FecPrimerUso = DateTime.Now.AddMinutes(-30);
        b.UltUso = DateTime.Now;
        await ctxB.IPs.AddAsync(b);
        await ctxB.SaveChangesAsync();

        var resB = await serviceB.DetectarNuevaIPConOutboxAsync(1, 1, "203.0.113.11");
        Assert.True(resB.IsSuccess);
        Assert.Null(resB.Value!.Outbox);
        publisherB.Verify(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()), Times.Never);

        // C) IP inexistente (primera vez): EsNueva=true aunque se fuercen los timestamps a coincidir.
        var (ctxC, repoC, serviceC, publisherC, _, _) = Build();
        var primera = repoC.ObtenerOCrear("203.0.113.12", 4);
        Assert.True(primera.IsSuccess);
        Assert.True(primera.Value.EsNueva);
        primera.Value.Entidad.UltUso = primera.Value.Entidad.FecPrimerUso; // incluso coincidiendo
        await ctxC.SaveChangesAsync();

        // Tras persistir (existe), la segunda detección ya no es nueva, pese a timestamps iguales.
        var segunda = repoC.ObtenerOCrear("203.0.113.12", 4);
        Assert.True(segunda.IsSuccess);
        Assert.False(segunda.Value.EsNueva);

        var resC = await serviceC.DetectarNuevaIPConOutboxAsync(1, 1, "203.0.113.12");
        Assert.True(resC.IsSuccess);
        Assert.Null(resC.Value!.Outbox);
        publisherC.Verify(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()), Times.Never);

        // D) IP genuinamente nueva siempre prepara Outbox, y la creación marcó EsNueva=true.
        var (ctxD, _, serviceD, publisherD, _, _) = Build();
        var resD = await serviceD.DetectarNuevaIPConOutboxAsync(1, 1, "203.0.113.13");
        ResultadoOutbox(resD);
        publisherD.Verify(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // S19-T4 — Error de persistencia (operación primaria) se propaga: Failure + 0 Outbox.
    [Fact]
    public async Task T4_ErrorPersistencia_SePropagaYSinOutbox()
    {
        var (ctx, _, service, publisher, logger, _) = Build();
        await ctx.DisposeAsync();

        var result = await service.DetectarNuevaIPConOutboxAsync(7, 2, DirNueva);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("DB_ERROR", result.Error!.Code);
        publisher.Verify(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // S21.3 — PublishAsync (SecurityAlertEvent en VerificarCambioIPAsync) falla: primaria Success,
    // fallo observable vía LogError estructurado.
    [Fact]
    public async Task T5_PublishFallido_IPPersisteYSuccessConLogErrorEstructurado()
    {
        var (ctx, _, service, publisher, logger, _) = Build();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("EVENT_ERROR", "dispatcher rechazó el evento"));

        var ip = IP.Crear(DirNueva, 4, "Test", "TestCity");
        await ctx.IPs.AddAsync(ip);
        await ctx.SaveChangesAsync();

        var result = await service.VerificarCambioIPAsync(7, 2, DirNueva);

        Assert.True(result.IsSuccess, "La operación primaria (detección) no debe fallar por la publicación");
        Assert.Equal(1, await ctx.IPs.CountAsync());
        Assert.True(await ctx.IPs.AnyAsync(i => i.Direccion == DirNueva), "La IP debe permanecer persistida");

        var evtErr = Assert.Single(logger.Events(LoggingEvents.EventFailed));
        Assert.Equal(CBP.Logging.Models.LogLevel.Error, logger.AllLogCalls.First(c => c.Event == evtErr).Level);
        Assert.Equal(CorrTest, (string?)evtErr.Properties[LoggingPropertyNames.CorrelationId]);
        Assert.Equal("7", evtErr.Properties[LoggingPropertyNames.UserId]);
        Assert.Equal(2, evtErr.Properties[LoggingPropertyNames.TenantId]);
    }

    // S21.3 — PublishAsync lanza excepción: mismo contrato, excepción no se traga silenciosamente.
    [Fact]
    public async Task T5b_ExcepcionPublicacion_NoSeTragaYsQuedaLogErrorConExcepcion()
    {
        var (ctx, _, service, publisher, logger, _) = Build();
        var boom = new InvalidOperationException("fallo de red en el dispatcher");
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(boom);

        var ip = IP.Crear(DirNueva, 4, "Test", "TestCity");
        await ctx.IPs.AddAsync(ip);
        await ctx.SaveChangesAsync();

        var result = await service.VerificarCambioIPAsync(7, 2, DirNueva);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await ctx.IPs.CountAsync());

        var evtErr = Assert.Single(logger.Events(LoggingEvents.EventFailed));
        Assert.Same(boom, evtErr.Exception);
        Assert.Equal(CorrTest, (string?)evtErr.Properties[LoggingPropertyNames.CorrelationId]);
        Assert.Contains("Excepcion al publicar", evtErr.Message);
        Assert.Contains("fallo de red", Assert.IsType<object[]>(evtErr.Args).Cast<string>().Single());
    }

    // S21 — El Outbox propaga el CorrelationId del request (W3C) y datos del payload.
    [Fact]
    public async Task T6_OutboxPropagaCorrelationId()
    {
        var (_, _, service, _, _, _) = Build();

        var result = await service.DetectarNuevaIPConOutboxAsync(7, 2, DirNueva);

        var outbox = ResultadoOutbox(result);
        Assert.Equal(CorrTest, outbox.CorrelationId);
    }

    // S21 — El Outbox propaga IdUsuario e IdTenant.
    [Fact]
    public async Task T7_OutboxPropagaUsuarioYTenant()
    {
        var (_, _, service, _, _, _) = Build();

        var result = await service.DetectarNuevaIPConOutboxAsync(42, 9, DirNueva);

        var outbox = ResultadoOutbox(result);
        Assert.Equal(42, outbox.IdUsuario);
        Assert.Equal(9, outbox.IdTenant);
    }

    // S19-T8 — Regresión: tras persistir, una segunda detección de la misma IP no duplica fila ni Outbox.
    [Fact]
    public async Task T8_Regresion_NoDuplicaFilaNiOutboxTrasPersistir()
    {
        var (ctx, _, service, publisher, _, _) = Build();

        var r1 = await service.DetectarNuevaIPConOutboxAsync(7, 2, DirNueva);
        ResultadoOutbox(r1);
        await ctx.SaveChangesAsync();

        var r2 = await service.DetectarNuevaIPConOutboxAsync(7, 2, DirNueva);
        Assert.True(r2.IsSuccess);
        Assert.Null(r2.Value!.Outbox);
        publisher.Verify(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(1, await ctx.IPs.CountAsync());
    }
}