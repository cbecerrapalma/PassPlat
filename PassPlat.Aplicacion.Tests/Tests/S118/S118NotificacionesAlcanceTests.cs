using AutoMapper;
using CBP.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PassPlat.Aplicacion.Mapping;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S118;

/// <summary>
/// S118 · D5 (P54/P44) — notificaciones acotadas al tenant de la sesión: alcance null = sin acotar
/// (sistema o el propio usuario); con valor = solo las notificaciones de ese tenant.
/// </summary>
public class S118NotificacionesAlcanceTests
{
    private const int IdUsuario = 7, TenantA = 10, TenantB = 20;

    private static readonly IMapper Mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<AplicacionProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static NotificacionService Sembrar()
    {
        var ctx = new PassPlatDbContext(new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        ctx.Notificaciones.AddRange(
            Notificacion.Crear(IdUsuario, TenantA, "INFO", "A1"),
            Notificacion.Crear(IdUsuario, TenantA, "INFO", "A2"),
            Notificacion.Crear(IdUsuario, TenantB, "INFO", "B1"),
            Notificacion.Crear(99, TenantB, "INFO", "Otro usuario"));
        ctx.SaveChanges();
        return new NotificacionService(new NotificacionRepository(ctx), Mapper);
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData(TenantA, 2)]
    [InlineData(TenantB, 1)]
    public async Task NoLeidas_y_Contar_respetan_el_alcance(int? alcanceTenant, int esperado)
    {
        var service = Sembrar();

        var lista = await service.ObtenerNoLeidasAsync(IdUsuario, alcanceTenant, Ct);

        Assert.True(lista.IsSuccess);
        Assert.Equal(esperado, lista.Value.Count);
    }

    [Fact]
    public async Task Pagina_por_tenant_solo_trae_ese_tenant()
    {
        var service = Sembrar();

        var pagina = await service.ObtenerPaginadoPorTenantAsync(TenantA, new PaginationOptions<Notificacion>(1, 20), Ct);

        Assert.True(pagina.IsSuccess);
        Assert.Equal(2, pagina.Value.TotalCount);
        Assert.All(pagina.Value.Items, n => Assert.Equal(TenantA, n.IdTenant));
    }
}
