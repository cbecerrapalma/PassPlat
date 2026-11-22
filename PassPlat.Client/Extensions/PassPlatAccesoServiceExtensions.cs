using CBP.Acceso;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Client.Services;

namespace PassPlat.Client;

/// <summary>Opciones del SDK de acceso de PassPlat.</summary>
public sealed class OpcionesPassPlatAcceso
{
    /// <summary>URL base de la API oficial de PassPlat.</summary>
    public string BaseUrl { get; set; } = "";

    public int TimeoutMs { get; set; } = 15000;

    /// <summary>Tipo de módulo para crear la raíz cuando no existe (se busca por <c>TipoModuloCodigo</c>). Si la raíz existe, se usa su tipo.</summary>
    public string CodigoTipoModulo { get; set; } = "SHARED";

    /// <summary>
    /// Token Bearer del usuario actual (se reenvía en CADA petición; PassPlat autoriza con sus propios permisos).
    /// Recibe el proveedor de servicios del ámbito de la petición.
    /// </summary>
    public Func<IServiceProvider, string?>? ObtenerToken { get; set; }
}

public static class PassPlatAccesoServiceExtensions
{
    /// <summary>
    /// Registra <see cref="IInyectorCatalogoRemoto"/> implementado contra el contrato de catálogo de la API oficial de PassPlat
    /// (cliente tipado de <c>IHttpClientFactory</c>, con ámbito por petición).
    /// </summary>
    public static IServiceCollection AddPassPlatAcceso(this IServiceCollection services, Action<OpcionesPassPlatAcceso> configurar)
    {
        ArgumentNullException.ThrowIfNull(configurar);
        var opciones = new OpcionesPassPlatAcceso();
        configurar(opciones);
        if (!Uri.TryCreate(opciones.BaseUrl, UriKind.Absolute, out var baseUri))
            throw new ArgumentException("OpcionesPassPlatAcceso.BaseUrl debe ser una URL absoluta.", nameof(configurar));
        if (opciones.ObtenerToken is null)
            throw new ArgumentException("OpcionesPassPlatAcceso.ObtenerToken es obligatorio: PassPlat se llama con el token del usuario.", nameof(configurar));
        if (opciones.TimeoutMs <= 0)
            throw new ArgumentException("OpcionesPassPlatAcceso.TimeoutMs debe ser mayor que cero.", nameof(configurar));

        services.AddSingleton(opciones);
        services.AddHttpClient<IInyectorCatalogoRemoto, PassPlatCatalogoCliente>(c =>
        {
            c.BaseAddress = baseUri;
            c.Timeout = TimeSpan.FromMilliseconds(opciones.TimeoutMs);
        });
        // S100 D6: el menú de la app para el usuario actual (GET api/modulos/menu).
        services.AddHttpClient<IProveedorMenuRemoto, PassPlatMenuCliente>(c =>
        {
            c.BaseAddress = baseUri;
            c.Timeout = TimeSpan.FromMilliseconds(opciones.TimeoutMs);
        });
        // S127 D1: login externo de la app (api/auth/externo/*, anónimo: sin token).
        services.AddHttpClient<IPassPlatLoginExterno, PassPlatLoginExternoCliente>(c =>
        {
            c.BaseAddress = baseUri;
            c.Timeout = TimeSpan.FromMilliseconds(opciones.TimeoutMs);
        });
        return services;
    }
}
