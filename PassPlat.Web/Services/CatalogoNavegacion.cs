using CBP.Blazor.Mud;
using MudBlazor;
using PassPlat.Aplicacion.Dtos.Catalogos;

namespace PassPlat.Web.Services;

/// <summary>
/// S83 · D4 — catálogo de navegación de PassPlat.Web a partir del árbol de <c>api/modulos/menu</c>:
/// grupos del menú lateral, buscador y título de la barra. Clase pura: no llama a la red ni a JS.
/// </summary>
public sealed class CatalogoNavegacion
{
    /// <summary>Catálogo sin módulos (antes de la primera carga o sin sesión).</summary>
    public static CatalogoNavegacion Vacio { get; } = new([]);

    private const string TituloRespaldo = "PassPlat";
    private const int MaximoResultados = 8;

    private static readonly Dictionary<string, string> Alias = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/dispositivos-confiables"] = "/disp-confiables",
        ["/admin/dashboard-enterprise"] = "/admin/panel",
        ["/admin/dashboard-operacional"] = "/admin/panel",
        ["/admin/iam-dashboard"] = "/admin/panel"
    };

    // Fichas y páginas sin módulo: prefijo de la ruta (sin la barra inicial), título e ícono fijo.
    private static readonly (string Prefijo, string Titulo, string Icono)[] Fichas =
    [
        ("usuario/", "Ficha de usuario", Icons.Material.Filled.Person),
        ("tenant/", "Ficha de tenant", Icons.Material.Filled.Business),
        ("rol/", "Ficha de rol", Icons.Material.Filled.Badge),
        ("grupo/", "Ficha de grupo", Icons.Material.Filled.Group),
        ("permiso/", "Ficha de permiso", Icons.Material.Filled.Security),
        ("acceso/", "Ficha de acceso", Icons.Material.Filled.Key),
        ("dispositivos/", "Ficha de dispositivo", Icons.Material.Filled.DevicesOther),
        ("federacion/confproviden/", "Configuración de proveedor", Icons.Material.Filled.Settings),
        ("federacion/iden-ext/", "Identidad externa", Icons.Material.Filled.Link),
        ("404", "Página no encontrada", Icons.Material.Filled.SentimentDissatisfied)
    ];

    private readonly List<ModuloMenuDto> _conRuta = [];
    private readonly Dictionary<string, ModuloMenuDto> _porRuta = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ModuloMenuDto[]> _rastroPorRuta = new(StringComparer.OrdinalIgnoreCase);

    public CatalogoNavegacion(IReadOnlyList<ModuloMenuDto> arbol)
    {
        var grupos = new List<(ModuloMenuDto Raiz, IReadOnlyList<ModuloMenuDto> Enlaces)>();
        foreach (var raiz in arbol)
        {
            var enlaces = new List<ModuloMenuDto>();
            AplanarMenu(raiz.Hijos, enlaces);
            if (enlaces.Count > 0 || !string.IsNullOrWhiteSpace(raiz.Ruta))
                grupos.Add((raiz, enlaces));
            Indexar(raiz, []);
        }
        Grupos = grupos;
    }

    /// <summary>
    /// Raíces del menú en orden. Sin <c>Enlaces</c>, la raíz es un enlace suelto (su <c>Ruta</c>);
    /// con enlaces, es un grupo con sus descendientes visibles en el menú, en preorden.
    /// </summary>
    public IReadOnlyList<(ModuloMenuDto Raiz, IReadOnlyList<ModuloMenuDto> Enlaces)> Grupos { get; }

    /// <summary>Entradas con ruta (estén o no en el menú) cuyo nombre o descripción contiene el texto, sin distinguir mayúsculas ni tildes.</summary>
    public IReadOnlyList<ModuloMenuDto> Buscar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 2) return [];
        var buscado = Normalizar(texto.Trim());

        bool Empieza(ModuloMenuDto m) => Normalizar(m.Nombre).StartsWith(buscado, StringComparison.Ordinal)
            || Normalizar(m.Descripcion).StartsWith(buscado, StringComparison.Ordinal);
        bool Contiene(ModuloMenuDto m) => Normalizar(m.Nombre).Contains(buscado, StringComparison.Ordinal)
            || Normalizar(m.Descripcion).Contains(buscado, StringComparison.Ordinal);

        return _conRuta.Where(Empieza)
            .Concat(_conRuta.Where(m => !Empieza(m) && Contiene(m)))
            .Take(MaximoResultados)
            .ToList();
    }

    /// <summary>Título, subtítulo e ícono de la barra para una dirección. Nunca devuelve la ruta cruda ni lanza.</summary>
    public (string Titulo, string Subtitulo, string Icono) TituloDe(string? uri)
    {
        var ruta = RutaDe(uri);
        if (ruta is null) return (TituloRespaldo, "", Icons.Material.Filled.Description);

        if (_porRuta.TryGetValue(ruta, out var modulo)
            || (Alias.TryGetValue(ruta, out var destino) && _porRuta.TryGetValue(destino, out modulo)))
            return (modulo.Nombre, modulo.Descripcion ?? "", IconosMud.PorNombre(modulo.Icono));

        var sinBarra = ruta.TrimStart('/');
        foreach (var (prefijo, titulo, icono) in Fichas)
        {
            if (sinBarra.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
                return (titulo, "", icono);
        }

        return (TituloRespaldo, "", Icons.Material.Filled.Description);
    }

    /// <summary>
    /// S86 · D2 — módulos desde la raíz hasta el de la ruta (relativa, sin barra inicial o con ella; se ignoran
    /// consulta y fragmento; alias incluidos), para las migas de pan. Vacío si la ruta no tiene módulo.
    /// </summary>
    public IReadOnlyList<ModuloMenuDto> RastroDe(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return [];
        var clave = NormalizarRuta(ruta.Split('?', '#')[0]);
        if (_rastroPorRuta.TryGetValue(clave, out var rastro)
            || (Alias.TryGetValue(clave, out var destino) && _rastroPorRuta.TryGetValue(destino, out rastro)))
            return rastro;
        return [];
    }

    private static void AplanarMenu(IEnumerable<ModuloMenuDto> nodos, List<ModuloMenuDto> destino)
    {
        foreach (var nodo in nodos)
        {
            if (nodo.EsVisibleMenu && !string.IsNullOrWhiteSpace(nodo.Ruta))
                destino.Add(nodo);
            AplanarMenu(nodo.Hijos, destino);
        }
    }

    private void Indexar(ModuloMenuDto nodo, List<ModuloMenuDto> camino)
    {
        camino.Add(nodo);
        if (!string.IsNullOrWhiteSpace(nodo.Ruta))
        {
            var ruta = NormalizarRuta(nodo.Ruta);
            if (_porRuta.TryAdd(ruta, nodo))
            {
                _conRuta.Add(nodo);
                _rastroPorRuta[ruta] = [.. camino];
            }
        }
        foreach (var hijo in nodo.Hijos)
            Indexar(hijo, camino);
        camino.RemoveAt(camino.Count - 1);
    }

    private static string? RutaDe(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri) || !Uri.TryCreate(uri, UriKind.Absolute, out var absoluta))
            return null;
        return NormalizarRuta(absoluta.AbsolutePath);
    }

    private static string NormalizarRuta(string ruta) => "/" + ruta.Trim().Trim('/');

    // Sin depender de la globalización de WASM: minúsculas invariantes y un mapa fijo de tildes.
    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        var chars = texto.ToLowerInvariant().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                'á' => 'a',
                'é' => 'e',
                'í' => 'i',
                'ó' => 'o',
                'ú' or 'ü' => 'u',
                'ñ' => 'n',
                _ => chars[i]
            };
        }
        return new string(chars);
    }
}
