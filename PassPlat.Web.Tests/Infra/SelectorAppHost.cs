using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Web.Shared;

namespace PassPlat.Web.Tests.Infra;

/// <summary>
/// Host de prueba con valor enlazado en las dos direcciones, como el
/// <c>@bind-Value</c> de las paginas: al cambiar el valor del selector, el host
/// lo conserva y vuelve a dibujar, igual que hace el padre real.
/// </summary>
internal sealed class SelectorAppHost : IComponent
{
    private RenderHandle _handle;
    private AppDto? _valor;

    [Parameter] public IReadOnlyList<AppDto> Items { get; set; } = [];

    [Parameter] public AppDto? ValorInicial { get; set; }

    [Parameter] public EventCallback<AppDto?> ValorCambiado { get; set; }

    [Parameter] public int MaximoVisible { get; set; } = 50;

    [Parameter] public bool Required { get; set; }

    [Parameter] public string? RequiredError { get; set; }

    public void Attach(RenderHandle renderHandle) => _handle = renderHandle;

    public Task SetParametersAsync(ParameterView parameters)
    {
        var inicial = parameters.GetValueOrDefault<AppDto?>(nameof(ValorInicial));
        parameters.SetParameterProperties(this);
        if (inicial is not null && !ReferenceEquals(inicial, _valor))
        {
            _valor = inicial;
        }
        _handle.Render(Dibujar);
        return Task.CompletedTask;
    }

    private void Dibujar(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<SelectorCatalogo<AppDto>>(1);
        builder.AddAttribute(1, nameof(SelectorCatalogo<AppDto>.Items), Items);
        builder.AddAttribute(1, nameof(SelectorCatalogo<AppDto>.SearchTextSelector), (Func<AppDto, string?>)(a => $"{a.Nombre} {a.Codigo}"));
        builder.AddAttribute(1, nameof(SelectorCatalogo<AppDto>.ToStringFunc), (Func<AppDto, string>)(a => $"{a.Nombre} ({a.Codigo})"));
        builder.AddAttribute(1, nameof(SelectorCatalogo<AppDto>.Label), "App");
        builder.AddAttribute(1, nameof(SelectorCatalogo<AppDto>.MaximoVisible), MaximoVisible);
        builder.AddAttribute(1, nameof(SelectorCatalogo<AppDto>.Required), Required);
        builder.AddAttribute(1, nameof(SelectorCatalogo<AppDto>.RequiredError), RequiredError);
        builder.AddAttribute(1, nameof(SelectorCatalogo<AppDto>.Value), _valor);
        builder.AddAttribute(1, nameof(SelectorCatalogo<AppDto>.ValueChanged), EventCallback.Factory.Create<AppDto?>(this, AlCambiarValor));
        builder.CloseComponent();
    }

    private async Task AlCambiarValor(AppDto? valor)
    {
        _valor = valor;
        await ValorCambiado.InvokeAsync(valor);
        _handle.Render(Dibujar);
    }
}
