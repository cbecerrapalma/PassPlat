using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CBP.Acceso;
using CBP.Acceso.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PassPlat.Client.Models;

namespace PassPlat.Client.Services;

/// <summary>
/// S99 D6 — Implementación de <see cref="IInyectorCatalogoRemoto"/> contra el contrato de PassPlat
/// <c>POST api/apps/{codigoApp}/catalogo</c>: una sola llamada por operación, transaccional del lado de PassPlat.
/// <list type="bullet">
/// <item>El catálogo sale del manifiesto con <see cref="CatalogoAcceso.Desde"/>; PassPlat crea, por ventana, VER y las acciones que el catálogo declara (S103).</item>
/// <item>El token del usuario va en cada petición (nunca en los encabezados por defecto del HttpClient compartido); no se registra en logs.</item>
/// <item>Sin reintentos: es un POST. Un estado distinto de 2xx sale como <see cref="ErrorProveedorAccesoException"/> con su estado HTTP.</item>
/// </list>
/// </summary>
public sealed class PassPlatCatalogoCliente : IInyectorCatalogoRemoto
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly OpcionesPassPlatAcceso _opciones;
    private readonly IServiceProvider _servicios;
    private readonly ILogger<PassPlatCatalogoCliente> _log;

    public PassPlatCatalogoCliente(HttpClient http, OpcionesPassPlatAcceso opciones, IServiceProvider servicios, ILogger<PassPlatCatalogoCliente>? log = null)
    {
        _http = http;
        _opciones = opciones;
        _servicios = servicios;
        _log = log ?? NullLogger<PassPlatCatalogoCliente>.Instance;
    }

    public async Task<PlanPublicacionDto> SimularAsync(ManifiestoAcceso manifiesto, CancellationToken ct = default)
    {
        var respuesta = await EnviarAsync(manifiesto, simular: true, huellaEsperada: null, [], ct);
        return new PlanPublicacionDto
        {
            CodigoApp = respuesta.CodigoApp,
            HuellaRemota = respuesta.HuellaRemota,
            HuellaManifiesto = manifiesto.Huella,
            Sobrantes = respuesta.Sobrantes,
            Operaciones = respuesta.Operaciones.Select(ADto).ToList()
        };
    }

    public async Task<ResultadoPublicacionDto> InyectarAsync(
        ManifiestoAcceso manifiesto, string huellaRemota, IReadOnlyCollection<string> desactivarSobrantes, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(huellaRemota);
        var respuesta = await EnviarAsync(manifiesto, simular: false, huellaRemota, desactivarSobrantes ?? [], ct);
        return new ResultadoPublicacionDto { Aplicadas = respuesta.Operaciones.Select(ADto).ToList() };
    }

    // ---------------- internos ----------------

    private async Task<ResultadoCatalogoRespuesta> EnviarAsync(
        ManifiestoAcceso manifiesto, bool simular, string? huellaEsperada, IReadOnlyCollection<string> desactivarSobrantes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(manifiesto);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifiesto.AppCodigo);
        var catalogo = CatalogoAcceso.Desde(manifiesto);
        var cuerpo = new CatalogoSolicitud
        {
            Simular = simular,
            HuellaEsperada = huellaEsperada,
            DesactivarSobrantes = [.. desactivarSobrantes],
            CodigoTipoModulo = _opciones.CodigoTipoModulo,
            Modulos = catalogo.Modulos.Select(m => new ModuloCatalogoSolicitud
            {
                Codigo = m.Codigo, Nombre = m.Nombre, Ruta = m.Ruta, Icono = m.Icono, Orden = m.Orden,
                CodigoPadre = m.CodigoPadre, EsVisibleMenu = m.EsVisibleMenu, Acciones = m.Acciones
            }).ToList(),
            Permisos = catalogo.Permisos.Select(p => new PermisoCatalogoSolicitud { Codigo = p.Codigo, Nombre = p.Nombre, CodigoModulo = p.CodigoModulo }).ToList()
        };

        var operacion = $"{(simular ? "simular" : "inyectar")} el catálogo de {manifiesto.AppCodigo}";
        using var msg = new HttpRequestMessage(HttpMethod.Post, $"api/apps/{Uri.EscapeDataString(manifiesto.AppCodigo)}/catalogo")
        {
            Content = JsonContent.Create(cuerpo, options: Json)
        };
        var token = _opciones.ObtenerToken?.Invoke(_servicios);
        if (!string.IsNullOrWhiteSpace(token)) msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(msg, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ErrorProveedorAccesoException($"PassPlat no respondió al {operacion}: {ex.Message}", null, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ErrorProveedorAccesoException($"PassPlat no respondió a tiempo al {operacion}.", null, ex);
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode) throw await ErrorAsync(resp, operacion, ct);
            _log.LogInformation("PassPlat: {Operacion} → {Estado}.", operacion, (int)resp.StatusCode);
            return await resp.Content.ReadFromJsonAsync<ResultadoCatalogoRespuesta>(Json, ct)
                ?? throw new ErrorProveedorAccesoException($"PassPlat respondió vacío al {operacion}.", (int)resp.StatusCode);
        }
    }

    private static OperacionAcceso ADto(OperacionCatalogoRespuesta o)
    {
        if (!Enum.TryParse<TipoOperacionAcceso>(o.Tipo, ignoreCase: false, out var tipo))
            throw new ErrorProveedorAccesoException($"PassPlat devolvió una operación desconocida: '{o.Tipo}' ({o.Codigo}).");
        var operacion = new OperacionAcceso { Tipo = tipo, Codigo = o.Codigo };
        if (tipo is TipoOperacionAcceso.CrearPermiso or TipoOperacionAcceso.MoverPermiso or TipoOperacionAcceso.DesactivarPermiso)
            operacion.Permiso = new PermisoDto { Codigo = o.Codigo, Nombre = o.Nombre ?? string.Empty, CodigoModulo = o.CodigoModulo ?? string.Empty };
        else
            operacion.Modulo = new ModuloDto { Codigo = o.Codigo, Nombre = o.Nombre ?? string.Empty, CodigoPadre = o.CodigoModulo };
        return operacion;
    }

    private async Task<ErrorProveedorAccesoException> ErrorAsync(HttpResponseMessage resp, string operacion, CancellationToken ct)
    {
        ProblemaRespuesta? problema = null;
        try
        {
            problema = await resp.Content.ReadFromJsonAsync<ProblemaRespuesta>(Json, ct);
        }
        catch (JsonException) { }
        catch (NotSupportedException) { }
        _log.LogWarning("PassPlat: {Operacion} → {Estado}.", operacion, (int)resp.StatusCode);

        var detalle = problema?.Detail ?? problema?.Title;
        var codigo = string.IsNullOrWhiteSpace(problema?.ErrorCode) ? null : $" [{problema!.ErrorCode}]";
        var lista = problema?.Errors is { Count: > 0 } errores ? " " + string.Join(" ", errores.OrderBy(e => e.Key, StringComparer.Ordinal).Take(5).Select(e => e.Value)) : string.Empty;
        return new ErrorProveedorAccesoException(
            $"PassPlat rechazó {operacion}: {(int)resp.StatusCode} {resp.ReasonPhrase}{(detalle is null ? "" : " — " + detalle)}{codigo}{lista}",
            (int)resp.StatusCode);
    }
}
