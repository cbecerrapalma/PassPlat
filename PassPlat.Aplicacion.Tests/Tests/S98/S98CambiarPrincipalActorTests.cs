using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S98;

/// <summary>
/// S98 · D2 (P14) — cambiar la identidad principal: el dueño sale de la identidad y el
/// historial registra al actor que manda el controlador (el usuario del token).
/// </summary>
public sealed class S98CambiarPrincipalActorTests
{
    private readonly Mock<IHistorialIdenExtService> _historial = new();
    private readonly Mock<IUsuarioRepository> _usuarios = new();

    private static PassPlatDbContext CrearContext(string bd) =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(bd).Options);

    private IdenExtervice CrearServicio(PassPlatDbContext ctx) => new(
        new IdenExtRepository(ctx), Mock.Of<IExternalAuthRepository>(), Mock.Of<IUnitOfWorkAsync>(), Mock.Of<IMapper>(),
        Mock.Of<IMFAService>(), Mock.Of<IEmailQueue>(), _usuarios.Object, _historial.Object,
        NullLogger<IdenExtervice>.Instance, Mock.Of<IUsuarioTenantRepository>());

    [Fact]
    public async Task CambiarPrincipal_ActorDistintoDelDueno_MarcaPrincipalYRegistraActor()
    {
        var bd = Guid.NewGuid().ToString();
        await using (var semilla = CrearContext(bd))
        {
            semilla.Set<IdenExt>().AddRange(
                new IdenExt { Id = 5, IdUsuario = 10, IdProvIden = 1, IdTenant = 2, SubExterno = "s98-a", EsPrincipal = false },
                new IdenExt { Id = 6, IdUsuario = 10, IdProvIden = 1, IdTenant = 2, SubExterno = "s98-b", EsPrincipal = true });
            await semilla.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var ctx = CrearContext(bd);
        _usuarios.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Usuario>.Success(Usuario.Crear(2, 1, "u98", "u98@s98.test", "U", "Noventayocho")));

        var result = await CrearServicio(ctx).CambiarPrincipalAsync(5, 1, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True((await ctx.Set<IdenExt>().FindAsync([5L], TestContext.Current.CancellationToken))!.EsPrincipal);
        _historial.Verify(h => h.RegistrarCambioAsync(2, 10, 5L, 1, "CAMBIAR_PRINCIPAL", "False", "true", 1, false, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CambiarPrincipal_IdentidadInexistente_RetornaFalloSinHistorial()
    {
        await using var ctx = CrearContext(Guid.NewGuid().ToString());

        var result = await CrearServicio(ctx).CambiarPrincipalAsync(99, 1, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        _historial.Verify(h => h.RegistrarCambioAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<bool>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
