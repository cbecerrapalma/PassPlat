using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CBP.Authentication.JwtBearer;
using CBP.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace PassPlat.Aplicacion.Test.Tests.Framework.S17;

/// <summary>
/// S17 — T1/T2/T3. Instrumentación Jwt_Validated / Jwt_Expired en JwtTokenService.
/// </summary>
public class JwtTokenServiceInstrumentationTests
{
    private const string Secret = "s17-test-secret-key-32-bytes-minimum!";

    private static JwtTokenService CreateService(CapturingLoggerService? capture = null)
    {
        var options = new JwtOptions
        {
            SecretKey = Secret,
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 60,
            ClockSkew = TimeSpan.Zero
        };
        return new JwtTokenService(options, NullLogger<JwtTokenService>.Instance, capture);
    }

    private static string CreateToken(DateTime expires, string secret = Secret)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: new[] { new Claim("sub", "s17-user") },
            expires: expires,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public void T1_ValidToken_Emits_Jwt_Validated()
    {
        var capture = new CapturingLoggerService();
        var service = CreateService(capture);

        var token = CreateToken(DateTime.UtcNow.AddHours(1));
        var principal = service.ValidateToken(token, out _);

        Assert.NotNull(principal);
        var emitted = capture.Events(LoggingEvents.JwtValidated);
        Assert.Single(emitted);
        Assert.Equal(LoggingScopes.Authentication, emitted[0].Scope);
        Assert.Equal(LoggingCategories.ApplicationAuth,
            emitted[0].Properties[LoggingPropertyNames.Category]);
        Assert.Equal(LoggingOperations.Validate,
            emitted[0].Properties[LoggingPropertyNames.Operation]);
    }

    [Fact]
    public void T2_ExpiredToken_Emits_Jwt_Expired_And_Returns_Null()
    {
        var capture = new CapturingLoggerService();
        var service = CreateService(capture);

        var token = CreateToken(DateTime.UtcNow.AddMinutes(-10));
        var principal = service.ValidateToken(token, out _);

        Assert.Null(principal);
        var expired = capture.Events(LoggingEvents.JwtExpired);
        Assert.Single(expired);
        Assert.Equal(LoggingScopes.Authentication, expired[0].Scope);
        Assert.NotNull(expired[0].Exception);
        Assert.Empty(capture.Events(LoggingEvents.JwtValidated));
    }

    [Fact]
    public void T3_InvalidToken_Does_Not_Emit_Jwt_Validated()
    {
        var capture = new CapturingLoggerService();
        var service = CreateService(capture);

        var token = CreateToken(DateTime.UtcNow.AddHours(1), secret: "different-secret-key-32-bytes-minimum!");

        var principal = service.ValidateToken(token, out _);

        Assert.Null(principal);
        Assert.Empty(capture.Events(LoggingEvents.JwtValidated));
        Assert.Empty(capture.Events(LoggingEvents.JwtExpired));
    }
}
