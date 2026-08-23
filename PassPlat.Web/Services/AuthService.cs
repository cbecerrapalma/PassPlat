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

    public AuthService(HttpClient http, NavigationManager nav, IJSRuntime js)
    {
        _http = http;
        _nav = nav;
        _js = js;
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
    public int AppId { get; set; } = 1;
    public HashSet<string> Permisos { get; private set; } = [];
    public bool TienePermiso(string codigo) => Permisos.Contains(codigo);
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

    public async Task RestoreSessionAsync(string? accessToken, string? refreshToken, int? idUsuario, int? idTenant, int? idUsuarioTenant, string? nomUsuario)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        IdUsuario = idUsuario;
        IdTenant = idTenant;
        IdUsuarioTenant = idUsuarioTenant;
        NomUsuario = nomUsuario;
        ParseRoleFromToken();
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

    public async Task<List<AppItem>> GetAppsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<AppItem>>("api/apps/activas") ?? [];
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

            await SetSessionAsync(body, rememberMe: true);
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

    public async Task<LoginResult> CompleteExternalLoginAsync(string providerCode, string authorizationCode, string redirectUri, int idApp, int? idTenant = null)
    {
        var response = await _http.PostAsJsonAsync("api/auth/externo/login", new
        {
            idTenant = idTenant ?? 1,
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

        await SetSessionAsync(body, rememberMe: true);
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

    public async Task<string> CalcularHashAsync(string password)
    {
        var response = await _http.PostAsJsonAsync("api/password/calcular-hash", new { password });
        if (!response.IsSuccessStatusCode) return "";
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<LoginResult> CambiarPasswordAsync(string currentPassword, string newPasswordHash)
    {
        var response = await _http.PostAsJsonAsync("api/password/cambiar", new
        {
            currentPassword,
            newPasswordHash
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

        await SetSessionAsync(result, rememberMe: true);
        return true;
    }

    private void ParseRoleFromToken()
    {
        EsSistema = false;
        Permisos = [];
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
        if (rememberMe)
            await PersistSessionAsync();
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
        await PersistSessionAsync();
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
        }
        catch
        {
        }
    }

    private async Task ClearPersistedSessionAsync()
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
}
