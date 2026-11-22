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
using PassPlat.Aplicacion.Tests.Tests.Framework.S17;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Contexto;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S19;

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
        await ctx.IPs.AddAsync(ip, TestContext.Current.CancellationToken);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await service.VerificarCambioIPAsync(7, 2, DirNueva, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, "La operación primaria (detección) no debe fallar por la publicación");
        Assert.Equal(1, await ctx.IPs.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await ctx.IPs.AnyAsync(i => i.Direccion == DirNueva, cancellationToken: TestContext.Current.CancellationToken), "La IP debe permanecer persistida");

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
        await ctx.IPs.AddAsync(ip, TestContext.Current.CancellationToken);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await service.VerificarCambioIPAsync(7, 2, DirNueva, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await ctx.IPs.CountAsync(cancellationToken: TestContext.Current.CancellationToken));

        var evtErr = Assert.Single(logger.Events(LoggingEvents.EventFailed));
        Assert.Same(boom, evtErr.Exception);
        Assert.Equal(CorrTest, (string?)evtErr.Properties[LoggingPropertyNames.CorrelationId]);
        Assert.Contains("Excepcion al publicar", evtErr.Message);
        Assert.Contains("fallo de red", Assert.IsType<object[]>(evtErr.Args).Cast<string>().Single());
    }
}
