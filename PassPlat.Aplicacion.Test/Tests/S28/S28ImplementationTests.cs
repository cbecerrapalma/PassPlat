using System.Reflection;
using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Security.Cryptography.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Dtos.Contexto;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Aplicacion.Validations.Catalogos;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Test.Tests.S28;

/// <summary>
/// S28.3 — Tests de implementación S28 (DEUDA-001 concurrencia Acceso + DEUDA-003 higiene CS8602).
/// Ver Docs/Architecture/S27-Dependency-Debt-Discovery.md §5.
/// T1-T6: AccesoService.AsignarAccesoAsync — éxito y discriminación 2601/2627 → ACCESO_DUPLICADO.
/// T6: AccesoConfiguration — sin trigger fantasma + índice único (IdUsuario, IdApp, IdRol).
/// T7-T8: ConfProvIdenService — ClientSecret cifrado con Trim / no-ciifa en null; validator null-safe.
/// </summary>
public class S28ImplementationTests
{
    private const string MensajeUniqueAccesos =
        "Violation of UNIQUE KEY constraint 'UX_Accesos_TenantUsrAppRol'. Cannot insert duplicate key in object 'dbo.Accesos'.";

    private const string MensajeUniqueOtraTabla =
        "Violation of UNIQUE KEY constraint 'UX_Usuarios_Email'. Cannot insert duplicate key in object 'dbo.Usuarios'.";

    private static PassPlatDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PassPlatDbContext(options);
    }

    private static SqlException CrearSqlException(int numero, string mensaje)
    {
        var tipo = typeof(SqlException);
        var tipoError = tipo.Assembly.GetType("Microsoft.Data.SqlClient.SqlError")!;
        var error = Activator.CreateInstance(tipoError, BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { numero, (byte)0, (byte)0, "server", mensaje, "proc", 0, 0, null! }, null)!;

        var tipoColeccion = tipo.Assembly.GetType("Microsoft.Data.SqlClient.SqlErrorCollection")!;
        var coleccion = Activator.CreateInstance(tipoColeccion, true)!;
        tipoColeccion.GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(coleccion, new[] { error });

        var ctor = tipo.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(c => c.GetParameters().Length == 4);
        return (SqlException)ctor!.Invoke(new object[] { mensaje, coleccion, null!, Guid.NewGuid() });
    }

    private static (AccesoService service, Mock<IUnitOfWorkAsync> uow) BuildAccesoService()
    {
        var ctx = CrearContext();
        var repo = new AccesoRepository(ctx);
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<AccesoDto>(It.IsAny<Acceso>()))
            .Returns((Acceso a) => new AccesoDto
            {
                Id = a.Id,
                IdUsuario = a.IdUsuario,
                IdTenant = a.IdTenant,
                IdApp = a.IdApp,
                IdRol = a.IdRol,
                Activo = a.Activo
            });
        var usuarioRepo = new Mock<IUsuarioRepository>();
        usuarioRepo
            .Setup(r => r.ObtenerPorIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Usuario?>.Success(
                new Usuario { Id = 9, NomUsuario = "u", Nombre = "U", Apellido = "" }, allowNull: true));

        var uow = new Mock<IUnitOfWorkAsync>();
        var service = new AccesoService(repo, mapper.Object, usuarioRepo.Object,
            Mock.Of<IRolRepository>(), Mock.Of<IEmailQueue>(), NullLogger<AccesoService>.Instance, uow.Object);
        return (service, uow);
    }

    private static AsignarAccesoDto DtoAcceso(int idRol = 3) => new()
    {
        IdUsuario = 9,
        IdTenant = 2,
        IdApp = 1,
        IdRol = idRol
    };

    // T1 — Éxito: nuevo acceso → Result success + DTO mapeado.
    [Fact]
    public async Task T1_AsignarAcceso_Exito_TraduceAEntidadYRetornaSuccess()
    {
        var (service, uow) = BuildAccesoService();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await service.AsignarAccesoAsync(DtoAcceso());

        Assert.True(result.IsSuccess, $"Esperaba Success; error: {result.Error?.Code}");
        Assert.Equal(9, result.Value.IdUsuario);
        Assert.Equal(2, result.Value.IdTenant);
        Assert.Equal(1, result.Value.IdApp);
        Assert.Equal(3, result.Value.IdRol);
        Assert.True(result.Value.Activo);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // T2 — Duplicado 2601 mencionando "Accesos" → Failure ACCESO_DUPLICADO (no excepción).
    [Fact]
    public async Task T2_DuplicadoSqlError2601_Acceso_RetornaACCESO_DUPLICADO()
    {
        var (service, uow) = BuildAccesoService();
        var unico = CrearSqlException(2601, MensajeUniqueAccesos);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("update", unico));

        var result = await service.AsignarAccesoAsync(DtoAcceso());

        Assert.True(result.IsFailure);
        Assert.Equal("ACCESO_DUPLICADO", result.Error!.Code);
        Assert.Contains("ya tiene un acceso activo", result.Error.Message);
    }

    // T3 — Duplicado 2627 mencionando "Accesos" → Failure ACCESO_DUPLICADO (no excepción).
    [Fact]
    public async Task T3_DuplicadoSqlError2627_Acceso_RetornaACCESO_DUPLICADO()
    {
        var (service, uow) = BuildAccesoService();
        var unico = CrearSqlException(2627, MensajeUniqueAccesos);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("update", unico));

        var result = await service.AsignarAccesoAsync(DtoAcceso());

        Assert.True(result.IsFailure);
        Assert.Equal("ACCESO_DUPLICADO", result.Error!.Code);
    }

    // T4 — Violación 2601 de OTRA tabla (no "Accesos") → NO se convierte en ACCESO_DUPLICADO; se propaga.
    [Fact]
    public async Task T4_DuplicadoDeOtraTabla_SePropagaComoDbUpdateException()
    {
        var (service, uow) = BuildAccesoService();
        var otro = CrearSqlException(2601, MensajeUniqueOtraTabla);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("update", otro));

        await Assert.ThrowsAsync<DbUpdateException>(() => service.AsignarAccesoAsync(DtoAcceso()));
    }

    // T5 — DbUpdateException con inner genérico (no SqlException) → se propaga.
    [Fact]
    public async Task T5_ExcepcionNoSql_SePropagaComoDbUpdateException()
    {
        var (service, uow) = BuildAccesoService();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("update", new InvalidOperationException("no sql")));

        await Assert.ThrowsAsync<DbUpdateException>(() => service.AsignarAccesoAsync(DtoAcceso()));
    }

    // T6 — AccesoConfiguration: modelo EF sin trigger fantasma + índice único (IdUsuario, IdApp, IdRol).
    [Fact]
    public void T6_AccesoConfiguration_SinTriggerFantasmaYConIndiceUnico()
    {
        var ctx = CrearContext();
        var entityType = ctx.Model.FindEntityType(typeof(Acceso));
        Assert.NotNull(entityType);

        var annotationTrigger = entityType!.GetAnnotations()
            .Any(a => a.Name.Contains("Trigger", StringComparison.OrdinalIgnoreCase));
        Assert.False(annotationTrigger, "El modelo EF no debe declarar triggers sobre Acceso (removido en A1)");

        var tieneIndiceUnico = entityType.GetIndexes().Any(i =>
            i.IsUnique && i.Properties.Select(p => p.Name).ToHashSet()
                .SetEquals(new[] { nameof(Acceso.IdUsuario), nameof(Acceso.IdApp), nameof(Acceso.IdRol) }));

        Assert.True(tieneIndiceUnico, "Debe existir el índice único (IdUsuario, IdApp, IdRol)");
    }

    private static (ConfProvIdenService service, Mock<IUnitOfWorkAsync> uow, Mock<IEncryptionService> encryption)
        BuildConfProvIdenService(ConfProvIden entidad)
    {
        var ctx = CrearContext();
        var repo = new ConfProvIdenRepository(ctx);
        ctx.Set<ConfProvIden>().Add(entidad);
        ctx.SaveChanges();

        var encryption = new Mock<IEncryptionService>();
        encryption.Setup(e => e.Encrypt(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns<string, string?>((plain, _) => $"enc:{plain}");

        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map(It.IsAny<ActualizarConfProvIdenDto>(), It.IsAny<ConfProvIden>()))
            .Callback<ActualizarConfProvIdenDto, ConfProvIden>((src, dest) =>
            {
                if (src.ClientSecret is not null) dest.ClientSecret = src.ClientSecret;
                if (src.Callback is not null) dest.Callback = src.Callback;
                if (src.ClientId is not null) dest.ClientId = src.ClientId;
            });
        mapper.Setup(m => m.Map<ConfProvIdenDto>(It.IsAny<ConfProvIden>()))
            .Returns((ConfProvIden e) => new ConfProvIdenDto
            {
                Id = e.Id,
                IdTenant = e.IdTenant,
                IdProvIden = e.IdProvIden,
                ClientId = e.ClientId,
                Callback = e.Callback,
                Estado = e.Estado,
                Activo = e.Activo
            });

        var uow = new Mock<IUnitOfWorkAsync>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var httpAccessor = new Mock<IHttpContextAccessor>();
        httpAccessor.SetupGet(x => x.HttpContext).Returns((HttpContext?)null);

        var service = new ConfProvIdenService(repo, uow.Object, mapper.Object, encryption.Object,
            Mock.Of<IEmailQueue>(), NullLogger<ConfProvIdenService>.Instance,
            Mock.Of<IAuditoriaPwdService>(), httpAccessor.Object);
        return (service, uow, encryption);
    }

    // T7 — ClientSecret " nueva clave " → se cifra con Trim ("nueva clave").
    [Fact]
    public async Task T7_Actualizar_ClientSecretConEspacios_CifraConTrim()
    {
        var entidad = ConfProvIden.Crear(2, 1, "cid", "secret-anterior", "https://cb.local");
        entidad.Id = 7;
        var (service, _, encryption) = BuildConfProvIdenService(entidad);

        var result = await service.ActualizarAsync(7, new ActualizarConfProvIdenDto
        {
            ClientSecret = "  nueva clave  "
        });

        Assert.True(result.IsSuccess, $"Esperaba Success; error: {result.Error?.Code}");
        encryption.Verify(e => e.Encrypt("nueva clave", "ConfProvIden"), Times.Once);
    }

    // T8a — ClientSecret null/whitespace → NO se cifra (se conserva el valor existente).
    [Fact]
    public async Task T8_Actualizar_ClientSecretNulo_NoCifra()
    {
        var entidad = ConfProvIden.Crear(2, 1, "cid", "secret-anterior", "https://cb.local");
        entidad.Id = 8;
        var (service, _, encryption) = BuildConfProvIdenService(entidad);

        var result = await service.ActualizarAsync(8, new ActualizarConfProvIdenDto { ClientSecret = null });

        Assert.True(result.IsSuccess);
        encryption.Verify(e => e.Encrypt(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    // T8b — Validator ActualizarConfProvIdenValidator: Callback/RedirectUri null-safe (fix CS8602) + regla https.
    [Fact]
    public async Task T8b_Validator_Actualizar_NullSafetyYReglaHttps()
    {
        var validator = new ActualizarConfProvIdenValidator();

        var nulos = await validator.ValidateAsync(new ActualizarConfProvIdenDto { Callback = null, RedirectUri = null });
        Assert.True(nulos.IsValid, "Callback/RedirectUri null no deben producir errores ni NRE");

        var noHttps = await validator.ValidateAsync(new ActualizarConfProvIdenDto
        {
            Callback = "http://cb.local",
            RedirectUri = "http://redir.local"
        });
        Assert.False(noHttps.IsValid);
        Assert.Contains(noHttps.Errors, e => e.ErrorMessage.Contains("https://") && !e.PropertyName.Equals("Callback") || e.PropertyName == "Callback");
        Assert.Contains(noHttps.Errors, e => e.PropertyName == "Callback");
        Assert.Contains(noHttps.Errors, e => e.PropertyName == "RedirectUri");
    }

    // T8c — Validator CrearConfProvIdenValidator: reglas Google (https callback + clientId dominio + scopes).
    [Fact]
    public async Task T8c_Validator_CrearGoogle_ReglasGoogle()
    {
        var provIden = new Mock<IProvIdenService>();
        provIden
            .Setup(s => s.ObtenerPorCodigoAsync("GOOGLE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProvIdenDto?>.Success(new ProvIdenDto { Id = 1, Codigo = "GOOGLE" }, allowNull: true));
        var validator = new CrearConfProvIdenValidator(provIden.Object);

        var valido = await validator.ValidateAsync(new CrearConfProvIdenDto
        {
            IdTenant = 2,
            IdProvIden = 1,
            ClientId = "x.apps.googleusercontent.com",
            ClientSecret = "s",
            Callback = "https://cb.local",
            Scopes = "openid profile email"
        });
        Assert.True(valido.IsValid, string.Join("; ", valido.Errors.Select(e => e.ErrorMessage)));

        var invalido = await validator.ValidateAsync(new CrearConfProvIdenDto
        {
            IdTenant = 2,
            IdProvIden = 1,
            ClientId = "x.apps.googleusercontent.com",
            ClientSecret = "s",
            Callback = "http://cb.local",
            Scopes = "openid"
        });
        Assert.False(invalido.IsValid);
        Assert.Contains(invalido.Errors, e => e.ErrorMessage.Contains("https://"));
        Assert.Contains(invalido.Errors, e => e.ErrorMessage.Contains("profile"));
    }
}