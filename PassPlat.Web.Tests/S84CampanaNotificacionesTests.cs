using System.Net;
using System.Text.Json;
using Bunit;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Web.Layout;

namespace PassPlat.Web.Tests;

/// <summary>
/// S84 · D1 — la campana de la barra muestra las notificaciones no leidas reales del usuario
/// de la sesion (las mismas de /notificaciones) y solo recarga al abrirse.
/// </summary>
public class S84CampanaNotificacionesTests : WebTestBase
{
    private const string Ruta = "/api/notificaciones/no-leidas/7";

    [Fact]
    public async Task Sin_avisos_no_muestra_contador_y_dice_que_no_hay()
    {
        await AutenticarAsync(7, 1, "sistema", "NOTIFICACIONES_VER");
        Handler.Responder(Ruta, "[]");

        var cut = RenderAbierta();

        cut.WaitForAssertion(() => Assert.Contains("No tienes notificaciones sin leer", cut.Markup));
        Assert.Empty(cut.FindAll(".mud-badge"));
    }

    [Fact]
    public async Task Con_dos_no_leidas_muestra_contador_y_sus_asuntos()
    {
        await AutenticarAsync(7, 1, "sistema", "NOTIFICACIONES_VER");
        Handler.Responder(Ruta, Avisos("A1", "A2"));

        var cut = RenderAbierta();

        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find(".mud-badge").TextContent.Trim()));
        Assert.Contains("A1", cut.Markup);
        Assert.Contains("A2", cut.Markup);
        Assert.Contains("2 sin leer", cut.Markup);
    }

    [Fact]
    public async Task Pide_las_no_leidas_del_usuario_de_la_sesion()
    {
        await AutenticarAsync(7, 1, "sistema", "NOTIFICACIONES_VER");
        Handler.Responder(Ruta, "[]");

        var cut = RenderAbierta();

        cut.WaitForAssertion(() => Assert.Contains("GET " + Ruta, Handler.Peticiones));
    }

    [Fact]
    public async Task Sin_permiso_no_dibuja_la_campana_ni_llama_a_la_API()
    {
        await AutenticarAsync(7, 1, "sistema", "SIN_PERMISOS_DE_NOTIFICACIONES");
        Handler.Responder(Ruta, Avisos("A1"));

        var cut = RenderAbierta();

        Assert.Equal("", cut.Markup.Trim());
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("/api/notificaciones"));
    }

    [Fact]
    public async Task Si_la_API_falla_avisa_y_no_muestra_contador()
    {
        await AutenticarAsync(7, 1, "sistema", "NOTIFICACIONES_VER");
        Handler.ResponderConError(Ruta, HttpStatusCode.InternalServerError, "{}");

        var cut = RenderAbierta();

        cut.WaitForAssertion(() => Assert.Contains("No se pudieron cargar las notificaciones", cut.Markup));
        Assert.Empty(cut.FindAll(".mud-badge"));
    }

    [Fact]
    public async Task Muestra_como_maximo_cinco_avisos()
    {
        await AutenticarAsync(7, 1, "sistema", "NOTIFICACIONES_VER");
        Handler.Responder(Ruta, Avisos("A1", "A2", "A3", "A4", "A5", "A6", "A7"));

        var cut = RenderAbierta();

        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll(".notif-item").Count));
        Assert.Equal("7", cut.Find(".mud-badge").TextContent.Trim());
    }

    [Fact]
    public async Task Ver_todas_invoca_OnVerTodas()
    {
        await AutenticarAsync(7, 1, "sistema", "NOTIFICACIONES_VER");
        Handler.Responder(Ruta, "[]");
        var llamadas = 0;

        var cut = Render<CampanaNotificaciones>(p => p
            .Add(x => x.Abierto, true)
            .Add(x => x.OnVerTodas, () => llamadas++));
        cut.Find(".notif-view-all").Click();

        Assert.Equal(1, llamadas);
    }

    [Fact]
    public async Task Al_abrir_recarga_y_los_demas_renders_no()
    {
        await AutenticarAsync(7, 1, "sistema", "NOTIFICACIONES_VER");
        Handler.Responder(Ruta, "[]");

        var cut = Render<CampanaNotificaciones>(p => p.Add(x => x.Abierto, false));
        cut.WaitForAssertion(() => Assert.Equal(1, Pedidas()));

        cut.Render(p => p.Add(x => x.Abierto, false));
        Assert.Equal(1, Pedidas());

        cut.Render(p => p.Add(x => x.Abierto, true));
        cut.WaitForAssertion(() => Assert.Equal(2, Pedidas()));
    }

    private IRenderedComponent<CampanaNotificaciones> RenderAbierta() =>
        Render<CampanaNotificaciones>(p => p.Add(x => x.Abierto, true));

    private int Pedidas() => Handler.Peticiones.Count(p => p == "GET " + Ruta);

    private static string Avisos(params string[] asuntos) =>
        JsonSerializer.Serialize(asuntos.Select((a, i) => new NotificacionDto
        {
            Id = i + 1,
            IdUsuario = 7,
            IdTenant = 1,
            TipoNotif = "INFO",
            Asunto = a,
            FecCrea = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc)
        }));
}
