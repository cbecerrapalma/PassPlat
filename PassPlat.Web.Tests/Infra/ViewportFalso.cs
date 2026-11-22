using MudBlazor;
using MudBlazor.Services;

namespace PassPlat.Web.Tests.Infra;

/// <summary>
/// S55.1 · K4 (D4) — viewport fijo para las pruebas del menu lateral: el menu lateral
/// responsive de MudBlazor se cierra solo segun el breakpoint, y asi la prueba mide
/// solo la decision de la aplicacion.
/// </summary>
internal sealed class ViewportFalso : IBrowserViewportService
{
    public Breakpoint BreakpointActual { get; set; } = Breakpoint.Lg;

    public BrowserWindowSize Ventana { get; set; } = new() { Width = 1280, Height = 800 };

    public ResizeOptions ResizeOptions { get; set; } = new();

    public Task<Breakpoint> GetCurrentBreakpointAsync() => Task.FromResult(BreakpointActual);

    public Task<BrowserWindowSize> GetCurrentBrowserWindowSizeAsync() => Task.FromResult(Ventana);

    public Task<bool> IsBreakpointWithinWindowSizeAsync(Breakpoint breakpoint)
        => Task.FromResult(breakpoint <= BreakpointActual);

    public Task<bool> IsBreakpointWithinReferenceSizeAsync(Breakpoint breakpoint, Breakpoint reference)
        => Task.FromResult(reference <= BreakpointActual);

    public Task<bool> IsMediaQueryMatchAsync(string query) => Task.FromResult(true);

    public Task SubscribeAsync(IBrowserViewportObserver observer, bool fireImmediately = false) => Task.CompletedTask;

    public Task SubscribeAsync(Guid id, Action<BrowserViewportEventArgs> callback, ResizeOptions? options = null, bool fireImmediately = true)
        => Task.CompletedTask;

    public Task SubscribeAsync(Guid id, Func<BrowserViewportEventArgs, Task> callback, ResizeOptions? options = null, bool fireImmediately = true)
        => Task.CompletedTask;

    public Task UnsubscribeAsync(IBrowserViewportObserver observer) => Task.CompletedTask;

    public Task UnsubscribeAsync(Guid id) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
