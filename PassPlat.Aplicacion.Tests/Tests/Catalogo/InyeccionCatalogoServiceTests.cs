using CBP.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.Catalogo;

/// <summary>
/// S99 D1–D3 — Inyección del catálogo de una app (contrato genérico) sobre una app de ejemplo DEMO.
/// InMemory, patrón de <c>S83MenuModulosTests</c>: la app, el tipo SHARED y las filas existentes se siembran en el contexto.
/// </summary>
public class InyeccionCatalogoServiceTests
{
    private const int IdDemo = 12;
    private const int IdOtra = 1;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PassPlatDbContext CrearContext() => new(new DbContextOptionsBuilder<PassPlatDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<PassPlatDbContext> SembrarAsync(Action<PassPlatDbContext>? extra = null)
    {
        var ctx = CrearContext();
        ctx.Set<App>().AddRange(new App { Id = IdDemo, Codigo = "DEMO", Nombre = "Demo", Activa = true },
            new App { Id = IdOtra, Codigo = "OTRA", Nombre = "Otra", Activa = true });
        ctx.Set<TipoModulo>().Add(new TipoModulo { Id = 3, Codigo = "SHARED", Nombre = "Compartido" });
        extra?.Invoke(ctx);
        await ctx.SaveChangesAsync(Ct);
        return ctx;
    }

    private static InyeccionCatalogoService Servicio(PassPlatDbContext ctx)
    {
        var apps = new Mock<IAppRepository>();
        apps.Setup(a => a.ObtenerPorCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string codigo, CancellationToken _) => Result<App?>.Success(ctx.Set<App>().FirstOrDefault(a => a.Codigo == codigo), allowNull: true));
        return new InyeccionCatalogoService(apps.Object, new TipoModuloRepository(ctx), new ModuloRepository(ctx), new PermisoRepository(ctx),
            new AppModuloRepository(ctx), NullLogger<InyeccionCatalogoService>.Instance);
    }

    private static ModuloCatalogoDto M(string codigo, string nombre, string? padre, int orden, string? ruta = null, params string[] acciones) => new()
    {
        Codigo = codigo, Nombre = nombre, CodigoPadre = padre, Orden = orden, Ruta = ruta, Icono = "Star", EsVisibleMenu = true, Acciones = [.. acciones]
    };

    /// <summary>DEMO → DEMO.VENTAS → ventanas Pedidos (acción ANULAR) y Cobros; un permiso suelto en el área.</summary>
    private static InyeccionCatalogoDto Catalogo(Action<InyeccionCatalogoDto>? ajustar = null)
    {
        var c = new InyeccionCatalogoDto
        {
            Modulos =
            [
                M("DEMO", "Demo", null, 0),
                M("DEMO.VENTAS", "Ventas", "DEMO", 101),
                M("DEMO.VENTAS.PEDIDOS", "Pedidos", "DEMO.VENTAS", 1, "/pedidos", "ANULAR"),
                M("DEMO.VENTAS.COBROS", "Cobros", "DEMO.VENTAS", 2, "/cobros"),
            ],
            Permisos = [new PermisoCatalogoDto { Codigo = "DEMO_VENTA_VER", Nombre = "Venta: ver", CodigoModulo = "DEMO.VENTAS" }]
        };
        ajustar?.Invoke(c);
        return c;
    }

    private static Task<Result<ResultadoInyeccionCatalogoDto>> Simular(InyeccionCatalogoService s, InyeccionCatalogoDto c, bool sistema = true, int? idApp = IdDemo)
    {
        c.Simular = true;
        return s.InyectarAsync("DEMO", c, sistema, idApp, Ct);
    }

    /// <summary>Simula, aplica con la huella del plan y guarda: lo que hace el controlador.</summary>
    private static async Task<ResultadoInyeccionCatalogoDto> AplicarAsync(PassPlatDbContext ctx, InyeccionCatalogoDto c, params string[] desactivar)
    {
        var s = Servicio(ctx);
        var plan = await Simular(s, c);
        Assert.True(plan.IsSuccess, plan.IsFailure ? plan.Error!.Message : "");
        c.Simular = false;
        c.HuellaEsperada = plan.Value.HuellaRemota;
        c.DesactivarSobrantes = [.. desactivar];
        var r = await s.InyectarAsync("DEMO", c, true, IdDemo, Ct);
        Assert.True(r.IsSuccess, r.IsFailure ? r.Error!.Message : "");
        await ctx.SaveChangesAsync(Ct);
        return r.Value;
    }

    private static int Cuenta(ResultadoInyeccionCatalogoDto r, string tipo) => r.Operaciones.Count(o => o.Tipo == tipo);

    [Fact]
    public async Task Simular_no_escribe()
    {
        await using var ctx = await SembrarAsync();
        var r = await Simular(Servicio(ctx), Catalogo());

        Assert.True(r.IsSuccess);
        Assert.True(r.Value.Simulado);
        Assert.Equal(4, Cuenta(r.Value, "CrearModulo"));
        Assert.False(ctx.ChangeTracker.HasChanges());
        await ctx.SaveChangesAsync(Ct);
        Assert.Equal(0, await ctx.Set<Modulo>().CountAsync(Ct));
        Assert.Equal(0, await ctx.Set<Permiso>().CountAsync(Ct));
        Assert.Equal(0, await ctx.Set<AppModulo>().CountAsync(Ct));
    }

    [Fact]
    public async Task Primera_inyeccion_crea_asocia_y_permisos_declarados()
    {
        await using var ctx = await SembrarAsync();
        var r = await AplicarAsync(ctx, Catalogo());

        Assert.False(r.Simulado);
        Assert.Equal(4, Cuenta(r, "CrearModulo"));
        Assert.Equal(4, Cuenta(r, "AsociarModulo"));
        Assert.Equal(4, Cuenta(r, "CrearPermiso")); // S103: VER de las 2 ventanas + ANULAR + 1 suelto (antes 10, con CRUD fijo)
        var pedidos = await ctx.Set<Modulo>().SingleAsync(m => m.Codigo == "DEMO.VENTAS.PEDIDOS", Ct);
        var area = await ctx.Set<Modulo>().SingleAsync(m => m.Codigo == "DEMO.VENTAS", Ct);
        Assert.Equal(area.Id, pedidos.IdModuloPadre);
        Assert.Equal(3, pedidos.IdTipoModulo);
        Assert.Equal(4, await ctx.Set<AppModulo>().CountAsync(a => a.IdApp == IdDemo && a.Activo, Ct));

        var crud = await ctx.Set<Permiso>().Where(p => p.IdModulo == pedidos.Id).OrderBy(p => p.Orden).ToListAsync(Ct);
        Assert.Equal(["DEMO_VENTAS_PEDIDOS_VER", "DEMO_VENTAS_PEDIDOS_ANULAR"], crud.Select(p => p.Codigo));
        Assert.Equal(["Pedidos: leer", "Pedidos: anular"], crud.Select(p => p.Nombre));
        Assert.Equal<byte[]>([1, 5], crud.Select(p => p.Orden).ToArray());
        Assert.Equal(area.Id, (await ctx.Set<Permiso>().SingleAsync(p => p.Codigo == "DEMO_VENTA_VER", Ct)).IdModulo);
    }

    [Fact]
    public async Task Agrupadores_sin_CRUD()
    {
        await using var ctx = await SembrarAsync();
        await AplicarAsync(ctx, Catalogo());

        var ids = await ctx.Set<Modulo>().Where(m => m.Ruta == null).Select(m => m.Id).ToListAsync(Ct);
        Assert.Equal(2, ids.Count); // la raíz y el área
        var enAgrupadores = await ctx.Set<Permiso>().Where(p => ids.Contains(p.IdModulo)).Select(p => p.Codigo).ToListAsync(Ct);
        Assert.Equal(["DEMO_VENTA_VER"], enAgrupadores); // solo el suelto: ni VER/CREAR/EDITAR/ELIMINAR de la raíz ni del área
    }

    [Fact]
    public async Task Ventana_recibe_VER_y_solo_las_acciones_declaradas()
    {
        await using var ctx = await SembrarAsync();
        var c = Catalogo(x => x.Modulos[3].Acciones = ["VER", "EDITAR", "TRANSFERIR", "ANULAR", "ANULAR"]);
        await AplicarAsync(ctx, c);

        var cobros = await ctx.Set<Modulo>().SingleAsync(m => m.Codigo == "DEMO.VENTAS.COBROS", Ct);
        var permisos = await ctx.Set<Permiso>().Where(p => p.IdModulo == cobros.Id).OrderBy(p => p.Orden).ToListAsync(Ct);
        Assert.Equal(["DEMO_VENTAS_COBROS_VER", "DEMO_VENTAS_COBROS_EDITAR", "DEMO_VENTAS_COBROS_ANULAR", "DEMO_VENTAS_COBROS_TRANSFERIR"],
            permisos.Select(p => p.Codigo)); // S103: ni CREAR ni ELIMINAR, que el catálogo no declara
        Assert.Equal(["Cobros: leer", "Cobros: modificar", "Cobros: anular", "Cobros: transferir"], permisos.Select(p => p.Nombre));
        Assert.Equal<byte[]>([1, 3, 5, 6], permisos.Select(p => p.Orden).ToArray());
    }

    [Fact]
    public async Task Segunda_inyeccion_sin_operaciones()
    {
        await using var ctx = await SembrarAsync();
        await AplicarAsync(ctx, Catalogo());

        var otra = await Simular(Servicio(ctx), Catalogo());
        Assert.True(otra.IsSuccess);
        Assert.Empty(otra.Value.Operaciones);
        Assert.Empty(otra.Value.Sobrantes);
    }

    [Fact]
    public async Task Asociacion_inactiva_se_reactiva()
    {
        await using var ctx = await SembrarAsync();
        await AplicarAsync(ctx, Catalogo());
        var cobros = await ctx.Set<Modulo>().SingleAsync(m => m.Codigo == "DEMO.VENTAS.COBROS", Ct);
        var fila = await ctx.Set<AppModulo>().SingleAsync(a => a.IdApp == IdDemo && a.IdModulo == cobros.Id, Ct);
        fila.Activo = false;
        await ctx.SaveChangesAsync(Ct);

        var plan = await Simular(Servicio(ctx), Catalogo());
        var op = Assert.Single(plan.Value.Operaciones);
        Assert.Equal("AsociarModulo", op.Tipo);
        Assert.Equal("DEMO.VENTAS.COBROS", op.Codigo);
        Assert.Equal("reactivar", op.Detalle);

        await AplicarAsync(ctx, Catalogo());
        Assert.Equal(4, await ctx.Set<AppModulo>().CountAsync(Ct)); // nunca inserta otra fila (UQ_AppsModulos_AppModulo)
        Assert.True((await ctx.Set<AppModulo>().SingleAsync(a => a.IdModulo == cobros.Id, Ct)).Activo);
    }

    [Fact]
    public async Task Permiso_inactivo_se_reactiva()
    {
        await using var ctx = await SembrarAsync();
        await AplicarAsync(ctx, Catalogo());
        var cantidad = await ctx.Set<Permiso>().CountAsync(Ct);
        var permiso = await ctx.Set<Permiso>().SingleAsync(p => p.Codigo == "DEMO_VENTAS_COBROS_VER", Ct);
        permiso.Activo = false;
        await ctx.SaveChangesAsync(Ct);

        var plan = await Simular(Servicio(ctx), Catalogo());
        var op = Assert.Single(plan.Value.Operaciones);
        Assert.Equal("CrearPermiso", op.Tipo);
        Assert.Equal("DEMO_VENTAS_COBROS_VER", op.Codigo);
        Assert.Equal("reactivar", op.Detalle);

        await AplicarAsync(ctx, Catalogo());
        Assert.Equal(cantidad, await ctx.Set<Permiso>().CountAsync(Ct));
        Assert.True((await ctx.Set<Permiso>().SingleAsync(p => p.Codigo == "DEMO_VENTAS_COBROS_VER", Ct)).Activo);
    }

    [Fact]
    public async Task Permiso_suelto_se_mueve()
    {
        await using var ctx = await SembrarAsync();
        await AplicarAsync(ctx, Catalogo());
        var raiz = await ctx.Set<Modulo>().SingleAsync(m => m.Codigo == "DEMO", Ct);

        var movido = Catalogo(c => c.Permisos[0].CodigoModulo = "DEMO");
        var plan = await Simular(Servicio(ctx), movido);
        var op = Assert.Single(plan.Value.Operaciones);
        Assert.Equal("MoverPermiso", op.Tipo);
        Assert.Equal("DEMO_VENTA_VER", op.Codigo);
        Assert.Equal("DEMO", op.CodigoModulo);
        Assert.Equal("Venta: ver", op.Nombre); // nunca renombra

        await AplicarAsync(ctx, Catalogo(c => c.Permisos[0].CodigoModulo = "DEMO"));
        Assert.Equal(raiz.Id, (await ctx.Set<Permiso>().SingleAsync(p => p.Codigo == "DEMO_VENTA_VER", Ct)).IdModulo);
    }

    [Fact]
    public async Task Agrupador_existente_conserva_nombre_y_orden()
    {
        await using var ctx = await SembrarAsync(c =>
        {
            c.Set<Modulo>().AddRange(
                new Modulo { Id = 300, IdTipoModulo = 3, Codigo = "DEMO", Nombre = "Demo remoto", Icono = "Store", Orden = 90, Activo = true },
                new Modulo { Id = 301, IdModuloPadre = 300, IdTipoModulo = 3, Codigo = "DEMO.VENTAS", Nombre = "Ventas remoto", Orden = 92, Activo = true });
        });
        var plan = await Simular(Servicio(ctx), Catalogo());

        Assert.Equal(2, Cuenta(plan.Value, "CrearModulo")); // solo las 2 ventanas
        Assert.Equal(0, Cuenta(plan.Value, "ActualizarModulo"));
        Assert.Equal(4, Cuenta(plan.Value, "AsociarModulo"));

        await AplicarAsync(ctx, Catalogo());
        var raiz = await ctx.Set<Modulo>().SingleAsync(m => m.Id == 300, Ct);
        var area = await ctx.Set<Modulo>().SingleAsync(m => m.Id == 301, Ct);
        Assert.Equal(("Demo remoto", (short)90), (raiz.Nombre, raiz.Orden));
        Assert.Equal(("Ventas remoto", (short)92), (area.Nombre, area.Orden));
        Assert.Equal(301, (await ctx.Set<Modulo>().SingleAsync(m => m.Codigo == "DEMO.VENTAS.PEDIDOS", Ct)).IdModuloPadre);
    }

    [Fact]
    public async Task Sobrante_solo_confirmado_y_sin_borrar()
    {
        await using var ctx = await SembrarAsync(c =>
        {
            c.Set<Modulo>().AddRange(
                new Modulo { Id = 300, IdTipoModulo = 3, Codigo = "DEMO", Nombre = "Demo", Activo = true },
                new Modulo { Id = 309, IdModuloPadre = 300, IdTipoModulo = 3, Codigo = "DEMO.CONFIG", Nombre = "Config", Activo = true });
            c.Set<Permiso>().Add(new Permiso { Id = 900, IdModulo = 309, Codigo = "DEMO_CONFIG_VER", Nombre = "Config: ver", Activo = true });
        });

        var plan = await Simular(Servicio(ctx), Catalogo());
        Assert.Equal(["DEMO.CONFIG", "DEMO_CONFIG_VER"], plan.Value.Sobrantes); // S100 D5b: el módulo y su permiso activo
        Assert.Equal(0, Cuenta(plan.Value, "DesactivarModulo"));

        await AplicarAsync(ctx, Catalogo()); // sin confirmar: sigue activo
        Assert.True((await ctx.Set<Modulo>().SingleAsync(m => m.Id == 309, Ct)).Activo);

        var noSobrante = Catalogo();
        noSobrante.Simular = false;
        noSobrante.HuellaEsperada = (await Simular(Servicio(ctx), Catalogo())).Value.HuellaRemota;
        noSobrante.DesactivarSobrantes = ["DEMO.VENTAS"];
        var invalido = await Servicio(ctx).InyectarAsync("DEMO", noSobrante, true, IdDemo, Ct);
        Assert.True(invalido.IsFailure);
        Assert.Equal("CATALOGO_SOBRANTE_INVALIDO", invalido.Error!.Code);
        Assert.Equal(400, invalido.Error.HttpStatusCode);

        var r = await AplicarAsync(ctx, Catalogo(), "DEMO.CONFIG");
        Assert.Equal(1, Cuenta(r, "DesactivarModulo"));
        var config = await ctx.Set<Modulo>().SingleAsync(m => m.Id == 309, Ct);
        Assert.False(config.Activo); // desactivado, no borrado
        Assert.True((await ctx.Set<Permiso>().SingleAsync(p => p.Id == 900, Ct)).Activo); // sus permisos no se tocan salvo que se confirmen
        Assert.Equal(["DEMO_CONFIG_VER"], (await Simular(Servicio(ctx), Catalogo())).Value.Sobrantes);
    }

    [Fact]
    public async Task Modulo_existente_actualiza_solo_ruta_y_conserva_nombre_icono_orden_y_visibilidad()
    {
        await using var ctx = await SembrarAsync(c =>
        {
            c.Set<Modulo>().AddRange(
                new Modulo { Id = 300, IdTipoModulo = 3, Codigo = "DEMO", Nombre = "Demo", Activo = true },
                new Modulo { Id = 301, IdModuloPadre = 300, IdTipoModulo = 3, Codigo = "DEMO.VENTAS", Nombre = "Ventas", Activo = true },
                new Modulo { Id = 302, IdModuloPadre = 301, IdTipoModulo = 3, Codigo = "DEMO.VENTAS.PEDIDOS", Nombre = "Pedidos del dueño", Ruta = "/viejo",
                    Icono = "Receipt", Orden = 77, EsVisibleMenu = false, Activo = true });
            c.Set<AppModulo>().AddRange(
                new AppModulo { IdApp = IdDemo, IdModulo = 300, Activo = true }, new AppModulo { IdApp = IdDemo, IdModulo = 301, Activo = true },
                new AppModulo { IdApp = IdDemo, IdModulo = 302, Activo = true });
        });

        var plan = await Simular(Servicio(ctx), Catalogo());
        var op = Assert.Single(plan.Value.Operaciones, o => o.Tipo == "ActualizarModulo");
        Assert.Equal("DEMO.VENTAS.PEDIDOS", op.Codigo);
        Assert.Equal("ruta", op.Detalle);

        await AplicarAsync(ctx, Catalogo());
        var pedidos = await ctx.Set<Modulo>().SingleAsync(m => m.Id == 302, Ct);
        Assert.Equal("/pedidos", pedidos.Ruta);
        Assert.Equal(("Pedidos del dueño", "Receipt", (short)77, false), (pedidos.Nombre, pedidos.Icono, pedidos.Orden, pedidos.EsVisibleMenu));
    }

    [Fact]
    public async Task Permiso_activo_no_deseado_es_sobrante_y_solo_se_desactiva_si_se_confirma()
    {
        await using var ctx = await SembrarAsync(c =>
        {
            c.Set<Modulo>().AddRange(
                new Modulo { Id = 300, IdTipoModulo = 3, Codigo = "DEMO", Nombre = "Demo", Activo = true },
                new Modulo { Id = 301, IdModuloPadre = 300, IdTipoModulo = 3, Codigo = "DEMO.VENTAS", Nombre = "Ventas", Activo = true });
            c.Set<Permiso>().AddRange(
                new Permiso { Id = 900, IdModulo = 301, Codigo = "DEMO_VENTA_VER", Nombre = "Venta: ver", Activo = true },
                new Permiso { Id = 901, IdModulo = 301, Codigo = "DEMO_VENTA_ANULAR", Nombre = "Venta: anular", Activo = true });
        });
        await AplicarAsync(ctx, Catalogo()); // DEMO_VENTA_VER sigue deseado; DEMO_VENTA_ANULAR ya no

        var plan = await Simular(Servicio(ctx), Catalogo());
        Assert.Equal(["DEMO_VENTA_ANULAR"], plan.Value.Sobrantes);
        Assert.True((await ctx.Set<Permiso>().SingleAsync(p => p.Id == 901, Ct)).Activo); // sin confirmar: sigue activo

        var r = await AplicarAsync(ctx, Catalogo(), "DEMO_VENTA_ANULAR");
        var op = Assert.Single(r.Operaciones, o => o.Tipo == "DesactivarPermiso");
        Assert.Equal("DEMO_VENTA_ANULAR", op.Codigo);
        Assert.False((await ctx.Set<Permiso>().SingleAsync(p => p.Id == 901, Ct)).Activo); // desactivado, no borrado
        Assert.True((await ctx.Set<Permiso>().SingleAsync(p => p.Id == 900, Ct)).Activo);
        Assert.Empty((await Simular(Servicio(ctx), Catalogo())).Value.Sobrantes);
    }

    [Fact]
    public async Task Accion_que_el_catalogo_deja_de_declarar_es_sobrante_y_se_reactiva_si_vuelve()
    {
        await using var ctx = await SembrarAsync();
        await AplicarAsync(ctx, Catalogo(x => x.Modulos[3].Acciones = ["CREAR", "ELIMINAR"])); // Cobros con el CRUD de antes de S103

        var plan = await Simular(Servicio(ctx), Catalogo()); // Cobros ya no declara acciones: le queda solo VER
        Assert.Empty(plan.Value.Operaciones);
        Assert.Equal(["DEMO_VENTAS_COBROS_CREAR", "DEMO_VENTAS_COBROS_ELIMINAR"], plan.Value.Sobrantes);

        var r = await AplicarAsync(ctx, Catalogo(), "DEMO_VENTAS_COBROS_CREAR", "DEMO_VENTAS_COBROS_ELIMINAR");
        Assert.Equal(2, Cuenta(r, "DesactivarPermiso"));
        Assert.True((await ctx.Set<Permiso>().SingleAsync(p => p.Codigo == "DEMO_VENTAS_COBROS_VER", Ct)).Activo);
        Assert.Empty((await Simular(Servicio(ctx), Catalogo())).Value.Sobrantes);

        var vuelve = await Simular(Servicio(ctx), Catalogo(x => x.Modulos[3].Acciones = ["CREAR"]));
        var op = Assert.Single(vuelve.Value.Operaciones);
        Assert.Equal("CrearPermiso", op.Tipo);
        Assert.Equal("DEMO_VENTAS_COBROS_CREAR", op.Codigo);
        Assert.Equal("reactivar", op.Detalle);
        Assert.Empty(vuelve.Value.Sobrantes);
    }

    [Fact]
    public async Task Huella_distinta_es_conflicto_con_plan()
    {
        await using var ctx = await SembrarAsync();
        var s = Servicio(ctx);
        var c = Catalogo();
        c.HuellaEsperada = "VIEJA";
        var r = await s.InyectarAsync("DEMO", c, true, IdDemo, Ct);

        Assert.True(r.IsFailure);
        Assert.Equal("CATALOGO_ESTADO_CAMBIO", r.Error!.Code);
        Assert.Equal(409, r.Error.HttpStatusCode);
        Assert.False(ctx.ChangeTracker.HasChanges());
        // El plan nuevo que el controlador adjunta al 409: la misma petición simulada.
        var plan = await Simular(s, Catalogo());
        Assert.NotEqual("VIEJA", plan.Value.HuellaRemota);
        Assert.NotEmpty(plan.Value.Operaciones);
        // La huella del plan es la que se acepta al aplicar.
        var ok = Catalogo();
        ok.HuellaEsperada = plan.Value.HuellaRemota;
        Assert.True((await s.InyectarAsync("DEMO", ok, true, IdDemo, Ct)).IsSuccess);
    }

    [Fact]
    public async Task Sin_is_system_prohibido()
    {
        await using var ctx = await SembrarAsync();
        var r = await Simular(Servicio(ctx), Catalogo(), sistema: false);

        Assert.True(r.IsFailure);
        Assert.Equal("CATALOGO_SOLO_SISTEMA", r.Error!.Code);
        Assert.Equal(403, r.Error.HttpStatusCode);
        Assert.False(ctx.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Token_de_otra_app_prohibido()
    {
        await using var ctx = await SembrarAsync();
        var s = Servicio(ctx);
        foreach (int? idApp in new int?[] { IdOtra, null })
        {
            var r = await Simular(s, Catalogo(), idApp: idApp);
            Assert.True(r.IsFailure);
            Assert.Equal("CATALOGO_OTRA_APP", r.Error!.Code);
            Assert.Equal(403, r.Error.HttpStatusCode);
        }
        var inexistente = await s.InyectarAsync("NOEXISTE", Catalogo(c => c.Simular = true), true, IdDemo, Ct);
        Assert.Equal("CATALOGO_APP_NO_EXISTE", inexistente.Error!.Code);
        Assert.Equal(404, inexistente.Error.HttpStatusCode);
    }

    [Fact]
    public async Task Codigo_fuera_del_prefijo_invalido()
    {
        await using var ctx = await SembrarAsync();
        var c = Catalogo(x =>
        {
            x.Modulos.Add(M("OTRA.INTRUSO", "Intruso", "DEMO", 5));
            x.Permisos.Add(new PermisoCatalogoDto { Codigo = "OTRA_COSA_VER", Nombre = "Cosa", CodigoModulo = "DEMO" });
            x.Permisos.Add(new PermisoCatalogoDto { Codigo = "DEMO_X_VER", Nombre = "X", CodigoModulo = "DEMO.NOESTA" });
        });
        var r = await Simular(Servicio(ctx), c);

        Assert.True(r.IsFailure);
        Assert.Equal("CATALOGO_INVALIDO", r.Error!.Code);
        Assert.Equal(400, r.Error.HttpStatusCode);
        var texto = string.Join("|", r.Error.Details!.Values.Select(v => v.ToString()));
        Assert.Contains("OTRA.INTRUSO", texto);
        Assert.Contains("OTRA_COSA_VER", texto);
        Assert.Contains("DEMO.NOESTA", texto);
        Assert.False(ctx.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Modulo_de_otra_app_es_colision()
    {
        await using var ctx = await SembrarAsync(c =>
        {
            c.Set<Modulo>().AddRange(
                new Modulo { Id = 300, IdTipoModulo = 3, Codigo = "DEMO", Nombre = "Demo", Activo = true },
                new Modulo { Id = 301, IdModuloPadre = 300, IdTipoModulo = 3, Codigo = "DEMO.VENTAS", Nombre = "Ventas", Activo = true });
            c.Set<AppModulo>().Add(new AppModulo { Id = 1, IdApp = IdOtra, IdModulo = 301, Activo = true });
        });
        var r = await Simular(Servicio(ctx), Catalogo());

        Assert.True(r.IsFailure);
        Assert.Equal("CATALOGO_COLISION", r.Error!.Code);
        Assert.Equal(400, r.Error.HttpStatusCode);
        Assert.Contains("DEMO.VENTAS", string.Join("|", r.Error.Details!.Values.Select(v => v.ToString())));
        Assert.False(ctx.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Codigo_generado_mayor_a_50_invalido()
    {
        await using var ctx = await SembrarAsync();
        var largo = "DEMO.VENTAS.ADMINISTRACION_GENERAL_DE_COBROS"; // 44 caracteres: el módulo y su _VER (48) caben; el _ELIMINAR que declara (53) no
        Assert.Equal(44, largo.Length);
        var c = Catalogo(x => x.Modulos.Add(M(largo, "Cobros generales", "DEMO.VENTAS", 9, "/cobros-generales", "ELIMINAR")));
        var r = await Simular(Servicio(ctx), c);

        Assert.True(r.IsFailure);
        Assert.Equal("CATALOGO_INVALIDO", r.Error!.Code);
        var textos = r.Error.Details!.Values.Select(v => v.ToString()!).ToList();
        Assert.Contains(textos, t => t.Contains("DEMO_VENTAS_ADMINISTRACION_GENERAL_DE_COBROS_ELIMINAR") && t.Contains("supera 50"));
        Assert.DoesNotContain(textos, t => t.StartsWith("El código 'DEMO.VENTAS.ADMINISTRACION"));
    }
}
