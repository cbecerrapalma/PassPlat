using CBP.Results;
using CBP.Security.Cryptography.Factories;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S137;

/// <summary>
/// S137 · D7 — la contraseña nueva se valida en el servidor: política aplicable e historial
/// (con Verify sobre hashes con sal, no comparando hashes). Argon2id y validadores reales de CBP.
/// </summary>
public sealed class S137PasswordNuevaServiceTests
{
    private const string PasswordAnterior = "Anterior#2026Xy";

    private readonly Mock<IPoliticaPwdService> _politicas = new();
    private readonly Mock<IUsuarioRepository> _usuarios = new();
    private readonly Mock<IHistorialPwdRepository> _historial = new();
    private readonly CBP.Security.Cryptography.Services.IPasswordService _hashing = ServiceFactory.CreateDefault();

    private static PoliticaPwdDto PoliticaPrueba() => new()
    {
        Id = 1,
        Codigo = "S137",
        Nombre = "Política S137",
        LongMin = 8,
        LongMax = 128,
        ReqMayuscula = true,
        ReqMinuscula = true,
        ReqNumero = true,
        ReqEspecial = false,
        VerificarBrechas = false,
        ProhPwdComun = false,
        PwdRecordadas = 5,
        Activa = true
    };

    private async Task<PasswordNuevaService> CrearServicioAsync(CancellationToken ct)
    {
        _politicas
            .Setup(p => p.ObtenerPoliticaAplicableAsync(1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PoliticaPwdDto?>.Success(PoliticaPrueba(), allowNull: true));

        var usuario = Usuario.Crear(1, 1, "usuario.s137", "s137@ejemplo.test", "Prueba", "S137");
        usuario.Id = 1;
        _usuarios
            .Setup(u => u.ObtenerPorIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Usuario?>.Success(usuario));

        var politicaCbp = new CBP.Security.Cryptography.Models.PoliticaPwd
        {
            LongMin = 1,
            LongMax = 128,
            ProhPwdComun = false,
            ProhInfoUsuario = false,
            VerificarBrechas = false
        };
        var creacion = await _hashing.CreatePasswordAsync(PasswordAnterior, politicaCbp, cancellationToken: ct);
        var hashAnterior = creacion.HashInfo!.Hash;

        IReadOnlyList<HistorialPwd> filas = [HistorialPwd.Crear(1, 1, hashAnterior)];
        _historial
            .Setup(h => h.ObtenerHistorialRecienteAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<HistorialPwd>>.Success(filas));

        return new PasswordNuevaService(_politicas.Object, _usuarios.Object, _historial.Object, new PassPlatPasswordSecurity(), _hashing);
    }

    [Fact]
    public async Task ValidarAsync_NoCumplePolitica_PasswordWeak()
    {
        var ct = TestContext.Current.CancellationToken;
        var servicio = await CrearServicioAsync(ct);

        var result = await servicio.ValidarAsync(1, 1, null, "abc", ct);

        Assert.True(result.IsFailure);
        Assert.Equal("PASSWORD_WEAK", result.Error!.Code);
    }

    [Fact]
    public async Task ValidarAsync_PasswordDelHistorial_PasswordReuse()
    {
        var ct = TestContext.Current.CancellationToken;
        var servicio = await CrearServicioAsync(ct);

        var result = await servicio.ValidarAsync(1, 1, null, PasswordAnterior, ct);

        Assert.True(result.IsFailure);
        Assert.Equal("PASSWORD_REUSE", result.Error!.Code);
    }

    [Fact]
    public async Task ValidarAsync_PasswordValidaYNueva_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        var servicio = await CrearServicioAsync(ct);

        var result = await servicio.ValidarAsync(1, 1, null, "Nueva#2026Kw", ct);

        Assert.True(result.IsSuccess);
    }
}
