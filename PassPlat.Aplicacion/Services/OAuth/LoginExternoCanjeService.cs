using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using CBP.Caching.Interfaces;
using CBP.Caching.Models;
using CBP.Results;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos.Repositories;

namespace PassPlat.Aplicacion.Services.OAuth;

/// <summary>
/// S125 — el login externo vuelve a la app que lo pidió (lista blanca: <c>Apps.UrlBase</c>)
/// con un código de un solo uso que la app canjea por la sesión; ningún token viaja en una URL.
/// Con MFA, la Web PassPlat lo completa a partir de una referencia opaca.
/// </summary>
public interface ILoginExternoCanjeService
{
    Task<Result<string>> ResolverUrlRetornoAsync(int idApp, string? urlRetorno, string urlBaseWeb, CancellationToken ct = default);
    Task<string> EmitirCodigoAsync(AuthResponseDto respuesta, int idApp, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> CanjearAsync(string codigo, int idApp, CancellationToken ct = default);
    Task<string> EmitirMfaPendienteAsync(MfaPendienteExterno pendiente, CancellationToken ct = default);
    Task<Result<int?>> ObtenerTipoMfaPendienteAsync(string mfaRef, CancellationToken ct = default);
    Task<Result<string>> ValidarMfaAsync(string mfaRef, string codigoMFA, CancellationToken ct = default);
}

/// <summary>S125 · D4 — login externo que espera el código MFA en la Web PassPlat.</summary>
public sealed record MfaPendienteExterno(int IdUsuario, int IdTenant, int IdApp, int IdMFAPrincipal, int? IdTipoMFA, string UrlRetorno);

/// <summary>S125 · D2 — lo que guarda un código de canje: la app que puede canjearlo y la sesión.</summary>
public sealed record CanjeLoginExternoEntrada(int IdApp, AuthResponseDto Respuesta);

public sealed class LoginExternoCanjeService : ILoginExternoCanjeService
{
    private static readonly TimeSpan VidaCodigo = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan VidaMfa = TimeSpan.FromMinutes(10);
    private const string RutaPorDefecto = "/signin-callback";
    private const string PrefijoCanje = "oauth_canje";
    private const string PrefijoMfa = "oauth_mfa";

    private readonly IAppRepository _apps;
    private readonly ICacheService _cache;
    private readonly IAuthService _authService;

    public LoginExternoCanjeService(IAppRepository apps, ICacheService cache, IAuthService authService)
    {
        _apps = apps;
        _cache = cache;
        _authService = authService;
    }

    public async Task<Result<string>> ResolverUrlRetornoAsync(int idApp, string? urlRetorno, string urlBaseWeb, CancellationToken ct = default)
    {
        var appResult = await _apps.GetByIdAsync(idApp, ct);
        if (appResult.IsFailure || appResult.Value is null || !appResult.Value.Activa)
            return Result<string>.Failure("APP_INVALIDA", "La aplicación no existe o está inactiva");

        var baseTexto = (string.IsNullOrWhiteSpace(appResult.Value.UrlBase) ? urlBaseWeb : appResult.Value.UrlBase).TrimEnd('/');
        if (!Uri.TryCreate(baseTexto, UriKind.Absolute, out var baseUri))
            return NoPermitida();

        if (string.IsNullOrWhiteSpace(urlRetorno))
            return Result<string>.Success(baseTexto + RutaPorDefecto);

        // Comparación por partes de Uri, no por texto: «https://localhost:57186.evil.test» no empieza por la base.
        if (!Uri.TryCreate(urlRetorno, UriKind.Absolute, out var destino)
            || !string.IsNullOrEmpty(destino.Fragment)
            || !string.Equals(destino.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(destino.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            || destino.Port != baseUri.Port)
            return NoPermitida();

        var rutaBase = baseUri.AbsolutePath.TrimEnd('/');
        var ruta = destino.AbsolutePath;
        if (rutaBase.Length > 0
            && !ruta.Equals(rutaBase, StringComparison.OrdinalIgnoreCase)
            && !ruta.StartsWith(rutaBase + "/", StringComparison.OrdinalIgnoreCase))
            return NoPermitida();

        return Result<string>.Success(urlRetorno);
    }

    public async Task<string> EmitirCodigoAsync(AuthResponseDto respuesta, int idApp, CancellationToken ct = default)
    {
        var codigo = NuevoCodigo();
        await _cache.SetAsync(Clave(PrefijoCanje, codigo), new CanjeLoginExternoEntrada(idApp, respuesta), new CacheEntryOptions(VidaCodigo), ct);
        return codigo;
    }

    public async Task<Result<AuthResponseDto>> CanjearAsync(string codigo, int idApp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return CodigoInvalido();

        var clave = Clave(PrefijoCanje, codigo);
        var entrada = await _cache.GetAsync<CanjeLoginExternoEntrada>(clave, ct);
        if (entrada is null)
            return CodigoInvalido();

        // Un solo uso: se borra antes de responder, también si la app no coincide.
        await _cache.RemoveAsync(clave, ct);
        if (entrada.IdApp != idApp)
            return CodigoInvalido();

        return Result<AuthResponseDto>.Success(entrada.Respuesta);
    }

    public async Task<string> EmitirMfaPendienteAsync(MfaPendienteExterno pendiente, CancellationToken ct = default)
    {
        var mfaRef = NuevoCodigo();
        await _cache.SetAsync(Clave(PrefijoMfa, mfaRef), pendiente, new CacheEntryOptions(VidaMfa), ct);
        return mfaRef;
    }

    public async Task<Result<int?>> ObtenerTipoMfaPendienteAsync(string mfaRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mfaRef))
            return Result<int?>.Failure("MFA_REF_INVALIDA", MensajeMfaVencido);

        var pendiente = await _cache.GetAsync<MfaPendienteExterno>(Clave(PrefijoMfa, mfaRef), ct);
        return pendiente is null
            ? Result<int?>.Failure("MFA_REF_INVALIDA", MensajeMfaVencido)
            : Result<int?>.Success(pendiente.IdTipoMFA, allowNull: true);
    }

    public async Task<Result<string>> ValidarMfaAsync(string mfaRef, string codigoMFA, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mfaRef))
            return Result<string>.Failure("MFA_REF_INVALIDA", MensajeMfaVencido);

        var clave = Clave(PrefijoMfa, mfaRef);
        var pendiente = await _cache.GetAsync<MfaPendienteExterno>(clave, ct);
        if (pendiente is null)
            return Result<string>.Failure("MFA_REF_INVALIDA", MensajeMfaVencido);

        var login = await _authService.CompletarLoginConMFAAsync(
            pendiente.IdUsuario, pendiente.IdTenant, pendiente.IdApp, pendiente.IdMFAPrincipal, codigoMFA,
            null, null, null, ct);
        if (login.IsFailure)
            return Result<string>.Failure(login.Error!); // el pendiente se conserva: se puede reintentar

        await _cache.RemoveAsync(clave, ct);
        var codigo = await EmitirCodigoAsync(login.Value!, pendiente.IdApp, ct);
        return Result<string>.Success(AgregarCodigo(pendiente.UrlRetorno, codigo));
    }

    /// <summary>Agrega <c>codigo</c> a la URL de retorno, con <c>?</c> o <c>&amp;</c> según ya tenga query.</summary>
    public static string AgregarCodigo(string url, string codigo) =>
        $"{url}{(url.Contains('?') ? '&' : '?')}codigo={Uri.EscapeDataString(codigo)}";

    private const string MensajeMfaVencido = "La verificación venció. Inicia sesión de nuevo";

    private static Result<string> NoPermitida() =>
        Result<string>.Failure("URL_RETORNO_NO_PERMITIDA", "La URL de retorno no está permitida para esta aplicación");

    private static Result<AuthResponseDto> CodigoInvalido() =>
        Result<AuthResponseDto>.Failure("CODIGO_INVALIDO", "El código es inválido o venció");

    private static string NuevoCodigo() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>La clave de caché guarda el SHA-256 del código, no el código.</summary>
    private static string Clave(string prefijo, string valor) =>
        $"{prefijo}:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(valor)))}";
}
