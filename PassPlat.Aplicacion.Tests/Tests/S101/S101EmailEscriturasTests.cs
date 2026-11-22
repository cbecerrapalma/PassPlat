using AutoMapper;
using CBP.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S101;

/// <summary>
/// S101 · D1-D2 — escrituras de Email: providers con alta/edición validadas y un solo commit;
/// asignaciones de cuenta sin duplicados; quitar una cuenta de otro tenant es 404 sin efectos.
/// </summary>
public sealed class S101EmailEscriturasTests
{
    private const int TenantFila = 2;
    private const int OtroTenant = 4;

    private readonly Mock<IUnitOfWorkAsync> _uow = new();
    private readonly Mock<IMapper> _mapper = new();

    public S101EmailEscriturasTests() =>
        _mapper.Setup(m => m.Map<EmailProviderDto>(It.IsAny<object>()))
            .Returns((object origen) =>
            {
                var p = (EmailProvider)origen;
                return new EmailProviderDto { Id = p.Id, Codigo = p.Codigo, Nombre = p.Nombre, Descripcion = p.Descripcion, Activo = p.Activo };
            });

    private static PassPlatDbContext CrearContext(string bd) =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(bd).Options);

    private static async Task<string> SembrarAsync(Action<PassPlatDbContext> sembrar)
    {
        var bd = Guid.NewGuid().ToString();
        await using var semilla = CrearContext(bd);
        sembrar(semilla);
        await semilla.SaveChangesAsync(TestContext.Current.CancellationToken);
        return bd;
    }

    private EmailProviderService CrearProviders(PassPlatDbContext ctx) =>
        new(new EmailProviderRepository(ctx), _uow.Object, _mapper.Object);

    private void NuncaGuardado() => _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Crear_provider_con_codigo_existente_devuelve_CODIGO_DUPLICADO()
    {
        var bd = await SembrarAsync(c => c.Set<EmailProvider>().Add(new EmailProvider { Id = 1, Codigo = "SMTP", Nombre = "SMTP" }));
        await using var ctx = CrearContext(bd);

        var result = await CrearProviders(ctx).CrearAsync(new CrearEmailProviderDto { Codigo = " smtp ", Nombre = "Otro" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("CODIGO_DUPLICADO", result.Error!.Code);
        NuncaGuardado();
    }

    [Fact]
    public async Task Crear_provider_valido_normaliza_el_codigo_y_guarda_una_vez()
    {
        await using var ctx = CrearContext(Guid.NewGuid().ToString());

        var result = await CrearProviders(ctx).CrearAsync(
            new CrearEmailProviderDto { Codigo = " postmark ", Nombre = " Postmark ", Descripcion = "  " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("POSTMARK", result.Value.Codigo);
        Assert.Equal("Postmark", result.Value.Nombre);
        Assert.Null(result.Value.Descripcion);
        Assert.True(result.Value.Activo);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        var invalido = await CrearProviders(ctx).CrearAsync(new CrearEmailProviderDto { Codigo = new string('X', 21), Nombre = "N" }, TestContext.Current.CancellationToken);
        Assert.Equal("DATOS_INVALIDOS", invalido.Error!.Code);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Actualizar_provider_conserva_el_codigo_y_fija_FecMod()
    {
        var bd = await SembrarAsync(c => c.Set<EmailProvider>().Add(new EmailProvider { Id = 3, Codigo = "SES", Nombre = "SES", Activo = true }));
        await using var ctx = CrearContext(bd);

        var result = await CrearProviders(ctx).ActualizarAsync(3,
            new ActualizarEmailProviderDto { Nombre = "Amazon SES", Descripcion = "AWS", Activo = false }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var fila = (await ctx.Set<EmailProvider>().FindAsync([(byte)3], TestContext.Current.CancellationToken))!;
        Assert.Equal("SES", fila.Codigo);
        Assert.Equal("Amazon SES", fila.Nombre);
        Assert.False(fila.Activo);
        Assert.NotNull(fila.FecMod);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Asignar_cuenta_a_tenant_duplicada_devuelve_ASIGNACION_DUPLICADA()
    {
        // el par existente está inactivo: la UQ (IdTenant, IdEmailAccount) lo cuenta igual
        var bd = await SembrarAsync(c => c.Set<TenantEmailAccount>().Add(
            new TenantEmailAccount { Id = 5, IdTenant = TenantFila, IdEmailAccount = 1, Activo = false }));
        await using var ctx = CrearContext(bd);
        var servicio = new TenantEmailAccountService(new TenantEmailAccountRepository(ctx), _uow.Object, _mapper.Object);

        var result = await servicio.CrearAsync(new CrearTenantEmailAccountDto { IdTenant = TenantFila, IdEmailAccount = 1 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("ASIGNACION_DUPLICADA", result.Error!.Code);
        _mapper.Verify(m => m.Map<TenantEmailAccount>(It.IsAny<object>()), Times.Never);
        NuncaGuardado();
    }

    [Fact]
    public async Task Asignar_cuenta_a_app_duplicada_devuelve_ASIGNACION_DUPLICADA()
    {
        var bd = await SembrarAsync(c => c.Set<AppEmailAccount>().Add(
            new AppEmailAccount { Id = 7, IdApp = 1, IdEmailAccount = 1, Activo = true }));
        await using var ctx = CrearContext(bd);
        var servicio = new AppEmailAccountService(new AppEmailAccountRepository(ctx), _uow.Object, _mapper.Object);

        var result = await servicio.CrearAsync(new CrearAppEmailAccountDto { IdApp = 1, IdEmailAccount = 1, EsPredeterminada = true }, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("ASIGNACION_DUPLICADA", result.Error!.Code);
        _mapper.Verify(m => m.Map<AppEmailAccount>(It.IsAny<object>()), Times.Never);
        NuncaGuardado();
    }

    [Fact]
    public async Task Quitar_cuenta_de_otro_tenant_devuelve_NotFound_y_no_borra()
    {
        var bd = await SembrarAsync(c => c.Set<TenantEmailAccount>().Add(
            new TenantEmailAccount { Id = 5, IdTenant = TenantFila, IdEmailAccount = 1, Activo = true }));
        await using (var ctx = CrearContext(bd))
        {
            var servicio = new TenantEmailAccountService(new TenantEmailAccountRepository(ctx), _uow.Object, _mapper.Object);

            var result = await servicio.EliminarAsync(5, OtroTenant, TestContext.Current.CancellationToken);

            Assert.True(result.IsFailure);
            Assert.Equal("ENTITY_NOT_FOUND", result.Error!.Code);
            Assert.DoesNotContain(ctx.ChangeTracker.Entries<TenantEmailAccount>(), e => e.State == EntityState.Deleted);
        }
        await using var verificacion = CrearContext(bd);
        Assert.NotNull(await verificacion.Set<TenantEmailAccount>().FindAsync([5], TestContext.Current.CancellationToken));
        NuncaGuardado();
    }
}
