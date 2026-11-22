using CBP.Results;
using PassPlat.Client.Models;

namespace PassPlat.Client.Services;

/// <summary>
/// S127 D1 — login externo (Google, Microsoft…) de una App consumidora a través de PassPlat: PassPlat hace el OAuth
/// y vuelve a <c>Apps.UrlBase</c> + <c>/signin-callback?codigo=…</c>; la App canjea ese código por la sesión.
/// Endpoints anónimos: no se envía token. Los fallos vuelven como <see cref="Result{T}"/>, nunca como excepción.
/// </summary>
public interface IPassPlatLoginExterno
{
    /// <summary>Proveedores que el tenant ofrece a la App (vacío si no hay ninguno válido).</summary>
    Task<Result<IReadOnlyList<ProveedorLoginExternoDto>>> ObtenerProveedoresLoginAsync(int idTenant, int idApp, CancellationToken ct = default);

    /// <summary>URL del proveedor a la que se redirige el navegador. Sin <paramref name="urlRetorno"/>, PassPlat vuelve a la base de la App.</summary>
    Task<Result<string>> IniciarLoginExternoAsync(string proveedor, int idTenant, int idApp, string? urlRetorno = null, CancellationToken ct = default);

    /// <summary>Canjea el código de un solo uso (2 min, ligado a la App) por la sesión.</summary>
    Task<Result<SesionLoginExternoDto>> CanjearCodigoAsync(string codigo, int idApp, CancellationToken ct = default);
}
