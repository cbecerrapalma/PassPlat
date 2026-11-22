using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CBP.Acceso;
using CBP.Acceso.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PassPlat.Client.Services;

/// <summary>
/// S100 D6 — Implementación de <see cref="IProveedorMenuRemoto"/> contra <c>GET api/modulos/menu</c> de PassPlat: el menú de la app del token,
/// filtrado por los permisos del usuario.
/// <list type="bullet">
/// <item>El token del usuario va en cada petición (nunca en los encabezados por defecto del HttpClient compartido); no se registra en logs.</item>
/// <item>Sin token → <see cref="ErrorProveedorAccesoException"/> 401. Estado no 2xx → excepción con ese estado. Sin respuesta o fuera de tiempo → 503.</item>
/// <item>Sin reintentos.</item>
/// </list>
/// </summary>
public sealed class PassPlatMenuCliente : IProveedorMenuRemoto
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly OpcionesPassPlatAcceso _opciones;
    private readonly IServiceProvider _servicios;
    private readonly ILogger<PassPlatMenuCliente> _log;

    public PassPlatMenuCliente(HttpClient http, OpcionesPassPlatAcceso opciones, IServiceProvider servicios, ILogger<PassPlatMenuCliente>? log = null)
    {
        _http = http;
        _opciones = opciones;
        _servicios = servicios;
        _log = log ?? NullLogger<PassPlatMenuCliente>.Instance;
    }

    public async Task<IReadOnlyList<ItemMenuDto>> ObtenerMenuAsync(CancellationToken ct = default)
    {
        var token = _opciones.ObtenerToken?.Invoke(_servicios);
        if (string.IsNullOrWhiteSpace(token))
            throw new ErrorProveedorAccesoException("No hay sesión: PassPlat necesita el token del usuario para entregar el menú.", 401);

        using var msg = new HttpRequestMessage(HttpMethod.Get, "api/modulos/menu");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(msg, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ErrorProveedorAccesoException($"PassPlat no respondió al pedir el menú: {ex.Message}", 503, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ErrorProveedorAccesoException("PassPlat no respondió a tiempo al pedir el menú.", 503, ex);
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("PassPlat: menú → {Estado}.", (int)resp.StatusCode);
                throw new ErrorProveedorAccesoException($"PassPlat rechazó el menú: {(int)resp.StatusCode} {resp.ReasonPhrase}", (int)resp.StatusCode);
            }
            try
            {
                return await resp.Content.ReadFromJsonAsync<List<ItemMenuDto>>(Json, ct)
                    ?? throw new ErrorProveedorAccesoException("PassPlat respondió vacío al pedir el menú.", (int)resp.StatusCode);
            }
            catch (JsonException ex)
            {
                throw new ErrorProveedorAccesoException("PassPlat devolvió un menú ilegible.", (int)resp.StatusCode, ex);
            }
        }
    }
}
