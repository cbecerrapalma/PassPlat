using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using PassPlat.Web.Services;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S55 · K5 (D6/U12) — la sesion sin "Recordarme" se guarda en sessionStorage
/// (sobrevive a F5 en la misma pestana) y con "Recordarme" sigue en localStorage.
/// S55.1 · K1 (D1) — un solo modo de persistencia por sesion: renovar el token,
/// cambiar de tenant o entrar con un proveedor externo conservan el modo, y al
/// guardar en un almacen se borra el otro.
/// </summary>
public class AuthServiceSessionTests : WebTestBase
{
    private const string LoginOk = """
    {
      "accessToken": "token-de-prueba",
      "refreshToken": "refresh-de-prueba",
      "expiresAt": "2026-12-31T00:00:00Z",
      "idUsuario": 1,
      "idTenant": 1,
      "nomUsuario": "sistema",
      "email": "sistema@passplat.app",
      "nombre": "Usuario",
      "apellido": "Sistema",
      "reqCambioPwd": false
    }
    """;

    private readonly RecordingJsRuntime _js = new();

    public AuthServiceSessionTests()
    {
        Services.AddSingleton<IJSRuntime>(_js);
        Handler.Responder("/api/auth/login", LoginOk);
        Handler.Responder("/api/auth/refresh", LoginOk);
        Handler.Responder("/api/auth/switch-tenant/2", LoginOk);
        Handler.Responder("/api/auth/externo/login", LoginOk);
    }

    private async Task IniciarSesionAsync(bool rememberMe)
    {
        var resultado = await Auth.LoginAsync("sistema", "clave", 1, rememberMe, idTenant: 1);

        Assert.True(resultado.Success);
    }

    [Fact]
    public async Task Sin_recordarme_la_sesion_va_a_sessionStorage()
    {
        await IniciarSesionAsync(rememberMe: false);

        Assert.Contains("access_token", _js.ClavesDe("sessionStorage"));
        Assert.Empty(_js.ClavesDe("localStorage"));
        Assert.Equal(ModoPersistenciaSesion.Pestana, Auth.ModoPersistencia);
    }

    [Fact]
    public async Task Con_recordarme_la_sesion_sigue_en_localStorage()
    {
        await IniciarSesionAsync(rememberMe: true);

        Assert.Contains("access_token", _js.ClavesDe("localStorage"));
        Assert.Empty(_js.ClavesDe("sessionStorage"));
        Assert.Equal(ModoPersistenciaSesion.Local, Auth.ModoPersistencia);
    }

    [Fact]
    public async Task Al_guardar_en_un_almacen_se_borra_el_otro()
    {
        await IniciarSesionAsync(rememberMe: false);

        Assert.True(_js.BorradosEn("localStorage") >= 6);
        Assert.Equal(0, _js.BorradosEn("sessionStorage"));
    }

    [Fact]
    public async Task Renovar_el_token_conserva_el_modo_de_pestana()
    {
        await IniciarSesionAsync(rememberMe: false);
        var escriturasLocal = _js.EscriturasEn("localStorage");

        var renovada = await Auth.TryRefreshAsync();

        Assert.True(renovada);
        Assert.Equal(escriturasLocal, _js.EscriturasEn("localStorage"));
        Assert.Contains("access_token", _js.ClavesDe("sessionStorage"));
        Assert.Equal(ModoPersistenciaSesion.Pestana, Auth.ModoPersistencia);
    }

    [Fact]
    public async Task Cambiar_de_tenant_conserva_el_modo_de_pestana()
    {
        await IniciarSesionAsync(rememberMe: false);
        var escriturasLocal = _js.EscriturasEn("localStorage");

        var resultado = await Auth.SwitchTenantAsync(2);

        Assert.True(resultado.Success);
        Assert.Equal(escriturasLocal, _js.EscriturasEn("localStorage"));
        Assert.Contains("access_token", _js.ClavesDe("sessionStorage"));
    }

    [Fact]
    public async Task El_login_externo_sin_eleccion_va_a_sessionStorage()
    {
        var resultado = await Auth.CompleteExternalLoginAsync("code", "code", "https://localhost:7275/signin-callback", 1, 1);

        Assert.True(resultado.Success);
        Assert.Contains("access_token", _js.ClavesDe("sessionStorage"));
        Assert.Empty(_js.ClavesDe("localStorage"));
    }

    // S100 · D3 — el tenant del login externo es obligatorio, sin valor de reserva
    [Fact]
    public void El_login_externo_exige_el_tenant_sin_reserva()
    {
        var idTenant = typeof(AuthService).GetMethod(nameof(AuthService.CompleteExternalLoginAsync))!
            .GetParameters().Single(p => p.Name == "idTenant");

        Assert.Equal(typeof(int), idTenant.ParameterType);
        Assert.False(idTenant.IsOptional);
    }

    [Fact]
    public async Task Restaurar_desde_sessionStorage_y_renovar_sigue_en_sessionStorage()
    {
        _js.Almacenes["sessionStorage"]["access_token"] = "token-guardado";
        _js.Almacenes["sessionStorage"]["refresh_token"] = "refresh-guardado";
        _js.Almacenes["sessionStorage"]["id_usuario"] = "1";
        _js.Almacenes["sessionStorage"]["id_tenant"] = "1";
        _js.Almacenes["sessionStorage"]["nom_usuario"] = "sistema";

        var proveedor = new CustomAuthenticationStateProvider(Auth, _js);
        await proveedor.InitializeFromStorageAsync();

        Assert.Equal(ModoPersistenciaSesion.Pestana, Auth.ModoPersistencia);

        var renovada = await Auth.TryRefreshAsync();

        Assert.True(renovada);
        Assert.Equal(0, _js.EscriturasEn("localStorage"));
        Assert.Contains("access_token", _js.ClavesDe("sessionStorage"));
    }

    [Fact]
    public async Task Al_cerrar_sesion_se_limpian_los_dos_almacenes()
    {
        await IniciarSesionAsync(rememberMe: false);

        await Auth.LogoutAsync();

        Assert.True(_js.BorradosEn("sessionStorage") >= 6);
        Assert.True(_js.BorradosEn("localStorage") >= 6);
    }
}
