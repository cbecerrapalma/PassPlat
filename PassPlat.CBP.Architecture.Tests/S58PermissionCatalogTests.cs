using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S58 · D1 — el permiso que exige cada política de la API y cada pantalla de la Web
/// debe existir en el catalogo de permisos (<c>dbo.Permisos</c>). Un codigo que no
/// existe en el catalogo no lo puede tener nadie, ni siquiera PLATFORM_ADMIN: la
/// pantalla queda siempre en «Acceso Denegado».
/// Fuente de verdad: <c>Seed/Configuracion/02_Permisos.sql</c>.
/// </summary>
public sealed class S58PermissionCatalogTests
{
    /// <summary>Politicas que no son un permiso del catalogo (las define el propio API).</summary>
    private static readonly string[] _politicasPropias = ["SystemOnly"];

    /// <summary>
    /// S58 · H10 — excepcion documentada: <c>EMAIL_API_ENVIAR</c> no existe en el catalogo
    /// y lo consume InventaNet. Arreglarlo exige permiso nuevo en la BD: plan aparte.
    /// S59 · D11 — el permiso ya existe en el catalogo (migracion 04_Permiso_Email_Api_Enviar)
    /// y en el seed, asi que ya no queda ninguna excepcion.
    /// </summary>
    private static readonly string[] _excepcionesDocumentadas = [];

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string RutaCatalogo()
    {
        var raiz = RaizDelRepo();
        return Path.GetFullPath(Path.Combine(raiz, "..", "BBDD", "PassPlat", "Seed", "Configuracion", "02_Permisos.sql"));
    }

    /// <summary>Codigos de permiso del catalogo (<c>N'CODIGO'</c> en el seed).</summary>
    private static HashSet<string> CodigosDelCatalogo()
    {
        var ruta = RutaCatalogo();
        Assert.True(File.Exists(ruta), $"No se encontro el catalogo de permisos en {ruta}.");
        var sql = File.ReadAllText(ruta);
        var codigos = Regex.Matches(sql, "N'([A-Z][A-Z0-9_]{2,})'")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(codigos.Count > 0, "El catalogo de permisos no contiene codigos.");
        return codigos;
    }

    private static IEnumerable<(string Archivo, int Linea, string Codigo)> PoliticasDeLaApi()
    {
        var carpeta = Path.Combine(RaizDelRepo(), "PassPlat.WebAPI", "Controllers");
        foreach (var archivo in Directory.GetFiles(carpeta, "*.cs", SearchOption.AllDirectories).OrderBy(x => x))
        {
            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
            {
                foreach (Match m in Regex.Matches(lineas[i], @"\[Authorize\(Policy\s*=\s*""([^""]+)""\)\]"))
                    yield return (archivo, i + 1, m.Groups[1].Value);
            }
        }
    }

    private static IEnumerable<(string Archivo, int Linea, string Codigo)> PermisosDeLaWeb()
    {
        var raiz = Path.Combine(RaizDelRepo(), "PassPlat.Web");
        var archivos = Directory.GetFiles(raiz, "*.razor", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(raiz, "*.cs", SearchOption.AllDirectories))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .OrderBy(x => x);

        foreach (var archivo in archivos)
        {
            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
            {
                foreach (Match m in Regex.Matches(lineas[i], @"TienePermiso\(""([^""]+)""\)"))
                    yield return (archivo, i + 1, m.Groups[1].Value);
                foreach (Match m in Regex.Matches(lineas[i], @"PermisoRequerido=""([^""]+)"""))
                    yield return (archivo, i + 1, m.Groups[1].Value);
            }
        }
    }

    [Fact]
    public void S58_Todas_las_politicas_de_la_API_existen_en_el_catalogo()
    {
        var catalogo = CodigosDelCatalogo();
        var fuera = new List<string>();

        foreach (var (archivo, linea, codigo) in PoliticasDeLaApi())
        {
            if (_politicasPropias.Contains(codigo)) continue;
            if (_excepcionesDocumentadas.Contains(codigo)) continue;
            if (!catalogo.Contains(codigo))
                fuera.Add($"{Path.GetFileName(archivo)}:{linea} -> {codigo}");
        }

        Assert.True(fuera.Count == 0,
            $"Politicas de la API que no existen en el catalogo de permisos (nadie puede tenerlas):\n  {string.Join("\n  ", fuera)}");
    }

    [Fact]
    public void S58_Todos_los_permisos_de_la_Web_existen_en_el_catalogo()
    {
        var catalogo = CodigosDelCatalogo();
        var fuera = new List<string>();

        foreach (var (archivo, linea, codigo) in PermisosDeLaWeb())
        {
            if (!catalogo.Contains(codigo))
                fuera.Add($"{archivo.Replace(RaizDelRepo() + Path.DirectorySeparatorChar, "")}:{linea} -> {codigo}");
        }

        Assert.True(fuera.Count == 0,
            $"Permisos de la Web que no existen en el catalogo:\n  {string.Join("\n  ", fuera)}");
    }

    [Fact]
    public void S58_Ya_no_queda_ninguna_excepcion_en_el_catalogo_de_permisos()
    {
        var api = PoliticasDeLaApi().Select(p => p.Codigo).Distinct().ToList();

        Assert.Contains("EMAIL_API_ENVIAR", api);
        Assert.Empty(_excepcionesDocumentadas);
    }
}
