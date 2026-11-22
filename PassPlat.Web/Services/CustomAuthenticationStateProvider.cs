using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace PassPlat.Web.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly AuthService _auth;
    private readonly IJSRuntime _js;

    public CustomAuthenticationStateProvider(AuthService auth, IJSRuntime js)
    {
        _auth = auth;
        _js = js;
        _auth.OnAuthStateChanged += NotifyAuthStateChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_auth.IsAuthenticated)
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _auth.IdUsuario?.ToString() ?? ""),
            new(ClaimTypes.Name, _auth.NomUsuario ?? ""),
            new("TenantId", _auth.IdTenant?.ToString() ?? "")
        };
        if (_auth.IdUsuarioTenant.HasValue)
            claims.Add(new Claim("UsuarioTenantId", _auth.IdUsuarioTenant.Value.ToString()));
        if (_auth.EsSistema)
            claims.Add(new Claim("is_system", "true"));
        claims.AddRange(_auth.Permisos.Select(p => new Claim("permiso", p)));
        var identity = new ClaimsIdentity(claims, "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyAuthStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task InitializeFromStorageAsync()
    {
        try
        {
            // S55 · D6 (U12) — sin "Recordarme" la sesion vive en sessionStorage:
            // sobrevive a F5 en la misma pestana y se borra al cerrarla.
            // Con "Recordarme" sigue en localStorage (persiste entre pestanas).
            // S55.1 · D1 — el almacen del que viene la sesion fija su modo.
            var modo = ModoPersistenciaSesion.Local;
            var token = await _js.InvokeAsync<string>("localStorage.getItem", "access_token");
            var refresh = await _js.InvokeAsync<string>("localStorage.getItem", "refresh_token");
            var idUsr = await _js.InvokeAsync<string>("localStorage.getItem", "id_usuario");
            var idTen = await _js.InvokeAsync<string>("localStorage.getItem", "id_tenant");
            var idUten = await _js.InvokeAsync<string>("localStorage.getItem", "id_usuario_tenant");
            var nomUsr = await _js.InvokeAsync<string>("localStorage.getItem", "nom_usuario");

            if (string.IsNullOrEmpty(token))
            {
                modo = ModoPersistenciaSesion.Pestana;
                token = await _js.InvokeAsync<string>("sessionStorage.getItem", "access_token");
                refresh = await _js.InvokeAsync<string>("sessionStorage.getItem", "refresh_token");
                idUsr = await _js.InvokeAsync<string>("sessionStorage.getItem", "id_usuario");
                idTen = await _js.InvokeAsync<string>("sessionStorage.getItem", "id_tenant");
                idUten = await _js.InvokeAsync<string>("sessionStorage.getItem", "id_usuario_tenant");
                nomUsr = await _js.InvokeAsync<string>("sessionStorage.getItem", "nom_usuario");
            }

            if (!string.IsNullOrEmpty(token))
            {
                await _auth.RestoreSessionAsync(token, refresh,
                    int.TryParse(idUsr, out var uid) ? uid : null,
                    int.TryParse(idTen, out var tid) ? tid : null,
                    int.TryParse(idUten, out var ut) ? ut : null,
                    nomUsr, modo);
                NotifyAuthStateChanged();
            }
        }
        catch
        {
            // Ignoramos errores de inicialización desde almacenamiento local
        }
    }
}
