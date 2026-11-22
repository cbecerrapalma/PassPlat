using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CBP.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PassPlat.Client.Models;

namespace PassPlat.Client.Services;

/// <summary>
/// S127 D1 — Implementación de <see cref="IPassPlatLoginExterno"/> contra <c>api/auth/externo/*</c> de PassPlat.
/// <list type="bullet">
/// <item>400 con <c>{codigo, mensaje}</c> → ese error; otro no-2xx → <c>PASSPLAT_HTTP_&lt;estado&gt;</c>; sin respuesta o fuera de tiempo → <c>PASSPLAT_NO_DISPONIBLE</c>.</item>
/// <item>Sin reintentos. El log nunca incluye el código de canje ni tokens.</item>
/// </list>
/// </summary>
public sealed class PassPlatLoginExternoCliente : IPassPlatLoginExterno
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<PassPlatLoginExternoCliente> _log;

    public PassPlatLoginExternoCliente(HttpClient http, ILogger<PassPlatLoginExternoCliente>? log = null)
    {
        _http = http;
        _log = log ?? NullLogger<PassPlatLoginExternoCliente>.Instance;
    }

    public async Task<Result<IReadOnlyList<ProveedorLoginExternoDto>>> ObtenerProveedoresLoginAsync(int idTenant, int idApp, CancellationToken ct = default)
    {
        if (idTenant <= 0 || idApp <= 0)
            return Result<IReadOnlyList<ProveedorLoginExternoDto>>.Failure(CodigosLoginExterno.ArgumentoInvalido, "idTenant e idApp deben ser mayores que cero.");

        using var msg = new HttpRequestMessage(HttpMethod.Get, $"api/auth/externo/proveedores-login?idTenant={idTenant}&idApp={idApp}");
        var r = await EnviarAsync<List<ProveedorLoginExternoDto>>(msg, "proveedores", ct);
        return r.IsSuccess
            ? Result<IReadOnlyList<ProveedorLoginExternoDto>>.Success(r.Value)
            : Result<IReadOnlyList<ProveedorLoginExternoDto>>.Failure(r.Error!);
    }

    public async Task<Result<string>> IniciarLoginExternoAsync(string proveedor, int idTenant, int idApp, string? urlRetorno = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(proveedor) || idTenant <= 0 || idApp <= 0)
            return Result<string>.Failure(CodigosLoginExterno.ArgumentoInvalido, "Proveedor, idTenant e idApp son obligatorios.");

        var ruta = $"api/auth/externo/{Uri.EscapeDataString(proveedor)}/authorize?idTenant={idTenant}&idApp={idApp}";
        if (!string.IsNullOrWhiteSpace(urlRetorno))
            ruta += $"&urlRetorno={Uri.EscapeDataString(urlRetorno)}";

        using var msg = new HttpRequestMessage(HttpMethod.Get, ruta);
        var r = await EnviarAsync<RespuestaAutorizar>(msg, "authorize", ct);
        if (r.IsFailure)
            return Result<string>.Failure(r.Error!);

        var url = r.Value.AuthorizationUrl;
        return string.IsNullOrWhiteSpace(url)
            ? Result<string>.Failure(CodigosLoginExterno.RespuestaInvalida, "PassPlat no devolvió la URL del proveedor.")
            : Result<string>.Success(url);
    }

    public async Task<Result<SesionLoginExternoDto>> CanjearCodigoAsync(string codigo, int idApp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo) || idApp <= 0)
            return Result<SesionLoginExternoDto>.Failure(CodigosLoginExterno.ArgumentoInvalido, "Código e idApp son obligatorios.");

        using var msg = new HttpRequestMessage(HttpMethod.Post, "api/auth/externo/canje")
        {
            Content = JsonContent.Create(new { codigo, idApp }, options: Json)
        };
        return await EnviarAsync<SesionLoginExternoDto>(msg, "canje", ct);
    }

    private async Task<Result<T>> EnviarAsync<T>(HttpRequestMessage msg, string operacion, CancellationToken ct) where T : class
    {
        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(msg, ct);
        }
        catch (HttpRequestException)
        {
            _log.LogWarning("PassPlat: login externo ({Operacion}) sin respuesta.", operacion);
            return Result<T>.Failure(CodigosLoginExterno.NoDisponible, "PassPlat no respondió.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning("PassPlat: login externo ({Operacion}) fuera de tiempo.", operacion);
            return Result<T>.Failure(CodigosLoginExterno.NoDisponible, "PassPlat no respondió a tiempo.");
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("PassPlat: login externo ({Operacion}) → {Estado}.", operacion, (int)resp.StatusCode);
                if (resp.StatusCode == HttpStatusCode.BadRequest)
                {
                    var error = await LeerErrorAsync(resp, ct);
                    if (error is not null)
                        return Result<T>.Failure(error.Codigo!, string.IsNullOrWhiteSpace(error.Mensaje) ? error.Codigo! : error.Mensaje);
                }
                return Result<T>.Failure($"PASSPLAT_HTTP_{(int)resp.StatusCode}", $"PassPlat respondió {(int)resp.StatusCode}.");
            }

            try
            {
                var valor = await resp.Content.ReadFromJsonAsync<T>(Json, ct);
                return valor is null
                    ? Result<T>.Failure(CodigosLoginExterno.RespuestaInvalida, "PassPlat respondió vacío.")
                    : Result<T>.Success(valor);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                return Result<T>.Failure(CodigosLoginExterno.RespuestaInvalida, "PassPlat devolvió una respuesta ilegible.");
            }
        }
    }

    private static async Task<ErrorPassPlat?> LeerErrorAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var error = await resp.Content.ReadFromJsonAsync<ErrorPassPlat>(Json, ct);
            return string.IsNullOrWhiteSpace(error?.Codigo) ? null : error;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private sealed class ErrorPassPlat
    {
        public string? Codigo { get; set; }
        public string? Mensaje { get; set; }
    }

    private sealed class RespuestaAutorizar
    {
        public string? AuthorizationUrl { get; set; }
    }
}
