using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S100;

/// <summary>
/// S100 · P58 — el historial federado se confirma (antes se agregaba después del único commit y nunca se guardaba)
/// y forzar-mfa registra una fila por identidad externa del usuario dentro del alcance (antes 0/0/0, contra las FK).
/// </summary>
public sealed class S100HistorialIdenExtTests
{
    private const int TenantFila = 2;
    private const int OtroTenant = 4;
    private const int IdUsuario = 10;
    private const int Admin = 1;

    private readonly List<string> _orden = [];
    private readonly Mock<IHistorialIdenExtService> _historial = new();
    private readonly Mock<IExternalAuthRepository> _externalAuth = new();
    private readonly Mock<IMFAService> _mfa = new();
    private readonly Mock<IUsuarioRepository> _usuarios = new();
    private readonly Mock<IUsuarioTenantRepository> _membresias = new();
    private readonly Mock<IUnitOfWorkAsync> _uow = new();

    public S100HistorialIdenExtTests()
    {
        _historial.Setup(h => h.RegistrarCambioAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<bool>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback(() => _orden.Add("historial"))
            .ReturnsAsync(Result<HistorialIdenExtDto>.Success(new HistorialIdenExtDto()));
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _orden.Add("commit"))
            .ReturnsAsync(1);
        _mfa.Setup(m => m.ObtenerMetodosPorUsuarioAsync(IdUsuario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MFADto>>.Success(Array.Empty<MFADto>()));
        _usuarios.Setup(r => r.ObtenerPorIdAsync(IdUsuario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Usuario?>.Success(null, allowNull: true));
        _usuarios.Setup(r => r.GetByIdAsync(IdUsuario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Usuario>.Success(Usuario.Crear(TenantFila, 1, "u100", "u100@s100.test", "U", "Cien")));
    }

    private static PassPlatDbContext CrearContext(string bd) =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(bd).Options);

    private IdenExtervice CrearServicio(PassPlatDbContext ctx) => new(
        new IdenExtRepository(ctx), _externalAuth.Object, _uow.Object, Mock.Of<IMapper>(),
        _mfa.Object, Mock.Of<IEmailQueue>(), _usuarios.Object, _historial.Object,
        NullLogger<IdenExtervice>.Instance, _membresias.Object);

    private static async Task<string> SembrarIdentidadesAsync()
    {
        var bd = Guid.NewGuid().ToString();
        await using var semilla = CrearContext(bd);
        // las consultas del repositorio incluyen ProvIden y Estado (navegaciones requeridas)
        semilla.Set<ProvIden>().Add(new ProvIden { Id = 1, Codigo = "S100", Nombre = "S100" });
        semilla.Set<EstIdenExt>().Add(new EstIdenExt { Id = 1, Nombre = "Activa" });
        semilla.Set<IdenExt>().AddRange(
            new IdenExt { Id = 5, IdUsuario = IdUsuario, IdProvIden = 1, IdTenant = TenantFila, SubExterno = "s100-a", EsPrincipal = false, IdEstado = 1 },
            new IdenExt { Id = 6, IdUsuario = IdUsuario, IdProvIden = 1, IdTenant = OtroTenant, SubExterno = "s100-b", EsPrincipal = false, IdEstado = 1 });
        await semilla.SaveChangesAsync(TestContext.Current.CancellationToken);
        return bd;
    }

    private void FilaForzarMfa(int idTenant, long idIdentidad, Times veces) =>
        _historial.Verify(h => h.RegistrarCambioAsync(idTenant, IdUsuario, idIdentidad, 1, "FORZAR_MFA", null,
            "MFA revocado por administrador", Admin, false, null, It.IsAny<CancellationToken>()), veces);

    [Fact]
    public async Task ForzarMFA_Sistema_UnaFilaPorIdentidadYUnSoloCommitAlFinal()
    {
        await using var ctx = CrearContext(await SembrarIdentidadesAsync());

        var result = await CrearServicio(ctx).ForzarMFAAsync(IdUsuario, Admin, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        FilaForzarMfa(TenantFila, 5, Times.Once());
        FilaForzarMfa(OtroTenant, 6, Times.Once());
        Assert.Equal(["historial", "historial", "commit"], _orden);
    }

    [Fact]
    public async Task ForzarMFA_AdminDelTenant_SoloRegistraLasIdentidadesDeSuTenant()
    {
        await using var ctx = CrearContext(await SembrarIdentidadesAsync());
        _membresias.Setup(r => r.ObtenerActivoPorTenantAsync(IdUsuario, TenantFila, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UsuarioTenant?>.Success(UsuarioTenant.Crear(IdUsuario, TenantFila, 1), allowNull: true));

        var result = await CrearServicio(ctx).ForzarMFAAsync(IdUsuario, Admin, TenantFila, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        FilaForzarMfa(TenantFila, 5, Times.Once());
        FilaForzarMfa(OtroTenant, 6, Times.Never());
        Assert.Equal(["historial", "commit"], _orden);
    }

    [Fact]
    public async Task ForzarMFA_SinIdentidades_NoRegistraNiConfirma()
    {
        await using var ctx = CrearContext(Guid.NewGuid().ToString());

        var result = await CrearServicio(ctx).ForzarMFAAsync(IdUsuario, Admin, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(_orden);
    }

    [Fact]
    public async Task Revocar_RegistraElHistorialAntesDelCommit()
    {
        await using var ctx = CrearContext(await SembrarIdentidadesAsync());

        var result = await CrearServicio(ctx).RevocarAsync(5, Admin, "s100", null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["historial", "commit"], _orden);
    }

    // S123 · D1 — crítico (seguridad): revocar guarda el 4 «Revocado» del catálogo, que SP_Auth_LoginExterno rechaza
    [Fact]
    public async Task Revocar_DejaEstadoRevocadoDelCatalogo()
    {
        await using var ctx = CrearContext(await SembrarIdentidadesAsync());

        var result = await CrearServicio(ctx).RevocarAsync(5, Admin, "s123", null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var fila = ctx.ChangeTracker.Entries<IdenExt>().Single(e => e.Entity.Id == 5).Entity;
        Assert.Equal((byte?)4, fila.IdEstado);
        Assert.NotNull(fila.FecRevocacion);
        Assert.Equal(Admin, fila.IdUsuarioRevoca);
    }

    // S123 · D6 — crítico (seguridad): reactivar (estado 1 Activo) quita la revocación completa
    [Fact]
    public async Task CambiarEstadoAActivo_LimpiaLaRevocacion()
    {
        var bd = await SembrarIdentidadesAsync();
        await using (var semilla = CrearContext(bd))
        {
            semilla.Set<EstIdenExt>().Add(new EstIdenExt { Id = 4, Nombre = "Revocado" });
            var revocada = await semilla.Set<IdenExt>().SingleAsync(i => i.Id == 5, TestContext.Current.CancellationToken);
            revocada.IdEstado = 4;
            revocada.FecRevocacion = DateTime.Now;
            revocada.IdUsuarioRevoca = Admin;
            revocada.MotivoRevocacion = "s123";
            await semilla.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var ctx = CrearContext(bd);

        var result = await CrearServicio(ctx).CambiarEstadoAsync(5, 1, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var fila = ctx.ChangeTracker.Entries<IdenExt>().Single(e => e.Entity.Id == 5).Entity;
        Assert.Equal((byte?)1, fila.IdEstado);
        Assert.Null(fila.FecRevocacion);
        Assert.Null(fila.IdUsuarioRevoca);
        Assert.Null(fila.MotivoRevocacion);
    }

    [Fact]
    public async Task CambiarEstado_RegistraElHistorialAntesDelCommit()
    {
        await using var ctx = CrearContext(await SembrarIdentidadesAsync());

        var result = await CrearServicio(ctx).CambiarEstadoAsync(5, 2, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["historial", "commit"], _orden);
    }

    [Fact]
    public async Task CambiarPrincipal_RegistraElHistorialAntesDelCommit()
    {
        await using var ctx = CrearContext(await SembrarIdentidadesAsync());

        var result = await CrearServicio(ctx).CambiarPrincipalAsync(5, Admin, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["historial", "commit"], _orden);
    }

    [Fact]
    public async Task Desvincular_ConfirmaElHistorialDespuesDelSP()
    {
        await using var ctx = CrearContext(await SembrarIdentidadesAsync());
        _externalAuth.Setup(r => r.DesvincularIdentidadAsync(5, Admin, true, It.IsAny<CancellationToken>()))
            .Callback(() => _orden.Add("sp"))
            .ReturnsAsync(Result.Success());

        var result = await CrearServicio(ctx).DesvincularAsync(5, Admin, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["sp", "historial", "commit"], _orden);
    }
}
