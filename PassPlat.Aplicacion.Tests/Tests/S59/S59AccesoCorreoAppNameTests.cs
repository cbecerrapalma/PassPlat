using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S59;

/// <summary>
/// S59 · K10 (D12) — los correos de rol (plantillas 17 y 18) usan <c>{{AppName}}</c>:
/// el <c>EmailJob</c> encolado lleva el nombre de la app y, si no hay app o no se
/// puede leer, "PassPlat" (el envio nunca se corta).
/// </summary>
public class S59AccesoCorreoAppNameTests
{
    private static PassPlatDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PassPlatDbContext(options);
    }

    private static (AccesoService service, Mock<IEmailQueue> emailQueue) BuildService(
        Mock<IAppRepository>? appRepo = null)
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
                new Usuario { Id = 9, NomUsuario = "u", Nombre = "U", Apellido = "", Email = "u@ejemplo.com" }, allowNull: true));

        var emailQueue = new Mock<IEmailQueue>();
        var uow = new Mock<IUnitOfWorkAsync>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var rolRepo = new Mock<IRolRepository>();
        rolRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Rol>.Success(new Rol { Id = 3, Nombre = "Editor", Activo = true, IdTenant = 2 }));

        var usuarioTenantRepo = new Mock<IUsuarioTenantRepository>();
        usuarioTenantRepo
            .Setup(r => r.ObtenerActivoPorTenantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UsuarioTenant?>.Success(
                new UsuarioTenant
                {
                    Id = 77,
                    IdUsuario = 9,
                    IdTenant = 2,
                    Activo = true,
                    Tenant = new Tenant { Id = 2, Codigo = "ABARROTES", EsSistema = false }
                }, allowNull: true));

        var service = new AccesoService(
            repo,
            mapper.Object,
            usuarioRepo.Object,
            rolRepo.Object,
            usuarioTenantRepo.Object,
            appRepo?.Object ?? Mock.Of<IAppRepository>(),
            emailQueue.Object,
            NullLogger<AccesoService>.Instance,
            uow.Object,
            Mock.Of<IHttpContextAccessor>());

        return (service, emailQueue);
    }

    private static AppDto_ App(string nombre) => new() { Id = 1, Nombre = nombre };

    /// <summary>Dto minimo para el mock (evita depender de la entidad de dominio).</summary>
    private sealed class AppDto_
    {
        public int Id { get; init; }
        public string Nombre { get; init; } = "";
    }

    [Fact]
    public async Task El_correo_de_rol_lleva_el_nombre_de_la_app()
    {
        var appRepo = new Mock<IAppRepository>();
        appRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<App>.Success(new App { Id = 1, Nombre = "Tienda Online" }));

        var (service, emailQueue) = BuildService(appRepo);

        await service.AsignarAccesoAsync(new AsignarAccesoDto
        {
            IdUsuario = 9,
            IdTenant = 2,
            IdApp = 1,
            IdRol = 3
        }, TestContext.Current.CancellationToken);

        emailQueue.Verify(q => q.EnqueueAsync(
            It.Is<EmailJob>(j => j.Extra.ContainsKey("AppName") && (string?)j.Extra["AppName"] == "Tienda Online"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_app_el_correo_lleva_PassPlat()
    {
        var appRepo = new Mock<IAppRepository>();
        appRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<App>.Success(null!, allowNull: true));

        var (service, emailQueue) = BuildService(appRepo);

        await service.AsignarAccesoAsync(new AsignarAccesoDto
        {
            IdUsuario = 9,
            IdTenant = 2,
            IdApp = 1,
            IdRol = 3
        }, TestContext.Current.CancellationToken);

        emailQueue.Verify(q => q.EnqueueAsync(
            It.Is<EmailJob>(j => j.Extra.ContainsKey("AppName") && (string?)j.Extra["AppName"] == "PassPlat"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Si_la_app_no_se_puede_leer_el_correo_lleva_PassPlat_y_no_falla()
    {
        var appRepo = new Mock<IAppRepository>();
        appRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fallo de lectura"));

        var (service, emailQueue) = BuildService(appRepo);

        var result = await service.AsignarAccesoAsync(new AsignarAccesoDto
        {
            IdUsuario = 9,
            IdTenant = 2,
            IdApp = 1,
            IdRol = 3
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, $"Esperaba Success; error: {result.Error?.Code}");
        emailQueue.Verify(q => q.EnqueueAsync(
            It.Is<EmailJob>(j => j.Extra.ContainsKey("AppName") && (string?)j.Extra["AppName"] == "PassPlat"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
