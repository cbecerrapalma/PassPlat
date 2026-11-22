using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Security.Cryptography.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Aplicacion.Services.OAuth;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S99;

/// <summary>
/// S99 · P57 — federación con el criterio de S95: un no-sistema (alcanceTenant con valor) solo
/// escribe y lee filas de su tenant; fuera de él, NotFound (404) y sin efectos.
/// </summary>
public sealed class S99AlcanceTenantEscriturasFederacionTests
{
    private const int TenantFila = 2;
    private const int OtroTenant = 4;

    private readonly Mock<IHistorialIdenExtService> _historial = new();
    private readonly Mock<IExternalAuthRepository> _externalAuth = new();
    private readonly Mock<IMFAService> _mfa = new();
    private readonly Mock<IUsuarioTenantRepository> _membresias = new();
    private readonly Mock<IUnitOfWorkAsync> _uow = new();
    private readonly Mock<IMapper> _mapper = new();

    private static PassPlatDbContext CrearContext(string bd) =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(bd).Options);

    private IdenExtervice CrearServicio(PassPlatDbContext ctx) => new(
        new IdenExtRepository(ctx), _externalAuth.Object, _uow.Object, _mapper.Object,
        _mfa.Object, Mock.Of<IEmailQueue>(), Mock.Of<IUsuarioRepository>(), _historial.Object,
        NullLogger<IdenExtervice>.Instance, _membresias.Object);

    private static async Task<string> SembrarIdentidadesAsync()
    {
        var bd = Guid.NewGuid().ToString();
        await using var semilla = CrearContext(bd);
        // las consultas del repositorio incluyen ProvIden y Estado (navegaciones requeridas)
        semilla.Set<ProvIden>().Add(new ProvIden { Id = 1, Codigo = "S99", Nombre = "S99" });
        semilla.Set<EstIdenExt>().Add(new EstIdenExt { Id = 1, Nombre = "Activa" });
        semilla.Set<IdenExt>().AddRange(
            new IdenExt { Id = 5, IdUsuario = 10, IdProvIden = 1, IdTenant = TenantFila, SubExterno = "s99-a", EsPrincipal = false, IdEstado = 1 },
            new IdenExt { Id = 6, IdUsuario = 10, IdProvIden = 1, IdTenant = OtroTenant, SubExterno = "s99-b", EsPrincipal = false, IdEstado = 1 });
        await semilla.SaveChangesAsync(TestContext.Current.CancellationToken);
        return bd;
    }

    private void HistorialNuncaRegistrado() =>
        _historial.Verify(h => h.RegistrarCambioAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<bool>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Revocar_IdentidadDeOtroTenant_NotFoundSinCambios()
    {
        var bd = await SembrarIdentidadesAsync();
        await using (var ctx = CrearContext(bd))
        {
            var result = await CrearServicio(ctx).RevocarAsync(5, 1, "s99", OtroTenant, TestContext.Current.CancellationToken);
            Assert.True(result.IsFailure);
            Assert.Equal("ENTITY_NOT_FOUND", result.Error!.Code);
        }
        await using var verificacion = CrearContext(bd);
        Assert.Equal((byte)1, (await verificacion.Set<IdenExt>().FindAsync([5L], TestContext.Current.CancellationToken))!.IdEstado);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        HistorialNuncaRegistrado();
    }

    [Fact]
    public async Task Desvincular_IdentidadDeOtroTenant_NotFoundSinDesvincular()
    {
        var bd = await SembrarIdentidadesAsync();
        await using var ctx = CrearContext(bd);

        var result = await CrearServicio(ctx).DesvincularAsync(5, 1, OtroTenant, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("ENTITY_NOT_FOUND", result.Error!.Code);
        _externalAuth.Verify(r => r.DesvincularIdentidadAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        HistorialNuncaRegistrado();
    }

    [Fact]
    public async Task CambiarEstado_IdentidadDeOtroTenant_NotFound()
    {
        var bd = await SembrarIdentidadesAsync();
        await using var ctx = CrearContext(bd);

        var result = await CrearServicio(ctx).CambiarEstadoAsync(5, 2, OtroTenant, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("ENTITY_NOT_FOUND", result.Error!.Code);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CambiarPrincipal_IdentidadDeOtroTenant_NotFoundYPrincipalDelTenantPropio_Exito()
    {
        var bd = await SembrarIdentidadesAsync();
        await using var ctx = CrearContext(bd);
        var servicio = CrearServicio(ctx);

        var fuera = await servicio.CambiarPrincipalAsync(6, 1, TenantFila, TestContext.Current.CancellationToken);
        Assert.True(fuera.IsFailure);
        Assert.Equal("ENTITY_NOT_FOUND", fuera.Error!.Code);

        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Usuario>.Success(Usuario.Crear(TenantFila, 1, "u99", "u99@s99.test", "U", "Noventaynueve")));
        var propio = await new IdenExtervice(new IdenExtRepository(ctx), _externalAuth.Object, _uow.Object, _mapper.Object,
                _mfa.Object, Mock.Of<IEmailQueue>(), usuarios.Object, _historial.Object,
                NullLogger<IdenExtervice>.Instance, _membresias.Object)
            .CambiarPrincipalAsync(5, 1, TenantFila, TestContext.Current.CancellationToken);
        Assert.True(propio.IsSuccess);
    }

    [Fact]
    public async Task ObtenerPorUsuario_ConAlcance_SoloDevuelveIdentidadesDelTenant()
    {
        var bd = await SembrarIdentidadesAsync();
        await using var ctx = CrearContext(bd);
        _mapper.Setup(m => m.Map<IReadOnlyList<IdenExtDto>>(It.IsAny<object>()))
            .Returns((object origen) => ((IEnumerable<IdenExt>)origen)
                .Select(e => new IdenExtDto { Id = e.Id, IdTenant = e.IdTenant }).ToList());

        var conAlcance = await CrearServicio(ctx).ObtenerPorUsuarioAsync(10, TenantFila, TestContext.Current.CancellationToken);
        var sinAlcance = await CrearServicio(ctx).ObtenerPorUsuarioAsync(10, null, TestContext.Current.CancellationToken);

        Assert.True(conAlcance.IsSuccess);
        Assert.Equal([5L], conAlcance.Value.Select(i => i.Id));
        Assert.Equal(2, sinAlcance.Value.Count);
    }

    [Fact]
    public async Task ForzarMFA_UsuarioSinMembresiaEnElTenant_NotFoundSinRevocar()
    {
        await using var ctx = CrearContext(Guid.NewGuid().ToString());
        _membresias.Setup(r => r.ObtenerActivoPorTenantAsync(10, OtroTenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UsuarioTenant?>.Success(null, allowNull: true));

        var result = await CrearServicio(ctx).ForzarMFAAsync(10, 1, OtroTenant, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("ENTITY_NOT_FOUND", result.Error!.Code);
        _mfa.Verify(m => m.ObtenerMetodosPorUsuarioAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ConfProvIdenService CrearConfProvIden(PassPlatDbContext ctx, Mock<IUnitOfWorkAsync> uow, IMapper? mapper = null)
    {
        var http = new Mock<IHttpContextAccessor>();
        http.SetupGet(x => x.HttpContext).Returns((HttpContext?)null);
        return new ConfProvIdenService(new ConfProvIdenRepository(ctx), uow.Object, mapper ?? Mock.Of<IMapper>(), Mock.Of<IEncryptionService>(),
            Mock.Of<IEmailQueue>(), NullLogger<ConfProvIdenService>.Instance, Mock.Of<IAuditoriaPwdService>(), http.Object,
            Mock.Of<IExternalProviderValidator>());
    }

    private static async Task SembrarConfProvIdenInactivasAsync(string bd, params int[] ids)
    {
        await using var semilla = CrearContext(bd);
        foreach (var id in ids)
        {
            var entidad = ConfProvIden.Crear(TenantFila, id, "cid", "secret", "https://cb.local");
            entidad.Id = id;
            entidad.Activo = false;
            entidad.Estado = 0;
            semilla.Set<ConfProvIden>().Add(entidad);
        }
        await semilla.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // S122 · D1 (crítico: aislamiento entre tenants en una escritura nueva)
    [Fact]
    public async Task ConfProvIden_ActivarDeOtroTenant_NotFoundSinCambios()
    {
        var bd = Guid.NewGuid().ToString();
        await SembrarConfProvIdenInactivasAsync(bd, 8);
        var uow = new Mock<IUnitOfWorkAsync>();
        await using (var ctx = CrearContext(bd))
        {
            var servicio = CrearConfProvIden(ctx, uow);
            var activar = await servicio.ActivarAsync(8, OtroTenant, TestContext.Current.CancellationToken);

            Assert.Equal("ENTITY_NOT_FOUND", activar.Error!.Code);
        }
        await using var verificacion = CrearContext(bd);
        var fila = (await verificacion.Set<ConfProvIden>().FindAsync([8], TestContext.Current.CancellationToken))!;
        Assert.False(fila.Activo);
        Assert.Equal((byte)0, fila.Estado);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // S122 · D1 + D2 (crítico: integridad, la reactivación es la causa del sprint)
    [Fact]
    public async Task ConfProvIden_ActivarYActualizarConEstado1_DejanActivoYEstado1()
    {
        var bd = Guid.NewGuid().ToString();
        await SembrarConfProvIdenInactivasAsync(bd, 8, 9);
        var uow = new Mock<IUnitOfWorkAsync>();
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<ConfProvIdenDto>(It.IsAny<object>())).Returns(new ConfProvIdenDto());
        await using var ctx = CrearContext(bd);
        var servicio = CrearConfProvIden(ctx, uow, mapper.Object);

        var activar = await servicio.ActivarAsync(8, TenantFila, TestContext.Current.CancellationToken);
        var actualizar = await servicio.ActualizarAsync(9, new ActualizarConfProvIdenDto { Estado = 1 }, TenantFila, TestContext.Current.CancellationToken);

        Assert.True(activar.IsSuccess);
        Assert.True(actualizar.IsSuccess);
        var activada = (await ctx.Set<ConfProvIden>().FindAsync([8], TestContext.Current.CancellationToken))!;
        var actualizada = (await ctx.Set<ConfProvIden>().FindAsync([9], TestContext.Current.CancellationToken))!;
        Assert.True(activada.Activo);
        Assert.Equal((byte)1, activada.Estado);
        Assert.True(actualizada.Activo);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ConfProvIden_DesactivarYActualizarDeOtroTenant_NotFoundSinCambios()
    {
        var bd = Guid.NewGuid().ToString();
        await using (var semilla = CrearContext(bd))
        {
            var entidad = ConfProvIden.Crear(TenantFila, 1, "cid", "secret", "https://cb.local");
            entidad.Id = 7;
            semilla.Set<ConfProvIden>().Add(entidad);
            await semilla.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var uow = new Mock<IUnitOfWorkAsync>();
        await using (var ctx = CrearContext(bd))
        {
            var servicio = CrearConfProvIden(ctx, uow);
            var desactivar = await servicio.DesactivarAsync(7, OtroTenant, TestContext.Current.CancellationToken);
            var actualizar = await servicio.ActualizarAsync(7, new ActualizarConfProvIdenDto { ClientId = "otro" }, OtroTenant, TestContext.Current.CancellationToken);

            Assert.Equal("ENTITY_NOT_FOUND", desactivar.Error!.Code);
            Assert.Equal("ENTITY_NOT_FOUND", actualizar.Error!.Code);
        }
        await using var verificacion = CrearContext(bd);
        var fila = (await verificacion.Set<ConfProvIden>().FindAsync([7], TestContext.Current.CancellationToken))!;
        Assert.True(fila.Activo);
        Assert.Equal("cid", fila.ClientId);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
