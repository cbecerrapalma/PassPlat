using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PassPlat.Web.Helpers;

namespace PassPlat.Web.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new LocalDateTimeConverter(), new LocalDateTimeNullableConverter() }
    };
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public string? LastError { get; private set; }

    public ApiClient(HttpClient http, AuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    private void SetAuthHeader()
    {
        var token = _auth.AccessToken;
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        var response = await SendAsync(() => _http.GetAsync(endpoint));
        if (response == null || !response.IsSuccessStatusCode) return default;
        return await response.Content.ReadFromJsonAsync<T>(JsonOpts);
    }

    public async Task<T?> PostAsync<T>(string endpoint, object? body = null)
    {
        var response = await SendAsync(() => body is not null
            ? _http.PostAsJsonAsync(endpoint, body, JsonOpts)
            : _http.PostAsync(endpoint, null));
        if (response == null || !response.IsSuccessStatusCode) return default;
        return await response.Content.ReadFromJsonAsync<T>(JsonOpts);
    }

    public async Task<(T? Result, string? Error)> PostWithErrorAsync<T>(string endpoint, object? body = null)
    {
        var response = await SendAsync(() => body is not null
            ? _http.PostAsJsonAsync(endpoint, body, JsonOpts)
            : _http.PostAsync(endpoint, null));
        if (response == null) return (default, "No se pudo conectar con el servidor");
        if (!response.IsSuccessStatusCode)
        {
            var bodyText = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(bodyText);
                if (doc.RootElement.TryGetProperty("detail", out var detail))
                    return (default, detail.GetString() ?? bodyText);
            }
            catch { }
            return (default, bodyText);
        }
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOpts);
        return (result, null);
    }

    public async Task<bool> PostVoidAsync(string endpoint, object? body = null)
    {
        var response = await SendAsync(() => body is not null
            ? _http.PostAsJsonAsync(endpoint, body, JsonOpts)
            : _http.PostAsync(endpoint, null));
        return response?.IsSuccessStatusCode ?? false;
    }

    public async Task<T?> PutAsync<T>(string endpoint, object? body = null)
    {
        var response = await SendAsync(() => body is not null
            ? _http.PutAsJsonAsync(endpoint, body, JsonOpts)
            : _http.PutAsync(endpoint, null));
        if (response == null || !response.IsSuccessStatusCode) return default;
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return default;
        return await response.Content.ReadFromJsonAsync<T>(JsonOpts);
    }

    public async Task<bool> PutVoidAsync(string endpoint, object? body = null)
    {
        var response = await SendAsync(() => body is not null
            ? _http.PutAsJsonAsync(endpoint, body, JsonOpts)
            : _http.PutAsync(endpoint, null));
        return response?.IsSuccessStatusCode ?? false;
    }

    public async Task<bool> DeleteAsync(string endpoint)
    {
        var response = await SendAsync(() => _http.DeleteAsync(endpoint));
        return response?.IsSuccessStatusCode ?? false;
    }

    private async Task<string?> TryExtractErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString();
            if (doc.RootElement.TryGetProperty("mensaje", out var mensaje) && mensaje.ValueKind == JsonValueKind.String)
                return mensaje.GetString();
            if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString();
        }
        catch { }
        return $"HTTP {(int)response.StatusCode}";
    }

    private async Task<HttpResponseMessage?> SendAsync(Func<Task<HttpResponseMessage>> request)
    {
        SetAuthHeader();
        LastError = null;
        var response = await request();

        if (response.IsSuccessStatusCode)
            return response;

        if ((int)response.StatusCode == 401 && _auth.IsAuthenticated)
        {
            response.Dispose();
            await RefreshLock.WaitAsync();
            try
            {
                var refreshed = await _auth.TryRefreshAsync();
                if (!refreshed)
                {
                    LastError = "Sesión expirada";
                    return null;
                }
            }
            finally
            {
                RefreshLock.Release();
            }
            SetAuthHeader();
            var retry = await request();
            if (retry.IsSuccessStatusCode)
                return retry;
            LastError = await TryExtractErrorAsync(retry);
            return retry;
        }

        LastError = await TryExtractErrorAsync(response);
        return response;
    }
}
