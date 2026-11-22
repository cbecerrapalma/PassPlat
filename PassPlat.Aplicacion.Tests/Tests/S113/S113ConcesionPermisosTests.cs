using CBP.Results;
using Microsoft.EntityFrameworkCore;
using Moq;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S113;

/// <summary>
/// S113 · D3 (P41) — un ejecutor que no es sistema solo concede permisos que él tiene en su tenant,
/// sumando todas sus apps (el token de PASSPLAT no trae los permisos de INVENTA).
/// </summary>
public sealed class S113ConcesionPermisosTests
{
    private const int Ejecutor = 3;
    private const int Tenant = 2;
    private const int AppPassPlat = 1;
    private const int AppInventa = 12;

    private static async Task<(ConcesionPermisosService Svc, Dictionary<string, int> Ids)> CrearAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = new PassPlatDbContext(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var lista = new[] { "USUARIOS_VER", "TENANTS_CREAR", "INVENTA_VENTAS_VER" }.Select(c => Permiso.Crear(c, c, 1)).ToList();
        ctx.Set<Permiso>().AddRange(lista);
        ctx.Set<Acceso>().AddRange(Acceso.Crear(Ejecutor, Tenant, AppPassPlat, 5), Acceso.Crear(Ejecutor, Tenant, AppInventa, 5));
        await ctx.SaveChangesAsync(ct);
        var ids = lista.ToDictionary(p => p.Codigo, p => p.Id);
        ctx.Set<RolPermiso>().AddRange(
            RolPermiso.Crear(7, ids["USUARIOS_VER"]), RolPermiso.Crear(7, ids["TENANTS_CREAR"]),
            RolPermiso.Crear(8, ids["USUARIOS_VER"]), RolPermiso.Crear(8, ids["INVENTA_VENTAS_VER"]));
        await ctx.SaveChangesAsync(ct);

        var auth = new Mock<IAuthRepository>();
        auth.Setup(a => a.ObtenerCodigosPermisosPorUsuarioAsync(Ejecutor, Tenant, AppPassPlat, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(["USUARIOS_VER"]));
        auth.Setup(a => a.ObtenerCodigosPermisosPorUsuarioAsync(Ejecutor, Tenant, AppInventa, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(["INVENTA_VENTAS_VER"]));
        var svc = new ConcesionPermisosService(new AccesoRepository(ctx), auth.Object, new PermisoRepository(ctx), new RolPermisoRepository(ctx));
        return (svc, ids);
    }

    [Theory]
    [InlineData("USUARIOS_VER", true)]       // lo tiene en PASSPLAT
    [InlineData("INVENTA_VENTAS_VER", true)] // lo tiene en otra app de su tenant
    [InlineData("TENANTS_CREAR", false)]     // no lo tiene → 403
    public async Task ValidarPermisos_SegunLosDelEjecutorEnSuTenant(string codigo, bool concede)
    {
        var (svc, ids) = await CrearAsync();

        var resultado = await svc.ValidarPermisosAsync([ids[codigo]], Ejecutor, Tenant, TestContext.Current.CancellationToken);

        Assert.Equal(concede, resultado.IsSuccess);
        if (!concede)
        {
            Assert.Equal("PERMISO_NO_CONCEDIBLE", resultado.Error!.Code);
            Assert.Equal(403, resultado.Error.HttpStatusCode);
        }
    }

    [Fact]
    public async Task ValidarRol_ConUnPermisoQueNoTiene_Rechaza()
    {
        var (svc, _) = await CrearAsync();
        var ct = TestContext.Current.CancellationToken;

        var ajeno = await svc.ValidarRolAsync(7, Ejecutor, Tenant, ct);
        var propio = await svc.ValidarRolAsync(8, Ejecutor, Tenant, ct);

        Assert.Equal("PERMISO_NO_CONCEDIBLE", ajeno.Error?.Code);
        Assert.True(propio.IsSuccess);
    }
}
