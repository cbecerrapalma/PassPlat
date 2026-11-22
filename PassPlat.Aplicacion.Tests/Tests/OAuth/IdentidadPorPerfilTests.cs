using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PassPlat.Aplicacion.Services;

namespace PassPlat.Aplicacion.Tests.Tests.OAuth;

/// <summary>
/// S129 · D6 — identidad externa sin id_token: Google y Microsoft toman la identidad del endpoint
/// de perfil llamado con el access_token.
/// </summary>
public class IdentidadPorPerfilTests
{
    private const string AccessToken = "at-prueba-s129";

    private static HttpResponseMessage Json(object cuerpo) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(cuerpo))
    };

    private static (Moq.Mock<IHttpClientFactory> Factory, List<HttpRequestMessage> Peticiones) Http(object perfil)
    {
        var peticiones = new List<HttpRequestMessage>();
        var handler = TestHelpers.CreateMockHttpHandler(req =>
        {
            peticiones.Add(req);
            return req.Method == HttpMethod.Post
                ? Json(new { access_token = AccessToken, refresh_token = "rt", expires_in = 3600, scope = "email profile" })
                : Json(perfil);
        });
        return (TestHelpers.CreateMockHttpClientFactory(handler.Object), peticiones);
    }

    [Fact]
    public async Task Google_PerfilValido_DevuelveClaimsDelPerfilConBearer()
    {
        var (factory, peticiones) = Http(new
        {
            sub = "google-sub-1",
            email = "ana@example.com",
            email_verified = true,
            name = "Ana",
            picture = "https://example.com/a.png"
        });
        var provider = new GoogleIdentityProvider(factory.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code", "https://localhost:5001/cb", "client", "secret", null, "verifier", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("google-sub-1", result.Value!.Sub);
        Assert.Equal("ana@example.com", result.Value.Email);
        Assert.True(result.Value.EmailVerificado);
        Assert.Equal("Ana", result.Value.Nombre);
        Assert.DoesNotContain("IdToken", result.Value.MetadataJson);

        var perfil = Assert.Single(peticiones, p => p.Method == HttpMethod.Get);
        Assert.Equal("https://www.googleapis.com/oauth2/v3/userinfo", perfil.RequestUri!.ToString());
        Assert.Equal("Bearer", perfil.Headers.Authorization!.Scheme);
        Assert.Equal(AccessToken, perfil.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Google_PerfilSinSub_Falla()
    {
        var (factory, _) = Http(new { email = "ana@example.com", email_verified = true });
        var provider = new GoogleIdentityProvider(factory.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code", "https://localhost:5001/cb", "client", "secret", null, "verifier", TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("USERINFO_INVALIDO", result.Error!.Code);
    }

    [Fact]
    public async Task Microsoft_Graph_EmailNuncaVerificado()
    {
        var (factory, peticiones) = Http(new
        {
            id = "ms-id-1",
            displayName = "Beto",
            mail = "beto@example.com",
            userPrincipalName = "beto@contoso.onmicrosoft.com"
        });
        var provider = new MicrosoftIdentityProvider(factory.Object);

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code", "https://localhost:5001/cb", "client", "secret", null, "verifier", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("ms-id-1", result.Value!.Sub);
        Assert.Equal("beto@example.com", result.Value.Email);
        Assert.False(result.Value.EmailVerificado);
        Assert.Equal("Beto", result.Value.Nombre);

        var perfil = Assert.Single(peticiones, p => p.Method == HttpMethod.Get);
        Assert.StartsWith("https://graph.microsoft.com/v1.0/me", perfil.RequestUri!.ToString());
        Assert.Equal(AccessToken, perfil.Headers.Authorization!.Parameter);
    }
}
