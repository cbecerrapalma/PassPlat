using AutoMapper;
using CBP.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Mapping;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Tests.Tests.S92;

/// <summary>
/// S92 · D2 — GrupoService.CrearAsync es el único dueño del commit al crear un grupo:
/// guarda una vez y devuelve el Id generado (el controlador ya no repite SaveChangesAsync).
/// </summary>
public class S92GrupoCrearUnSoloCommitTests
{
    private static readonly IMapper Mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<AplicacionProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CrearAsync_guarda_una_sola_vez_y_devuelve_el_id_generado()
    {
        var ctx = new PassPlatDbContext(new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var tenant = new Tenant { Codigo = "ABARROTES", Nombre = "Abarrotes del Sur" };
        ctx.Tenants.Add(tenant);
        ctx.SaveChanges();

        var uow = new Mock<IUnitOfWorkAsync>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(c => ctx.SaveChangesAsync(c));
        var servicio = new GrupoService(new GrupoRepository(ctx), uow.Object, Mapper);

        var r = await servicio.CrearAsync(new CrearGrupoDto { IdTenant = tenant.Id, Codigo = "VENTAS", Nombre = "Ventas" }, Ct);

        Assert.True(r.IsSuccess);
        Assert.True(r.Value!.Id > 0);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(ctx.Grupos.AsNoTracking().Where(g => g.Codigo == "VENTAS" && g.IdTenant == tenant.Id && g.Activo));
    }
}
