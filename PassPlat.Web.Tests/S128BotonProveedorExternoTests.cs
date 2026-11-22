using Bunit;
using CBP.Blazor.Mud.Login;
using MudBlazor;
using MudBlazor.Services;

namespace PassPlat.Web.Tests;

/// <summary>S128 — el botón de proveedor externo compartido (CBP.Blazor.Mud.Login) tal como lo usa la Web PassPlat.</summary>
public sealed class S128BotonProveedorExternoTests : IAsyncDisposable
{
    private readonly BunitContext _ctx = new();

    public S128BotonProveedorExternoTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();
    }

    public async ValueTask DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Marcas_de_proveedor_resuelven_icono_y_color()
    {
        Assert.Equal(Icons.Custom.Brands.Google, MarcasProveedor.IconoDe("google"));
        Assert.Equal(Icons.Custom.Brands.GitHub, MarcasProveedor.IconoDe("GITHUB"));
        Assert.Equal(Icons.Material.Filled.AccountCircle, MarcasProveedor.IconoDe("xyz"));
        Assert.Equal(Color.Error, MarcasProveedor.ColorDe("google", null));
        Assert.Equal(Color.Primary, MarcasProveedor.ColorDe("microsoft", "Primary"));
        Assert.Equal(Color.Default, MarcasProveedor.ColorDe("xyz", "noexiste"));
    }

    [Fact]
    public void Boton_ancho_muestra_texto_icono_y_respeta_cargando()
    {
        var normal = _ctx.Render<BotonProveedorExterno>(p => p
            .Add(x => x.Codigo, "GOOGLE")
            .Add(x => x.Nombre, "Google"));

        var boton = normal.Find("[data-testid=proveedor-externo-google]");
        Assert.Contains("Continuar con Google", boton.TextContent);
        Assert.NotNull(boton.QuerySelector("svg"));
        Assert.False(boton.HasAttribute("disabled"));

        var cargando = _ctx.Render<BotonProveedorExterno>(p => p
            .Add(x => x.Codigo, "GOOGLE")
            .Add(x => x.Nombre, "Google")
            .Add(x => x.Cargando, true));

        Assert.True(cargando.Find("[data-testid=proveedor-externo-google]").HasAttribute("disabled"));
        Assert.NotEmpty(cargando.FindAll(".mud-progress-circular"));
    }
}
