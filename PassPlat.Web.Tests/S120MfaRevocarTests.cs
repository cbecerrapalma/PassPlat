using Bunit;
using MfaIndex = PassPlat.Web.Pages.MFA.Index;

namespace PassPlat.Web.Tests;

/// <summary>S120 · D5 — <c>/mfa</c> ofrece «Revocar» en los métodos propios.</summary>
public class S120MfaRevocarTests : WebTestBase
{
    private const string Metodos = """
    [
      { "id": 7, "idUsuario": 1, "idTenant": 1, "idTipoMFA": 1, "idMFA": "sistema@passplat.app", "esPrincipal": true, "fecAlta": "2026-01-01T00:00:00", "idEstado": 1, "tipoMFANombre": "Email", "estadoMFANombre": "Activo" }
    ]
    """;

    [Fact]
    public async Task Revocar_visible_en_los_metodos_propios()
    {
        Handler.Responder("/api/usuarios", "[]");
        Handler.Responder("/api/mfa/metodos/1", Metodos);
        await AutenticarAsync(1, 1, "sistema", "MFA_VER");

        var cut = Render<MfaIndex>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button[aria-label='Revocar']")));
    }
}
