using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CBP.Authentication.JwtBearer;
using CBP.Logging.Interfaces;
using CBP.Logging.Models;
using Microsoft.AspNetCore.Http;

namespace PassPlat.Aplicacion.Services.Authentication;

public sealed class AuthenticationTokenIssuer
{
    private readonly IJwtTokenService _jwtService;
    private readonly JwtOptions _jwtOptions;
    private readonly ILoggerService _olog;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthenticationTokenIssuer(
        IJwtTokenService jwtService,
        JwtOptions jwtOptions,
        ILoggerService olog,
        IHttpContextAccessor httpContextAccessor)
    {
        _jwtService = jwtService;
        _jwtOptions = jwtOptions;
        _olog = olog;
        _httpContextAccessor = httpContextAccessor;
    }

    public AuthenticationTokenGenerationResult Generate(AuthenticationContext context, IReadOnlyCollection<Claim> permisoClaims)
    {
        var jti = Guid.NewGuid().ToString();
        var claims = BuildIdentityClaims(context, jti);
        claims.AddRange(permisoClaims);

        var accessToken = _jwtService.GenerateToken(claims);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var refreshHash = HashSHA256(refreshToken);
        var expiresAt = DateTime.Now.AddMinutes(_jwtOptions.RefreshTokenExpirationMinutes);

        _olog.LogInformation(new LogEvent
        {
            EventName = CBP.Logging.LoggingEvents.JwtGenerated,
            Scope = CBP.Logging.LoggingScopes.Authentication,
            Message = "JWT generado",
            Properties = new Dictionary<string, object?>
            {
                [CBP.Logging.LoggingPropertyNames.Category] = CBP.Logging.LoggingCategories.ApplicationAuth,
                [CBP.Logging.LoggingPropertyNames.Operation] = CBP.Logging.LoggingOperations.Execute,
                [CBP.Logging.LoggingPropertyNames.CorrelationId] = _httpContextAccessor.HttpContext?.Items[CBP.Logging.LoggingPropertyNames.HttpCorrelationIdKey] as string,
                [CBP.Logging.LoggingPropertyNames.UserId] = context.IdUsuario.ToString(),
                [CBP.Logging.LoggingPropertyNames.TenantId] = context.IdTenant,
            }
        });

        return new AuthenticationTokenGenerationResult(accessToken, refreshToken, refreshHash, jti, expiresAt);
    }

    private static List<Claim> BuildIdentityClaims(AuthenticationContext context, string jti)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, context.IdUsuario.ToString()),
            new("IdApp", context.IdApp.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti)
        };

        if (context.IdTenant.HasValue)
            claims.Add(new Claim("TenantId", context.IdTenant.Value.ToString()));

        if (context.IdUsuarioTenant.HasValue)
            claims.Add(new Claim("UsuarioTenantId", context.IdUsuarioTenant.Value.ToString()));

        if (context.EsSistema)
            claims.Add(new Claim("is_system", "true"));

        return claims;
    }

    private static string HashSHA256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
