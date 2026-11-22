using System.Collections.Concurrent;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Web.Pages.Grupos;
using PassPlat.Web.Services;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S92 · D3 — «Agregar miembro» pide los usuarios del tenant del grupo, no los del tenant de la
/// sesión: con sistema, en un grupo de otro tenant, ya no ofrece usuarios que la API rechaza
/// (SIN_MEMBRESIA_TENANT).
/// </summary>
public class S92AgregarMiembroGrupoTests : WebTestBase
{
    private const string UsuariosPagina = """
    {
      "items": [
        { "id": 9, "idTenant": 3, "nomUsuario": "admin_vestuario", "email": "admin@vestuario.test", "nombre": "Admin", "apellido": "Vestuario", "eliminado": false }
      ],
      "totalCount": 1,
      "pageNumber": 1,
      "pageSize": 100,
      "totalPages": 1,
      "hasPreviousPage": false,
      "hasNextPage": false
    }
    """;

    private readonly CapturaUrl _captura;

    public S92AgregarMiembroGrupoTests()
    {
        // La base registra solo la ruta; aquí se guarda también la query para ver idTenant.
        _captura = new CapturaUrl(Handler);
        var http = new HttpClient(_captura) { BaseAddress = new Uri("https://localhost:5001/") };
        Services.AddSingleton(http);
        Services.AddSingleton(sp => new AuthService(http, sp.GetRequiredService<NavigationManager>(), sp.GetRequiredService<IJSRuntime>()));
        Services.AddSingleton(sp => new ApiClient(http, sp.GetRequiredService<AuthService>()));
        CargarCatalogos();
        Handler.Responder("/api/usuarios/page", UsuariosPagina);
    }

    [Fact]
    public async Task S92_el_dialogo_pide_los_usuarios_del_tenant_del_grupo()
    {
        await AutenticarAsync();
        var cut = Render<TestHost>(p => p.AddChildContent<MudDialogProvider>());
        var parametros = new DialogParameters<AgregarMiembroGrupoDialog>
        {
            { x => x.Grupo, new GrupoDto { Id = 6, IdTenant = 3, Codigo = "BODEGA", Nombre = "Bodega" } }
        };

        await Services.GetRequiredService<IDialogService>().ShowAsync<AgregarMiembroGrupoDialog>("Agregar miembro", parametros);

        cut.WaitForAssertion(() => Assert.Contains(_captura.Urls,
            // S93 · D3 — ahora por membresía activa (miembroDe), no por tenant de origen (idTenant).
            u => u.StartsWith("/api/usuarios/page?", StringComparison.Ordinal) && u.Contains("miembroDe=3", StringComparison.Ordinal)
                && !u.Contains("idTenant=", StringComparison.Ordinal)));
        Assert.DoesNotContain(_captura.Urls, u => u == "/api/usuarios");
    }

    /// <summary>Envuelve el handler de la base y guarda la ruta con su query.</summary>
    private sealed class CapturaUrl(HttpMessageHandler interno) : DelegatingHandler(interno)
    {
        private readonly ConcurrentQueue<string> _urls = new();

        public IReadOnlyList<string> Urls => _urls.ToArray();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            _urls.Enqueue(request.RequestUri?.PathAndQuery ?? "");
            return base.SendAsync(request, ct);
        }
    }
}
