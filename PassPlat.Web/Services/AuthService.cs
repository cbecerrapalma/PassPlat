using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Web.Models;

namespace PassPlat.Web.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly NavigationManager _nav;
    private readonly IJSRuntime _js;

    public AuthService(HttpClient http, NavigationManager nav, IJSRuntime js, AppSettings? appSettings = null)
    {
        _http = http;
        _nav = nav;
        _js = js;
        // S76 · D1 — la Web es el portal de una sola app (AppSettings.AppId, 1 = PASSPLAT).
        AppId = appSettings?.AppId ?? 1;
    }

    public event Action? OnAuthStateChanged;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public int? IdUsuario { get; private set; }
    public int? IdTenant { get; private set; }
    public int? IdUsuarioTenant { get; private set; }
    public string? NombreTenant { get; private set; }
    public string? NomUsuario { get; private set; }
    public bool ReqCambioPwd { get; private set; }
    public bool EsSistema { get; private set; }
    public bool EsPlatform => !IdTenant.HasValue || IdTenant.Value == 0;
    public bool EsTenant => IdTenant.HasValue && IdTenant.Value > 0;
    public int AppId { get; }
    public HashSet<string> Permisos { get; private set; } = [];
    public bool TienePermiso(string codigo) => Permisos.Contains(codigo);
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

    /// <summary>
    /// S55.1 · D1 — donde vive la sesion. «Recordarme» la deja en <c>localStorage</c>
    /// (sobrevive al cierre del navegador); sin el, queda en <c>sessionStorage</c>
    /// (sobrevive a F5 en la misma pestana). El modo no cambia al renovar token,
    /// cambiar de tenant ni entrar con un proveedor externo.
    /// </summary>
    public ModoPersistenciaSesion ModoPersistencia => _modoPersistencia;

    private ModoPersistenciaSesion _modoPersistencia = ModoPersistenciaSesion.Pestana;

    /// <summary>S76 · D5 — claim IdApp del token; null si falta o no es numero.</summary>
    private int? _idAppToken;

    public async Task RestoreSessionAsync(string? accessToken, string? refreshToken, int? idUsuario, int? idTenant, int? idUsuarioTenant, string? nomUsuario, ModoPersistenciaSesion? modo = null)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        IdUsuario = idUsuario;
        IdTenant = idTenant;
        IdUsuarioTenant = idUsuarioTenant;
        NomUsuario = nomUsuario;
        _modoPersistencia = modo ?? ModoPersistenciaSesion.Pestana;
        ParseRoleFromToken();
        // S76 · D5 — una sesion guardada de otra app no abre el portal.
        if (_idAppToken.HasValue && _idAppToken.Value != AppId)
        {
            await ClearSessionAsync();
            return;
        }
        if (!string.IsNullOrEmpty(accessToken))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        await LoadCurrentTenantAsync();
    }

    public async Task<LoginResult> CompletarLoginMFAAsync(int idUsuario, int idTenant, int idApp, int idMFAPrincipal, string codigoMFA, bool rememberMe = true)
    {
        var response = await _http.PostAsJsonAsync("api/auth/validar-mfa", new
        {
            idUsuario,
            idTenant,
            idApp,
            idMFAPrincipal,
            codigoMFA,
            idDisp = (int?)null,
            idIP = (int?)null,
            idAgente = (int?)null
        });

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var doc = JsonDocument.Parse(json);
            return new LoginResult
            {
                Success = false,
                ErrorCode = TryGetString(doc.RootElement, "codigo"),
                ErrorMessage = TryGetString(doc.RootElement, "mensaje")
            };
        }

        var body = JsonSerializer.Deserialize<AuthResponseDto>(json, _jsonOptions);
        if (body is null || string.IsNullOrEmpty(body.AccessToken))
            return new LoginResult { Success = false, ErrorMessage = "Respuesta inválida del servidor" };

        await SetSessionAsync(body, rememberMe);
        return new LoginResult { Success = true };
    }

    /// <summary>S125 · D5 — canjea el código de un solo uso del login externo por la sesión.</summary>
    public async Task<LoginResult> CanjearCodigoExternoAsync(string codigo)
    {
        var response = await _http.PostAsJsonAsync("api/auth/externo/canje", new { codigo, idApp = AppId });
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new LoginResult { Success = false, ErrorMessage = LeerMensaje(json) ?? "No se pudo completar el inicio de sesión." };

        var body = JsonSerializer.Deserialize<AuthResponseDto>(json, _jsonOptions);
        if (body is null || string.IsNullOrEmpty(body.AccessToken))
            return new LoginResult { Success = false, ErrorMessage = "Respuesta inválida del servidor" };

        await SetSessionFromFragmentAsync(body.AccessToken, body.RefreshToken, body.IdUsuario, body.IdTenant, body.NomUsuario, body.ReqCambioPwd);
        return new LoginResult { Success = true };
    }

    /// <summary>S125 · D5 — tipo de MFA de un login externo pendiente; null si la referencia venció.</summary>
    public async Task<int?> ObtenerMfaExternoAsync(string mfaRef)
    {
        var response = await _http.GetAsync($"api/auth/externo/mfa/{Uri.EscapeDataString(mfaRef)}");
        if (!response.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return TryGetInt32(doc.RootElement, "idTipoMFA");
    }

    /// <summary>S125 · D5 — valida el MFA del login externo; devuelve la URL de la app con el código de canje.</summary>
    public async Task<(string? UrlRetorno, string? Error)> ValidarMfaExternoAsync(string mfaRef, string codigoMFA)
    {
        var response = await _http.PostAsJsonAsync("api/auth/externo/mfa/validar", new { mfaRef, codigoMFA });
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return (null, LeerMensaje(json) ?? "Código inválido");

        using var doc = JsonDocument.Parse(json);
        var url = TryGetString(doc.RootElement, "urlRetorno");
        return string.IsNullOrEmpty(url) ? (null, "Respuesta inválida del servidor") : (url, null);
    }

    private static string? LeerMensaje(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return TryGetString(doc.RootElement, "mensaje");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<TenantInfoResult?> GetTenantInfoAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<TenantInfoResult>("api/auth/tenant-info");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<TenantItem>> GetTenantsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<TenantItem>>("api/auth/tenants") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<List<TenantItem>> GetMisTenantsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<TenantItem>>("api/auth/mis-tenants") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<LoginResult> SwitchTenantAsync(int idTenant)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/switch-tenant/" + idTenant, new
            {
                idApp = AppId,
                idDisp = (int?)null,
                idIP = (int?)null,
                idAgente = (int?)null
            });

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var doc = JsonDocument.Parse(json);
                return new LoginResult
                {
                    Success = false,
                    ErrorCode = TryGetString(doc.RootElement, "codigo"),
                    ErrorMessage = TryGetString(doc.RootElement, "mensaje")
                };
            }

            var body = JsonSerializer.Deserialize<AuthResponseDto>(json, _jsonOptions);
            if (body is null || string.IsNullOrEmpty(body.AccessToken))
                return new LoginResult { Success = false, ErrorMessage = "Respuesta inválida del servidor" };

            await SetSessionAsync(body, rememberMe: _modoPersistencia == ModoPersistenciaSesion.Local);
            return new LoginResult { Success = true };
        }
        catch (Exception ex)
        {
            return new LoginResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<LoginResult> PlatformLoginAsync(string nomUsuario, string password, int idApp, bool rememberMe = true)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login/platform", new
            {
                nomUsuario,
                idApp,
                password,
                idDisp = (int?)null,
                idIP = (int?)null,
                idAgente = (int?)null
            });

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorCode = TryGetString(doc.RootElement, "codigo"),
                    ErrorMessage = TryGetString(doc.RootElement, "mensaje")
                };
            }

            var body = JsonSerializer.Deserialize<AuthResponseDto>(json, _jsonOptions);
            if (body is null || string.IsNullOrEmpty(body.AccessToken))
                return new LoginResult { Success = false, ErrorMessage = "Respuesta inválida del servidor" };

            await SetSessionAsync(body, rememberMe);
            return new LoginResult { Success = true };
        }
        catch (Exception ex)
        {
            return new LoginResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task LoadCurrentTenantAsync()
    {
        if (!IsAuthenticated || !IdTenant.HasValue)
            return;
        try
        {
            var response = await _http.GetFromJsonAsync<JsonElement>("api/auth/current-tenant");
            if (response.TryGetProperty("nombre", out var nombre))
                NombreTenant = nombre.GetString();
        }
        catch
        {
            // Ignore — tenant name is optional display info
        }
    }

    // S100 · D3 — el tenant es obligatorio: sin reserva al tenant 1 (P58)
    public async Task<LoginResult> CompleteExternalLoginAsync(string providerCode, string authorizationCode, string redirectUri, int idApp, int idTenant, bool rememberMe = false)
    {
        var response = await _http.PostAsJsonAsync("api/auth/externo/login", new
        {
            idTenant,
            idApp,
            providerCode,
            authorizationCode,
            redirectUri,
            idDisp = (int?)null,
            idIP = (int?)null,
            idAgente = (int?)null
        });

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        if (!response.IsSuccessStatusCode)
        {
            return new LoginResult
            {
                Success = false,
                ErrorCode = TryGetString(doc.RootElement, "codigo"),
                ErrorMessage = TryGetString(doc.RootElement, "mensaje")
            };
        }

        var requiereMFA = doc.RootElement.TryGetProperty("requiereMFA", out var mfa) && mfa.GetBoolean();
        if (requiereMFA)
        {
            return new LoginResult
            {
                Success = false,
                RequiereMFA = true,
                IdMFAPrincipal = TryGetInt32(doc.RootElement, "idMFAPrincipal"),
                IdTipoMFA = TryGetInt32(doc.RootElement, "idTipoMFA"),
                IdUsuario = TryGetInt32(doc.RootElement, "idUsuario"),
                IdTenant = TryGetInt32(doc.RootElement, "idTenant"),
                NomUsuario = TryGetString(doc.RootElement, "nomUsuario")
            };
        }

        var body = JsonSerializer.Deserialize<AuthResponseDto>(json, _jsonOptions);
        if (body is null || string.IsNullOrEmpty(body.AccessToken))
            return new LoginResult { Success = false, ErrorMessage = "Respuesta inválida del servidor" };

        await SetSessionAsync(body, rememberMe: rememberMe);
        return new LoginResult { Success = true };
    }

    public async Task<LoginResult> LoginAsync(string nomUsuario, string password, int idApp, bool rememberMe = true, int? idTenant = null)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new
        {
            nomUsuario,
            email = (string?)null,
            idApp,
            password,
            idDisp = (int?)null,
            idIP = (int?)null,
            idAgente = (int?)null,
            idTenant
        });

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        if (!response.IsSuccessStatusCode)
        {
            return new LoginResult
            {
                Success = false,
                ErrorCode = TryGetString(doc.RootElement, "codigo"),
                ErrorMessage = TryGetString(doc.RootElement, "mensaje")
            };
        }

        var requiereMFA = doc.RootElement.TryGetProperty("requiereMFA", out var mfa) && mfa.GetBoolean();
        if (requiereMFA)
        {
            return new LoginResult
            {
                Success = false,
                RequiereMFA = true,
                IdMFAPrincipal = TryGetInt32(doc.RootElement, "idMFAPrincipal"),
                IdTipoMFA = TryGetInt32(doc.RootElement, "idTipoMFA"),
                IdUsuario = TryGetInt32(doc.RootElement, "idUsuario"),
                IdTenant = TryGetInt32(doc.RootElement, "idTenant"),
                NomUsuario = TryGetString(doc.RootElement, "nomUsuario")
            };
        }

        var body = JsonSerializer.Deserialize<AuthResponseDto>(json, _jsonOptions);
        if (body is null || string.IsNullOrEmpty(body.AccessToken))
            return new LoginResult { Success = false, ErrorMessage = "Respuesta inválida del servidor" };

        await SetSessionAsync(body, rememberMe);
        return new LoginResult { Success = true };
    }

    public async Task<LoginResult> RestablecerPasswordAsync(string token, string nuevaPassword)
    {
        var response = await _http.PostAsJsonAsync("api/auth/restablecer-password", new
        {
            token,
            nuevaPassword
        });

        if (!response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            return new LoginResult
            {
                Success = false,
                ErrorCode = TryGetString(doc.RootElement, "codigo"),
                ErrorMessage = TryGetString(doc.RootElement, "mensaje")
            };
        }

        return new LoginResult { Success = true };
    }

    public async Task<LoginResult> SolicitarResetPasswordAsync(string email)
    {
        var response = await _http.PostAsJsonAsync("api/auth/olvido-password", new { email });
        if (!response.IsSuccessStatusCode)
            return new LoginResult { Success = false, ErrorMessage = "Error al solicitar restablecimiento" };

        return new LoginResult { Success = true };
    }

    public async Task<LoginResult> CambiarPasswordAsync(string passwordActual, string passwordNueva)
    {
        var response = await _http.PostAsJsonAsync("api/password/cambiar", new
        {
            passwordActual,
            passwordNueva
        });

        if (!response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            return new LoginResult
            {
                Success = false,
                ErrorCode = TryGetString(doc.RootElement, "codigo"),
                ErrorMessage = TryGetString(doc.RootElement, "mensaje")
            };
        }

        ReqCambioPwd = false;
        return new LoginResult { Success = true };
    }

    public async Task<PoliticaPwdDto?> GetPoliticaPwdAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<PoliticaPwdDto>("api/usuarios/password-policy");
        }
        catch
        {
            return null;
        }
    }

    public static string GetFriendlyErrorMessage(string? errorCode, string? defaultMessage)
    {
        return errorCode switch
        {
            "PASSWORD_REUSE" => "No puedes reutilizar una contraseña anterior",
            "PASSWORD_TOO_SHORT" => "La contraseña es demasiado corta",
            "PASSWORD_TOO_LONG" => "La contraseña excede la longitud máxima",
            "PASSWORD_WEAK" => "La contraseña no cumple los requisitos de complejidad",
            "PASSWORD_COMMON" => "La contraseña es demasiado común",
            "PASSWORD_SEQUENTIAL" => "La contraseña contiene secuencias no permitidas",
            "PASSWORD_REPEATING" => "La contraseña contiene caracteres repetidos",
            "PASSWORD_USER_INFO" => "La contraseña contiene información personal",
            "CURRENT_PASSWORD_INVALID" => "La contraseña actual es incorrecta",
            "USER_NOT_FOUND" => "Usuario no encontrado",
            "USER_INACTIVE" => "La cuenta está inactiva",
            "USER_BLOCKED" => "La cuenta está bloqueada",
            _ => defaultMessage ?? "Error al cambiar la contraseña"
        };
    }

    private static string? TryGetString(JsonElement el, string key)
    {
        return el.TryGetProperty(key, out var p) ? p.GetString() : null;
    }

    private static int? TryGetInt32(JsonElement el, string key)
    {
        return el.TryGetProperty(key, out var p) ? p.GetInt32() : null;
    }

    public async Task<bool> TryRefreshAsync()
    {
        if (string.IsNullOrEmpty(RefreshToken))
            return false;

        var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh")
        {
            Content = JsonContent.Create(new { refreshToken = RefreshToken })
        };

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            await LogoutAsync();
            return false;
        }

        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        if (result?.AccessToken is null)
        {
            await LogoutAsync();
            return false;
        }

        await SetSessionAsync(result, rememberMe: _modoPersistencia == ModoPersistenciaSesion.Local);
        return true;
    }

    private void ParseRoleFromToken()
    {
        EsSistema = false;
        Permisos = [];
        _idAppToken = null;
        if (string.IsNullOrEmpty(AccessToken)) return;
        try
        {
            var parts = AccessToken.Split('.');
            if (parts.Length < 2) return;
            var payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            var bytes = Convert.FromBase64String(payload);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            using var doc = JsonDocument.Parse(json);

            // Override IdTenant from JWT claim (authoritative source)
            if (doc.RootElement.TryGetProperty("TenantId", out var tid))
                IdTenant = int.TryParse(tid.GetString(), out var t) ? t : null;
            else
                IdTenant = null; // platform scope

            if (doc.RootElement.TryGetProperty("UsuarioTenantId", out var utid))
                IdUsuarioTenant = int.TryParse(utid.GetString(), out var ut) ? ut : null;
            else
                IdUsuarioTenant = null;

            if (doc.RootElement.TryGetProperty("IdApp", out var iapp))
                _idAppToken = int.TryParse(iapp.GetString(), out var ia) ? ia : null;

            if (doc.RootElement.TryGetProperty("is_system", out var sys))
                EsSistema = sys.GetString() == "true";
            if (doc.RootElement.TryGetProperty("permiso", out var perms))
            {
                if (perms.ValueKind == JsonValueKind.Array)
                    Permisos = [.. perms.EnumerateArray().Select(p => p.GetString() ?? "")];
                else if (perms.ValueKind == JsonValueKind.String)
                    Permisos = [perms.GetString() ?? ""];
            }
        }
        catch
        {
        }
    }

    private async Task SetSessionAsync(AuthResponseDto response, bool rememberMe)
    {
        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken;
        IdUsuario = response.IdUsuario;
        IdTenant = response.IdTenant;
        NomUsuario = response.NomUsuario;
        ReqCambioPwd = response.ReqCambioPwd;
        ParseRoleFromToken();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        // S55.1 · D1 — el modo lo fija el login («Recordarme») y no cambia despues.
        _modoPersistencia = rememberMe ? ModoPersistenciaSesion.Local : ModoPersistenciaSesion.Pestana;
        if (rememberMe)
            await PersistSessionAsync();
        else
            await PersistSessionInTabAsync();
        OnAuthStateChanged?.Invoke();
        await LoadCurrentTenantAsync();
    }

    public async Task SetSessionFromFragmentAsync(
        string accessToken, string? refreshToken,
        int idUsuario, int idTenant, string? nomUsuario, bool reqCambioPwd)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        IdUsuario = idUsuario;
        IdTenant = idTenant;
        NomUsuario = nomUsuario ?? "";
        ReqCambioPwd = reqCambioPwd;
        ParseRoleFromToken();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        if (_modoPersistencia == ModoPersistenciaSesion.Local)
            await PersistSessionAsync();
        else
            await PersistSessionInTabAsync();
        OnAuthStateChanged?.Invoke();
        await LoadCurrentTenantAsync();
    }

    private async Task ClearSessionAsync()
    {
        AccessToken = null;
        RefreshToken = null;
        IdUsuario = null;
        IdTenant = null;
        IdUsuarioTenant = null;
        NombreTenant = null;
        NomUsuario = null;
        ReqCambioPwd = false;
        EsSistema = false;
        Permisos = [];
        _http.DefaultRequestHeaders.Authorization = null;
        await ClearPersistedSessionAsync();
        OnAuthStateChanged?.Invoke();
    }

    public async Task LogoutAsync()
    {
        _ = TryLogoutServerAsync();
        await ClearSessionAsync();
        _nav.NavigateTo("/login");
    }

    private async Task TryLogoutServerAsync()
    {
        if (string.IsNullOrEmpty(AccessToken)) return;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _http.SendAsync(request, cts.Token);
        }
        catch
        {
        }
    }

    private async Task PersistSessionAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", "access_token", AccessToken ?? "");
            await _js.InvokeVoidAsync("localStorage.setItem", "refresh_token", RefreshToken ?? "");
            await _js.InvokeVoidAsync("localStorage.setItem", "id_usuario", IdUsuario?.ToString() ?? "");
            await _js.InvokeVoidAsync("localStorage.setItem", "id_tenant", IdTenant?.ToString() ?? "");
            await _js.InvokeVoidAsync("localStorage.setItem", "id_usuario_tenant", IdUsuarioTenant?.ToString() ?? "");
            await _js.InvokeVoidAsync("localStorage.setItem", "nom_usuario", NomUsuario ?? "");
            // S55.1 · D1 — nunca hay dos copias de la sesion.
            await ClearSessionInTabAsync();
        }
        catch
        {
        }
    }

    /// <summary>
    /// S55 · D6 (U12) — sin "Recordarme" la sesion se guarda en sessionStorage:
    /// sobrevive a F5 en la misma pestana y desaparece al cerrarla.
    /// </summary>
    private async Task PersistSessionInTabAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("sessionStorage.setItem", "access_token", AccessToken ?? "");
            await _js.InvokeVoidAsync("sessionStorage.setItem", "refresh_token", RefreshToken ?? "");
            await _js.InvokeVoidAsync("sessionStorage.setItem", "id_usuario", IdUsuario?.ToString() ?? "");
            await _js.InvokeVoidAsync("sessionStorage.setItem", "id_tenant", IdTenant?.ToString() ?? "");
            await _js.InvokeVoidAsync("sessionStorage.setItem", "id_usuario_tenant", IdUsuarioTenant?.ToString() ?? "");
            await _js.InvokeVoidAsync("sessionStorage.setItem", "nom_usuario", NomUsuario ?? "");
            // S55.1 · D1 — nunca hay dos copias de la sesion.
            await ClearSessionInBrowserAsync();
        }
        catch
        {
        }
    }

    private async Task ClearSessionInTabAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("sessionStorage.removeItem", "access_token");
            await _js.InvokeVoidAsync("sessionStorage.removeItem", "refresh_token");
            await _js.InvokeVoidAsync("sessionStorage.removeItem", "id_usuario");
            await _js.InvokeVoidAsync("sessionStorage.removeItem", "id_tenant");
            await _js.InvokeVoidAsync("sessionStorage.removeItem", "id_usuario_tenant");
            await _js.InvokeVoidAsync("sessionStorage.removeItem", "nom_usuario");
        }
        catch
        {
        }
    }

    private async Task ClearSessionInBrowserAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", "access_token");
            await _js.InvokeVoidAsync("localStorage.removeItem", "refresh_token");
            await _js.InvokeVoidAsync("localStorage.removeItem", "id_usuario");
            await _js.InvokeVoidAsync("localStorage.removeItem", "id_tenant");
            await _js.InvokeVoidAsync("localStorage.removeItem", "id_usuario_tenant");
            await _js.InvokeVoidAsync("localStorage.removeItem", "nom_usuario");
        }
        catch
        {
        }
    }

    private async Task ClearPersistedSessionAsync()
    {
        await ClearSessionInBrowserAsync();
        await ClearSessionInTabAsync();
    }
}

/// <summary>S55.1 · D1 — almacen donde vive la sesion de la Web.</summary>
public enum ModoPersistenciaSesion
{
    /// <summary>localStorage: sobrevive al cierre del navegador («Recordarme»).</summary>
    Local,

    /// <summary>sessionStorage: sobrevive a F5 en la misma pestana.</summary>
    Pestana
}
