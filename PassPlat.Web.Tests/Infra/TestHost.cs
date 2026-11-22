using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;

namespace PassPlat.Web.Tests.Infra;

/// <summary>
/// Host de prueba: renderiza el <see cref="MudPopoverProvider"/> (las listas de MudAutocomplete
/// se dibujan ahi) y el <see cref="MudDialogProvider"/> (los dialogos, igual que en la app),
/// junto al contenido.
/// </summary>
internal sealed class TestHost : IComponent
{
    private RenderHandle _handle;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    public void Attach(RenderHandle renderHandle) => _handle = renderHandle;

    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        _handle.Render(BuildRenderTree);
        return Task.CompletedTask;
    }

    private void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenRegion(2);
        builder.AddContent(2, ChildContent);
        builder.CloseRegion();
    }
}
