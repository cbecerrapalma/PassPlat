using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CBP.Acceso;
using CBP.Acceso.Dto;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Client;
using PassPlat.Client.Services;

namespace PassPlat.Aplicacion.Tests.Tests.Catalogo;

/// <summary>
/// S99 D6 — SDK PassPlat.Client (<see cref="PassPlatCatalogoCliente"/>) contra una API PassPlat simulada
/// (HttpMessageHandler falso, sin red ni BD). App genérica DEMO.
/// </summary>
public sealed class PassPlatCatalogoClienteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string RespuestaPlan = """
        {"codigoApp":"DEMO","simulado":true,"huellaRemota":"HR1","sobrantes":["DEMO.CONFIG"],"operaciones":[
          {"tipo":"CrearModulo","codigo":"DEMO.VENTAS","nombre":"Ventas","codigoModulo":"DEMO"},
          {"tipo":"AsociarModulo","codigo":"DEMO.VENTAS","nombre":"Ventas"},
          {"tipo":"CrearPermiso","codigo":"DEMO_VENTAS_PRINCIPAL_VER","nombre":"Ventas: leer","codigoModulo":"DEMO.VENTAS.PRINCIPAL"},
          {"tipo":"MoverPermiso","codigo":"DEMO_VENTA_VER","nombre":"Venta: ver","codigoModulo":"DEMO.VENTAS","detalle":null}]}
        """;

    /// <summary>API PassPlat falsa: registra cada petición y responde con las respuestas encoladas (o el plan por defecto).</summary>
    private sealed class ApiFalsa : HttpMessageHandler
    {
        public readonly List<(string Metodo, string Ruta, string? Token, string? Cuerpo)> Peticiones = [];
        public readonly Queue<(HttpStatusCode Estado, string Json)> Respuestas = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var cuerpo = req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
            Peticiones.Add((req.Method.Method, req.RequestUri!.AbsolutePath.TrimStart('/'), req.Headers.Authorization?.Parameter, cuerpo));
            var (estado, json) = Respuestas.Count > 0 ? Respuestas.Dequeue() : (HttpStatusCode.OK, RespuestaPlan);
            return new HttpResponseMessage(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static (PassPlatCatalogoCliente Cliente, ApiFalsa Api) Cliente(string token = "tok-1")
    {
        var api = new ApiFalsa();
        var opciones = new OpcionesPassPlatAcceso { BaseUrl = "https://passplat.test/", ObtenerToken = _ => token };
        var http = new HttpClient(api) { BaseAddress = new Uri(opciones.BaseUrl) };
        return (new PassPlatCatalogoCliente(http, opciones, new ServiceCollection().BuildServiceProvider()), api);
    }

    private static ManifiestoAcceso Manifiesto() => new()
    {
        AppCodigo = "DEMO",
        Huella = "H-MANIFIESTO",
        Areas =
        [
            new() { Codigo = "DEMO", Nombre = "Demo", Orden = 0 },
            new() { Codigo = "DEMO.VENTAS", Nombre = "Ventas", Orden = 1, Modulos =
            [
                new ModuloAcceso { Codigo = "DEMO.VENTAS.PRINCIPAL", Nombre = "Ventas", Ruta = "/ventas", Icono = "Star", Orden = 1, CodigoPadre = "DEMO.VENTAS", EsVisibleMenu = true }
            ]}
        ],
        PermisosCatalogo =
        [
            new() { Codigo = "DEMO_VENTAS_PRINCIPAL_VER", Nombre = "Ventas: ver", CodigoModulo = "DEMO.VENTAS.PRINCIPAL", Orden = 1 },
            new() { Codigo = "DEMO_VENTAS_PRINCIPAL_ANULAR", Nombre = "Ventas: anular", CodigoModulo = "DEMO.VENTAS.PRINCIPAL", Orden = 1 },
            new() { Codigo = "DEMO_VENTA_VER", Nombre = "Venta: ver", CodigoModulo = "DEMO.VENTAS", Orden = 1 },
        ]
    };

    [Fact]
    public async Task Simular_envia_catalogo_con_token_y_mapea_plan()
    {
        var (cliente, api) = Cliente("tok-simular");
        var plan = await cliente.SimularAsync(Manifiesto(), Ct);

        var (metodo, ruta, token, cuerpo) = Assert.Single(api.Peticiones);
        Assert.Equal(("POST", "api/apps/DEMO/catalogo", "tok-simular"), (metodo, ruta, token));
        var json = JsonNode.Parse(cuerpo!)!;
        Assert.True((bool)json["simular"]!);
        Assert.Null(json["huellaEsperada"]);
        Assert.Equal("SHARED", (string?)json["codigoTipoModulo"]);
        var modulos = json["modulos"]!.AsArray();
        Assert.Equal(["DEMO", "DEMO.VENTAS", "DEMO.VENTAS.PRINCIPAL"], modulos.Select(m => (string?)m!["codigo"]));
        Assert.Equal(101, (int)modulos[1]!["orden"]!);
        var ventana = modulos[2]!;
        Assert.Equal(("/ventas", "DEMO.VENTAS"), ((string?)ventana["ruta"], (string?)ventana["codigoPadre"]));
        Assert.Equal(["ANULAR", "VER"], ventana["acciones"]!.AsArray().Select(a => (string?)a)); // S103: PassPlat crea VER y solo las acciones declaradas
        Assert.Equal(["DEMO_VENTA_VER"], json["permisos"]!.AsArray().Select(p => (string?)p!["codigo"]));

        Assert.Equal(("DEMO", "HR1", "H-MANIFIESTO"), (plan.CodigoApp, plan.HuellaRemota, plan.HuellaManifiesto));
        Assert.Equal(["DEMO.CONFIG"], plan.Sobrantes);
        Assert.Equal([TipoOperacionAcceso.CrearModulo, TipoOperacionAcceso.AsociarModulo, TipoOperacionAcceso.CrearPermiso, TipoOperacionAcceso.MoverPermiso],
            plan.Operaciones.Select(o => o.Tipo));
        Assert.Equal(("DEMO.VENTAS", "Ventas", "DEMO"), (plan.Operaciones[0].Modulo!.Codigo, plan.Operaciones[0].Modulo!.Nombre, plan.Operaciones[0].Modulo!.CodigoPadre));
        Assert.Equal("DEMO.VENTAS.PRINCIPAL", plan.Operaciones[2].Permiso!.CodigoModulo);
        Assert.Equal("DEMO.VENTAS", plan.Operaciones[3].Permiso!.CodigoModulo);
        Assert.Equal(1, plan.ModulosACrear);
        Assert.Equal(1, plan.PermisosAMover);
    }

    [Fact]
    public async Task Inyectar_envia_huella_y_sobrantes()
    {
        var (cliente, api) = Cliente();
        var resultado = await cliente.InyectarAsync(Manifiesto(), "HR1", ["DEMO.CONFIG"], Ct);

        var json = JsonNode.Parse(Assert.Single(api.Peticiones).Cuerpo!)!;
        Assert.False((bool)json["simular"]!);
        Assert.Equal("HR1", (string?)json["huellaEsperada"]);
        Assert.Equal(["DEMO.CONFIG"], json["desactivarSobrantes"]!.AsArray().Select(s => (string?)s));
        Assert.True(resultado.Exito);
        Assert.Equal(4, resultado.Aplicadas.Count);
        await Assert.ThrowsAsync<ArgumentException>(() => cliente.InyectarAsync(Manifiesto(), "", [], Ct));
    }

    [Fact]
    public async Task Conflicto_409_informa_estado()
    {
        var (cliente, api) = Cliente();
        api.Respuestas.Enqueue((HttpStatusCode.Conflict,
            """{"title":"Conflict","detail":"El estado de PassPlat cambió desde que se generó el plan.","status":409,"errorCode":"CATALOGO_ESTADO_CAMBIO"}"""));
        var conflicto = await Assert.ThrowsAsync<ErrorProveedorAccesoException>(() => cliente.InyectarAsync(Manifiesto(), "VIEJA", [], Ct));
        Assert.Equal(409, conflicto.EstadoHttp);
        Assert.Contains("CATALOGO_ESTADO_CAMBIO", conflicto.Message);
        Assert.Contains("cambió", conflicto.Message);

        api.Respuestas.Enqueue((HttpStatusCode.BadRequest,
            """{"title":"Bad Request","detail":"El catálogo no es válido: 1 error(es).","status":400,"errorCode":"CATALOGO_INVALIDO","errors":{"e01":"Módulo repetido: DEMO.X."}}"""));
        var invalido = await Assert.ThrowsAsync<ErrorProveedorAccesoException>(() => cliente.SimularAsync(Manifiesto(), Ct));
        Assert.Equal(400, invalido.EstadoHttp);
        Assert.Contains("Módulo repetido: DEMO.X.", invalido.Message);
    }

    [Fact]
    public async Task Error_401_403_sin_reintento()
    {
        var (cliente, api) = Cliente();
        foreach (var estado in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError })
        {
            var antes = api.Peticiones.Count;
            api.Respuestas.Enqueue((estado, """{"title":"Error","detail":"rechazado"}"""));
            var ex = await Assert.ThrowsAsync<ErrorProveedorAccesoException>(() => cliente.SimularAsync(Manifiesto(), Ct));
            Assert.Equal((int)estado, ex.EstadoHttp);
            Assert.Equal(antes + 1, api.Peticiones.Count); // un POST, sin reintentos
        }
    }
}
