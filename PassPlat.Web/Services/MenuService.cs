using PassPlat.Aplicacion.Dtos.Catalogos;

namespace PassPlat.Web.Services;

/// <summary>
/// S83 · D5 — carga el menú de la app de la sesión (<c>GET api/modulos/menu</c>) y lo expone como
/// <see cref="CatalogoNavegacion"/>. Se recarga al cambiar la sesión y se vacía al cerrarla.
/// </summary>
public sealed class MenuService : IDisposable
{
    private readonly ApiClient _api;
    private readonly AuthService _auth;
    private readonly object _bloqueo = new();
    private Task? _carga;
    private int _version;

    public MenuService(ApiClient api, AuthService auth)
    {
        _api = api;
        _auth = auth;
        _auth.OnAuthStateChanged += AlCambiarSesion;
    }

    public CatalogoNavegacion Catalogo { get; private set; } = CatalogoNavegacion.Vacio;

    /// <summary>La última carga no obtuvo el menú.</summary>
    public bool Fallo { get; private set; }

    public event Action? Cambio;

    /// <summary>Carga el menú si aún no se cargó; las llamadas simultáneas comparten la misma carga.</summary>
    public Task AsegurarCargaAsync()
    {
        lock (_bloqueo)
        {
            return _carga ??= CargarAsync(_version);
        }
    }

    /// <summary>Descarta el catálogo actual y vuelve a pedirlo.</summary>
    public Task RecargarAsync()
    {
        lock (_bloqueo)
        {
            _version++;
            return _carga = CargarAsync(_version);
        }
    }

    private async Task CargarAsync(int version)
    {
        List<ModuloMenuDto>? arbol;
        try
        {
            arbol = await _api.GetAsync<List<ModuloMenuDto>>("api/modulos/menu");
        }
        catch (HttpRequestException)
        {
            arbol = null;
        }
        if (version != _version) return;

        Fallo = arbol is null;
        Catalogo = arbol is null ? CatalogoNavegacion.Vacio : new CatalogoNavegacion(arbol);
        Cambio?.Invoke();
    }

    private void AlCambiarSesion()
    {
        if (_auth.IsAuthenticated)
        {
            _ = RecargarAsync();
            return;
        }

        lock (_bloqueo)
        {
            _version++;
            _carga = null;
        }
        Fallo = false;
        Catalogo = CatalogoNavegacion.Vacio;
        Cambio?.Invoke();
    }

    public void Dispose() => _auth.OnAuthStateChanged -= AlCambiarSesion;
}
