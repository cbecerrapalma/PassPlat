using System.Net;
using System.Text;
using CBP.Acceso;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Client;
using PassPlat.Client.Services;

namespace PassPlat.Aplicacion.Tests.Tests.Catalogo;

/// <summary>S100 D6 — SDK PassPlat.Client (<see cref="PassPlatMenuCliente"/>) contra <c>GET api/modulos/menu</c> simulado (sin red). App genérica DEMO.</summary>
public sealed class PassPlatMenuClienteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Menu = """
        [{"codigo":"DEMO","nombre":"Demo","descripcion":null,"ruta":null,"icono":null,"orden":0,"esVisibleMenu":true,"hijos":[
          {"codigo":"DEMO.VENTAS","nombre":"Ventas","orden":101,"esVisibleMenu":true,"hijos":[
            {"codigo":"DEMO.VENTAS.PRINCIPAL","nombre":"Ventas","ruta":"/ventas","icono":"Receipt","orden":1,"esVisibleMenu":true,"hijos":[]}]}]}]
        """;

    private sealed class ApiFalsa : HttpMessageHandler
    {
        public readonly List<(string Metodo, string Ruta, string? Token)> Peticiones = [];
        public HttpStatusCode Estado { get; set; } = HttpStatusCode.OK;
        public string Json { get; set; } = Menu;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Peticiones.Add((req.Method.Method, req.RequestUri!.AbsolutePath.TrimStart('/'), req.Headers.Authorization?.Parameter));
            return Task.FromResult(new HttpResponseMessage(Estado) { Content = new StringContent(Json, Encoding.UTF8, "application/json") });
        }
    }

    private static (PassPlatMenuCliente Cliente, ApiFalsa Api) Cliente(string? token = "tok-1")
    {
        var api = new ApiFalsa();
        var opciones = new OpcionesPassPlatAcceso { BaseUrl = "https://passplat.test/", ObtenerToken = _ => token };
        var http = new HttpClient(api) { BaseAddress = new Uri(opciones.BaseUrl) };
        return (new PassPlatMenuCliente(http, opciones, new ServiceCollection().BuildServiceProvider()), api);
    }

    [Fact]
    public async Task Pide_api_modulos_menu_con_el_token_del_usuario_y_mapea_el_arbol()
    {
        var (cliente, api) = Cliente();

        var menu = await cliente.ObtenerMenuAsync(Ct);

        var peticion = Assert.Single(api.Peticiones);
        Assert.Equal(("GET", "api/modulos/menu", "tok-1"), peticion);
        var raiz = Assert.Single(menu);
        Assert.Equal("DEMO", raiz.Codigo);
        var hoja = Assert.Single(Assert.Single(raiz.Hijos).Hijos);
        Assert.Equal(("DEMO.VENTAS.PRINCIPAL", "/ventas", "Receipt", 1, true), (hoja.Codigo, hoja.Ruta, hoja.Icono, hoja.Orden, hoja.EsVisibleMenu));
    }

    [Fact]
    public async Task Estado_401_sale_como_ErrorProveedorAccesoException_con_estado()
    {
        var (cliente, api) = Cliente();
        api.Estado = HttpStatusCode.Unauthorized;
        var error = await Assert.ThrowsAsync<ErrorProveedorAccesoException>(() => cliente.ObtenerMenuAsync(Ct));
        Assert.Equal(401, error.EstadoHttp);
        Assert.DoesNotContain("tok-1", error.Message);

        var (sinToken, apiSin) = Cliente(token: null);
        var sin = await Assert.ThrowsAsync<ErrorProveedorAccesoException>(() => sinToken.ObtenerMenuAsync(Ct));
        Assert.Equal(401, sin.EstadoHttp);
        Assert.Empty(apiSin.Peticiones); // sin token no se llama
    }
}
