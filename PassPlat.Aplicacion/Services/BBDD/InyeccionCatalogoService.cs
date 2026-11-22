using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CBP.Results;
using CBP.Results.Errors;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

/// <summary>S99 D3 — Inyección transaccional del catálogo de una app (módulos, asociaciones y permisos de ventana).</summary>
public interface IInyeccionCatalogoService
{
    /// <summary>
    /// Simula o aplica el catálogo. Aplica sobre las entidades del contexto: quien llama hace UN <c>SaveChangesAsync</c>
    /// si el resultado es correcto y no se simuló. <paramref name="esSistema"/> e <paramref name="idAppToken"/> salen del token.
    /// </summary>
    Task<Result<ResultadoInyeccionCatalogoDto>> InyectarAsync(
        string codigoApp, InyeccionCatalogoDto catalogo, bool esSistema, int? idAppToken, CancellationToken ct = default);
}

public sealed class InyeccionCatalogoService : IInyeccionCatalogoService
{
    private const int MaxCodigo = 50, MaxNombre = 100, MaxRuta = 255, MaxIcono = 100, MaxErrores = 50, MaxAcciones = 250;

    private const string CrearModulo = "CrearModulo", ActualizarModulo = "ActualizarModulo", AsociarModulo = "AsociarModulo",
        CrearPermiso = "CrearPermiso", MoverPermiso = "MoverPermiso", DesactivarModulo = "DesactivarModulo", DesactivarPermiso = "DesactivarPermiso",
        Reactivar = "reactivar";

    /// <summary>
    /// Acciones estándar, en orden (Orden 1–4) y con el verbo de su nombre. S103: solo VER se crea siempre;
    /// CREAR, EDITAR y ELIMINAR, solo si el catálogo las declara (<see cref="AccionesDe"/>).
    /// </summary>
    private static readonly (string Accion, string Verbo)[] Crud = [("VER", "leer"), ("CREAR", "crear"), ("EDITAR", "modificar"), ("ELIMINAR", "eliminar")];

    private static readonly Regex RegexAccion = new("^[A-Z][A-Z0-9]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly IAppRepository _apps;
    private readonly ITipoModuloRepository _tipos;
    private readonly IModuloRepository _modulos;
    private readonly IPermisoRepository _permisos;
    private readonly IAppModuloRepository _appModulos;
    private readonly ILogger<InyeccionCatalogoService> _log;

    public InyeccionCatalogoService(IAppRepository apps, ITipoModuloRepository tipos, IModuloRepository modulos,
        IPermisoRepository permisos, IAppModuloRepository appModulos, ILogger<InyeccionCatalogoService> log)
    {
        _apps = apps;
        _tipos = tipos;
        _modulos = modulos;
        _permisos = permisos;
        _appModulos = appModulos;
        _log = log;
    }

    /// <summary>Un permiso deseado: el VER y las acciones declaradas de cada ventana, más los sueltos.</summary>
    private sealed record PermisoDeseado(string Codigo, string Nombre, string CodigoModulo, byte Orden);

    public async Task<Result<ResultadoInyeccionCatalogoDto>> InyectarAsync(
        string codigoApp, InyeccionCatalogoDto catalogo, bool esSistema, int? idAppToken, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoApp);
        ArgumentNullException.ThrowIfNull(catalogo);

        if (!esSistema)
            return Falla("CATALOGO_SOLO_SISTEMA", "Solo un usuario maestro de PassPlat puede inyectar el catálogo de una app.", 403, ErrorType.Forbidden);

        var appR = await _apps.ObtenerPorCodigoAsync(codigoApp, ct);
        if (appR.IsFailure) return Result<ResultadoInyeccionCatalogoDto>.Failure(appR.Error!);
        var app = appR.Value;
        if (app is null || !app.Activa)
            return Falla("CATALOGO_APP_NO_EXISTE", $"La aplicación '{codigoApp}' no existe o no está activa.", 404, ErrorType.NotFound);
        if (idAppToken != app.Id)
            return Falla("CATALOGO_OTRA_APP", "Solo la app que ejecuta la inyección puede inyectar su propio catálogo.", 403, ErrorType.Forbidden);

        // ---- Validación del contrato (D2) ----
        var errores = Validar(codigoApp, catalogo);
        var tipoR = await _tipos.ObtenerPorCodigoAsync(catalogo.CodigoTipoModulo ?? string.Empty, ct);
        if (tipoR.IsFailure) return Result<ResultadoInyeccionCatalogoDto>.Failure(tipoR.Error!);
        var tipo = tipoR.Value;
        if (tipo is null) errores.Add($"El tipo de módulo '{catalogo.CodigoTipoModulo}' no existe.");
        if (!catalogo.Simular && string.IsNullOrWhiteSpace(catalogo.HuellaEsperada))
            errores.Add("HuellaEsperada es obligatoria cuando no se simula.");
        if (errores.Count > 0) return Invalido(errores);

        // ---- Estado actual (con seguimiento: tablas chicas) ----
        var modulosR = await _modulos.GetAllAsync(false, ct);
        if (modulosR.IsFailure) return Result<ResultadoInyeccionCatalogoDto>.Failure(modulosR.Error!);
        var permisosR = await _permisos.GetAllAsync(false, ct);
        if (permisosR.IsFailure) return Result<ResultadoInyeccionCatalogoDto>.Failure(permisosR.Error!);
        var asocR = await _appModulos.GetAllAsync(false, ct);
        if (asocR.IsFailure) return Result<ResultadoInyeccionCatalogoDto>.Failure(asocR.Error!);

        var modulos = modulosR.Value;
        var porId = modulos.GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First());
        var porCodigo = modulos.GroupBy(m => m.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var permisosPorCodigo = permisosR.Value.GroupBy(p => p.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var asociaciones = asocR.Value;
        var asociacionesApp = asociaciones.Where(a => a.IdApp == app.Id).GroupBy(a => a.IdModulo).ToDictionary(g => g.Key, g => g.First());

        var universo = Universo(codigoApp, modulos);
        var huella = Huella(universo, permisosR.Value, asociacionesApp.Values, porId);

        if (!catalogo.Simular && !string.Equals(huella, catalogo.HuellaEsperada, StringComparison.OrdinalIgnoreCase))
            return Falla("CATALOGO_ESTADO_CAMBIO", "El estado de PassPlat cambió desde que se generó el plan. Revisar el plan nuevo y volver a inyectar.",
                409, ErrorType.Conflict);

        // ---- Colisiones: módulos o permisos que ya pertenecen (activos) a otra app ----
        var deOtraApp = asociaciones.Where(a => a.Activo && a.IdApp != app.Id).Select(a => a.IdModulo).ToHashSet();
        var colisiones = new List<string>();
        foreach (var m in catalogo.Modulos.Where(m => porCodigo.TryGetValue(m.Codigo, out var e) && deOtraApp.Contains(e.Id)))
            colisiones.Add($"El módulo '{m.Codigo}' ya está asociado a otra aplicación.");

        var ventanas = catalogo.Modulos.Where(EsVentana).OrderBy(m => m.Orden).ThenBy(m => m.Codigo, StringComparer.Ordinal).ToList();
        var deseados = PermisosDeseados(ventanas, catalogo.Permisos);
        foreach (var p in deseados.Where(p => permisosPorCodigo.TryGetValue(p.Codigo, out var e) && deOtraApp.Contains(e.IdModulo)))
            colisiones.Add($"El permiso '{p.Codigo}' ya pertenece a un módulo de otra aplicación.");
        if (colisiones.Count > 0) return Invalido(colisiones, "CATALOGO_COLISION");

        // ---- Plan: cada operación trae la acción que la aplica ----
        var ops = new List<OperacionCatalogoDto>();
        var acciones = new List<Action>();
        var entidades = new Dictionary<string, Modulo>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in porCodigo) entidades[kv.Key] = kv.Value;

        var idTipoNuevo = porCodigo.TryGetValue(codigoApp, out var raizExistente) ? raizExistente.IdTipoModulo : tipo!.Id;
        var agrupadores = catalogo.Modulos.Where(m => !EsVentana(m))
            .OrderBy(m => string.Equals(m.Codigo, codigoApp, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(m => m.Orden).ThenBy(m => m.Codigo, StringComparer.Ordinal).ToList();
        var deseadosModulos = agrupadores.Concat(ventanas).ToList();

        foreach (var m in deseadosModulos.Where(m => !porCodigo.ContainsKey(m.Codigo)))
            entidades[m.Codigo] = Modulo.Crear(m.Codigo, m.Nombre, idTipoNuevo, null, m.Ruta, m.Icono, (short)m.Orden, m.EsVisibleMenu);

        // 1) raíz y áreas: crear, o solo reactivar/reubicar (se conservan nombre y orden). 2) ventanas: crear o sincronizar.
        foreach (var m in deseadosModulos)
        {
            var esRaiz = string.Equals(m.Codigo, codigoApp, StringComparison.Ordinal);
            var ventana = EsVentana(m);
            var padreCodigo = esRaiz ? null : m.CodigoPadre;
            var padre = padreCodigo is null ? null : entidades[padreCodigo];
            var entidad = entidades[m.Codigo];

            if (!porCodigo.ContainsKey(m.Codigo))
            {
                entidad.Descripcion = m.Descripcion;
                ops.Add(new OperacionCatalogoDto { Tipo = CrearModulo, Codigo = m.Codigo, Nombre = m.Nombre, CodigoModulo = padreCodigo });
                acciones.Add(() =>
                {
                    entidad.ModuloPadre = padre;
                    _modulos.Add(entidad);
                });
                continue;
            }

            var cambios = new List<string>();
            var padreActual = entidad.IdModuloPadre is int pid && porId.TryGetValue(pid, out var pe) ? pe.Codigo : null;
            if (!entidad.Activo) cambios.Add(Reactivar);
            if (!string.Equals(padreActual, padreCodigo, StringComparison.OrdinalIgnoreCase)) cambios.Add("padre");
            // S100 D5a: en una ventana existente PassPlat manda en nombre, ícono, orden y visibilidad (los administra el dueño); solo la ruta viene del código.
            if (ventana && !string.Equals(entidad.Ruta, m.Ruta, StringComparison.Ordinal)) cambios.Add("ruta");
            if (cambios.Count == 0) continue;
            ops.Add(new OperacionCatalogoDto { Tipo = ActualizarModulo, Codigo = m.Codigo, Nombre = entidad.Nombre, CodigoModulo = padreCodigo, Detalle = string.Join(", ", cambios) });
            acciones.Add(() =>
            {
                entidad.Activo = true;
                entidad.ModuloPadre = padre;
                if (padre is { Id: > 0 }) entidad.IdModuloPadre = padre.Id;
                if (padre is null) entidad.IdModuloPadre = null;
                if (ventana) entidad.Ruta = m.Ruta;
                entidad.FecMod = DateTime.Now;
            });
        }

        // 3) asociaciones a la app: crear la fila o reactivar la inactiva (UQ_AppsModulos_AppModulo no admite otra, H4).
        foreach (var m in deseadosModulos)
        {
            var entidad = entidades[m.Codigo];
            asociacionesApp.TryGetValue(entidad.Id, out var asociacion);
            if (entidad.Id == 0 || asociacion is null)
            {
                ops.Add(new OperacionCatalogoDto { Tipo = AsociarModulo, Codigo = m.Codigo, Nombre = m.Nombre });
                acciones.Add(() => _appModulos.Add(new AppModulo { App = app, IdApp = app.Id, Modulo = entidad, Activo = true, FecCrea = DateTime.Now }));
            }
            else if (!asociacion.Activo)
            {
                ops.Add(new OperacionCatalogoDto { Tipo = AsociarModulo, Codigo = m.Codigo, Nombre = m.Nombre, Detalle = Reactivar });
                acciones.Add(() => asociacion.Activo = true);
            }
        }

        // 4) permisos: crear (o reactivar) los que faltan y mover los que están en otro módulo. Nunca renombra ni borra.
        var creaciones = new List<(OperacionCatalogoDto Op, Action Accion)>();
        var movimientos = new List<(OperacionCatalogoDto Op, Action Accion)>();
        foreach (var d in deseados)
        {
            var destino = entidades[d.CodigoModulo];
            if (!permisosPorCodigo.TryGetValue(d.Codigo, out var existente))
            {
                creaciones.Add((new OperacionCatalogoDto { Tipo = CrearPermiso, Codigo = d.Codigo, Nombre = d.Nombre, CodigoModulo = d.CodigoModulo },
                    () => _permisos.Add(new Permiso { Codigo = d.Codigo, Nombre = d.Nombre, IdModulo = destino.Id, Modulo = destino, Orden = d.Orden, Activo = true, FecCrea = DateTime.Now })));
                continue;
            }
            if (!existente.Activo)
                creaciones.Add((new OperacionCatalogoDto { Tipo = CrearPermiso, Codigo = d.Codigo, Nombre = existente.Nombre, CodigoModulo = d.CodigoModulo, Detalle = Reactivar },
                    () => existente.Activo = true));
            if (destino.Id == 0 || existente.IdModulo != destino.Id)
                movimientos.Add((new OperacionCatalogoDto { Tipo = MoverPermiso, Codigo = d.Codigo, Nombre = existente.Nombre, CodigoModulo = d.CodigoModulo },
                    () =>
                    {
                        existente.Modulo = destino;
                        if (destino.Id > 0) existente.IdModulo = destino.Id;
                    }));
        }
        foreach (var (op, accion) in creaciones.Concat(movimientos))
        {
            ops.Add(op);
            acciones.Add(accion);
        }

        // 5) sobrantes: módulos activos del universo que no están en el catálogo, y (S100 D5b) permisos activos de esos módulos que ya no se desean.
        //    Se desactivan solo los confirmados.
        var codigosDeseados = deseadosModulos.Select(m => m.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sobrantes = universo.Where(m => m.Activo && !codigosDeseados.Contains(m.Codigo))
            .OrderBy(m => m.Codigo, StringComparer.Ordinal).ToList();
        var idsUniverso = universo.Select(m => m.Id).ToHashSet();
        var permisosDeseados = deseados.Select(d => d.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var permisosSobrantes = permisosR.Value.Where(p => p.Activo && idsUniverso.Contains(p.IdModulo) && !permisosDeseados.Contains(p.Codigo))
            .GroupBy(p => p.Codigo, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .OrderBy(p => p.Codigo, StringComparer.Ordinal).ToList();
        if (!catalogo.Simular)
        {
            var confirmados = (catalogo.DesactivarSobrantes ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var noSobrantes = confirmados.Where(c => !sobrantes.Any(s => string.Equals(s.Codigo, c, StringComparison.OrdinalIgnoreCase)) &&
                                                     !permisosSobrantes.Any(s => string.Equals(s.Codigo, c, StringComparison.OrdinalIgnoreCase))).ToList();
            if (noSobrantes.Count > 0)
                return Falla("CATALOGO_SOBRANTE_INVALIDO",
                    $"Solo se pueden desactivar sobrantes del plan; no lo son: {string.Join(", ", noSobrantes)}.", 400, ErrorType.BadRequest);
            foreach (var s in sobrantes.Where(s => confirmados.Contains(s.Codigo, StringComparer.OrdinalIgnoreCase)))
            {
                ops.Add(new OperacionCatalogoDto { Tipo = DesactivarModulo, Codigo = s.Codigo, Nombre = s.Nombre });
                acciones.Add(() =>
                {
                    s.Activo = false;
                    s.FecMod = DateTime.Now;
                });
            }
            foreach (var p in permisosSobrantes.Where(p => confirmados.Contains(p.Codigo, StringComparer.OrdinalIgnoreCase)))
            {
                ops.Add(new OperacionCatalogoDto { Tipo = DesactivarPermiso, Codigo = p.Codigo, Nombre = p.Nombre, CodigoModulo = Cod(p.IdModulo, porId) });
                acciones.Add(() => p.Activo = false);
            }
            foreach (var a in acciones) a();
        }

        _log.LogInformation("Inyección de catálogo de {App}: simulado={Simulado}, operaciones={Operaciones}, sobrantes={Sobrantes}.",
            codigoApp, catalogo.Simular, ops.Count, sobrantes.Count + permisosSobrantes.Count);
        return Result<ResultadoInyeccionCatalogoDto>.Success(new ResultadoInyeccionCatalogoDto
        {
            CodigoApp = codigoApp,
            Simulado = catalogo.Simular,
            HuellaRemota = huella,
            Operaciones = ops,
            Sobrantes = sobrantes.Select(s => s.Codigo).Concat(permisosSobrantes.Select(p => p.Codigo)).ToList()
        });
    }

    private static string? Cod(int idModulo, IReadOnlyDictionary<int, Modulo> porId) => porId.TryGetValue(idModulo, out var m) ? m.Codigo : null;

    // ---------------- reglas ----------------

    /// <summary>Ventana = módulo con ruta (la raíz nunca lo es).</summary>
    private static bool EsVentana(ModuloCatalogoDto m) => !string.IsNullOrWhiteSpace(m.Ruta);

    private static string CodigoPermisoVentana(string codigoVentana, string accion) => codigoVentana.Replace('.', '_') + "_" + accion;

    /// <summary>Acciones adicionales de una ventana: las que no son CRUD, sin repetir y en orden ordinal.</summary>
    private static List<string> Extras(ModuloCatalogoDto ventana) => (ventana.Acciones ?? [])
        .Where(a => !string.IsNullOrWhiteSpace(a) && !Crud.Any(c => c.Accion == a))
        .Distinct(StringComparer.Ordinal).OrderBy(a => a, StringComparer.Ordinal).ToList();

    /// <summary>
    /// S103 — Acciones que una ventana tendrá como permisos: <c>VER</c> siempre (abre la ventana y su menú) y, de las demás,
    /// solo las que el catálogo declara. CREAR, EDITAR y ELIMINAR conservan su orden (2–4) y su verbo; el resto va después (5…).
    /// </summary>
    private static List<(string Accion, string Verbo, byte Orden)> AccionesDe(ModuloCatalogoDto ventana)
    {
        var declaradas = (ventana.Acciones ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).ToHashSet(StringComparer.Ordinal);
        var lista = new List<(string Accion, string Verbo, byte Orden)>();
        for (var i = 0; i < Crud.Length; i++)
            if (i == 0 || declaradas.Contains(Crud[i].Accion)) lista.Add((Crud[i].Accion, Crud[i].Verbo, (byte)(i + 1)));
        var extras = Extras(ventana);
        for (var j = 0; j < extras.Count; j++)
            lista.Add((extras[j], extras[j].ToLowerInvariant(), (byte)(Crud.Length + 1 + j)));
        return lista;
    }

    private static List<PermisoDeseado> PermisosDeseados(IEnumerable<ModuloCatalogoDto> ventanas, IEnumerable<PermisoCatalogoDto> sueltos)
    {
        var lista = new List<PermisoDeseado>();
        foreach (var v in ventanas)
            foreach (var x in AccionesDe(v))
                lista.Add(new PermisoDeseado(CodigoPermisoVentana(v.Codigo, x.Accion), $"{v.Nombre}: {x.Verbo}", v.Codigo, x.Orden));
        lista.AddRange(sueltos.Select(p => new PermisoDeseado(p.Codigo, p.Nombre, p.CodigoModulo, 1)));
        return lista.OrderBy(p => p.Codigo, StringComparer.Ordinal).ToList();
    }

    /// <summary>Raíz <c>{app}</c> y sus descendientes (cadena de padre), más los módulos <c>{app}.*</c>, activos o no.</summary>
    private static List<Modulo> Universo(string app, IReadOnlyList<Modulo> modulos)
    {
        var hijos = modulos.Where(m => m.IdModuloPadre != null).ToLookup(m => m.IdModuloPadre!.Value);
        var vistos = new HashSet<int>();
        var cola = new Queue<Modulo>(modulos.Where(m => string.Equals(m.Codigo, app, StringComparison.OrdinalIgnoreCase)));
        while (cola.Count > 0)
        {
            var m = cola.Dequeue();
            if (!vistos.Add(m.Id)) continue;
            foreach (var h in hijos[m.Id]) cola.Enqueue(h);
        }
        foreach (var m in modulos.Where(m => m.Codigo.StartsWith(app + ".", StringComparison.OrdinalIgnoreCase))) vistos.Add(m.Id);
        return modulos.Where(m => vistos.Contains(m.Id)).GroupBy(m => m.Id).Select(g => g.First()).ToList();
    }

    /// <summary>SHA-256 en mayúsculas de las líneas del universo en orden ordinal: <c>M|…</c>, <c>P|…|activo</c> y <c>S|codigo|activo</c>.</summary>
    private static string Huella(IReadOnlyList<Modulo> universo, IReadOnlyList<Permiso> permisos, IEnumerable<AppModulo> asociacionesApp,
        IReadOnlyDictionary<int, Modulo> porId)
    {
        static string B(bool v) => v ? "1" : "0";
        string Cod(int? id) => id is int i && porId.TryGetValue(i, out var m) ? m.Codigo : string.Empty;
        var ids = universo.Select(m => m.Id).ToHashSet();
        var lineas = universo.Select(m => $"M|{m.Codigo}|{m.Nombre}|{m.Ruta}|{m.Icono}|{m.Orden}|{Cod(m.IdModuloPadre)}|{B(m.EsVisibleMenu)}|{B(m.Activo)}")
            .Concat(permisos.Where(p => ids.Contains(p.IdModulo)).Select(p => $"P|{p.Codigo}|{p.Nombre}|{Cod(p.IdModulo)}|{B(p.Activo)}"))
            .Concat(asociacionesApp.Select(a => $"S|{Cod(a.IdModulo)}|{B(a.Activo)}"))
            .OrderBy(l => l, StringComparer.Ordinal);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lineas)));
        return Convert.ToHexString(bytes);
    }

    private static List<string> Validar(string app, InyeccionCatalogoDto c)
    {
        var errores = new List<string>();
        void Err(string texto)
        {
            if (errores.Count < MaxErrores) errores.Add(texto);
        }

        var modulos = c.Modulos ?? [];
        if (modulos.Count == 0)
        {
            Err("El catálogo no trae módulos.");
            return errores;
        }

        var porCodigo = new Dictionary<string, ModuloCatalogoDto>(StringComparer.OrdinalIgnoreCase);
        var raices = 0;
        foreach (var m in modulos)
        {
            if (string.IsNullOrWhiteSpace(m.Codigo)) { Err("Hay un módulo sin código."); continue; }
            if (!porCodigo.TryAdd(m.Codigo, m)) { Err($"Módulo repetido: {m.Codigo}."); continue; }

            var esRaiz = string.Equals(m.Codigo, app, StringComparison.Ordinal);
            if (esRaiz)
            {
                raices++;
                if (m.CodigoPadre is not null) Err($"La raíz '{app}' no puede tener padre.");
                if ((m.Acciones?.Count ?? 0) > 0) Err($"La raíz '{app}' no admite acciones.");
            }
            else
            {
                if (!m.Codigo.StartsWith(app + ".", StringComparison.Ordinal)) Err($"El módulo '{m.Codigo}' debe llamarse '{app}' o empezar con '{app}.'.");
                if (string.IsNullOrWhiteSpace(m.CodigoPadre)) Err($"El módulo '{m.Codigo}' necesita padre.");
            }
            if (m.Codigo.Length > MaxCodigo) Err($"El código '{m.Codigo}' supera {MaxCodigo} caracteres.");
            if (string.IsNullOrWhiteSpace(m.Nombre)) Err($"El módulo '{m.Codigo}' no tiene nombre.");
            else if (m.Nombre.Length > MaxNombre) Err($"El nombre de '{m.Codigo}' supera {MaxNombre} caracteres.");
            if (m.Ruta is { Length: > MaxRuta }) Err($"La ruta de '{m.Codigo}' supera {MaxRuta} caracteres.");
            if (m.Icono is { Length: > MaxIcono }) Err($"El ícono de '{m.Codigo}' supera {MaxIcono} caracteres.");
            if (m.Orden is < 0 or > short.MaxValue) Err($"El orden de '{m.Codigo}' está fuera de 0–{short.MaxValue}.");

            var acciones = m.Acciones ?? [];
            if (acciones.Count > 0 && (esRaiz || !EsVentana(m))) Err($"Solo las ventanas (con ruta) admiten acciones: '{m.Codigo}'.");
            if (acciones.Count > MaxAcciones) Err($"'{m.Codigo}' trae más de {MaxAcciones} acciones.");
            foreach (var a in acciones.Where(a => a is null || !RegexAccion.IsMatch(a)))
                Err($"Acción inválida en '{m.Codigo}': '{a}' (debe ser MAYÚSCULAS y números, empezando con letra).");
        }
        if (raices != 1) Err($"Debe haber una sola raíz '{app}' (hay {raices}).");

        foreach (var m in porCodigo.Values.Where(m => !string.IsNullOrWhiteSpace(m.CodigoPadre)))
            if (!porCodigo.ContainsKey(m.CodigoPadre!)) Err($"El padre '{m.CodigoPadre}' de '{m.Codigo}' no está en el catálogo.");
        foreach (var m in porCodigo.Values)
        {
            var actual = m;
            var saltos = 0;
            while (actual.CodigoPadre is not null && porCodigo.TryGetValue(actual.CodigoPadre, out var siguiente) && saltos++ <= porCodigo.Count)
                actual = siguiente;
            if (saltos > porCodigo.Count) { Err($"Hay un ciclo de padres en '{m.Codigo}'."); break; }
        }

        // Permisos: los generados por ventana (largos y repetidos) y los sueltos.
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void RegistrarPermiso(string codigo, string nombre)
        {
            if (codigo.Length > MaxCodigo) Err($"El código de permiso '{codigo}' supera {MaxCodigo} caracteres.");
            if (nombre.Length > MaxNombre) Err($"El nombre del permiso '{codigo}' supera {MaxNombre} caracteres.");
            if (!vistos.Add(codigo)) Err($"Permiso repetido: {codigo}.");
        }
        foreach (var v in porCodigo.Values.Where(EsVentana).OrderBy(v => v.Codigo, StringComparer.Ordinal))
            foreach (var x in AccionesDe(v).Where(y => RegexAccion.IsMatch(y.Accion)))
                RegistrarPermiso(CodigoPermisoVentana(v.Codigo, x.Accion), $"{v.Nombre}: {x.Verbo}");
        foreach (var p in c.Permisos ?? [])
        {
            if (string.IsNullOrWhiteSpace(p.Codigo)) { Err("Hay un permiso sin código."); continue; }
            if (!p.Codigo.StartsWith(app + "_", StringComparison.Ordinal)) Err($"El permiso '{p.Codigo}' debe empezar con '{app}_'.");
            if (string.IsNullOrWhiteSpace(p.Nombre)) Err($"El permiso '{p.Codigo}' no tiene nombre.");
            if (!porCodigo.ContainsKey(p.CodigoModulo ?? string.Empty)) Err($"El módulo '{p.CodigoModulo}' del permiso '{p.Codigo}' no está en el catálogo.");
            RegistrarPermiso(p.Codigo, p.Nombre ?? string.Empty);
        }
        return errores;
    }

    private static Result<ResultadoInyeccionCatalogoDto> Falla(string codigo, string mensaje, int estado, ErrorType tipo) =>
        Result<ResultadoInyeccionCatalogoDto>.Failure(new Error(codigo, mensaje, null, estado, tipo));

    private static Result<ResultadoInyeccionCatalogoDto> Invalido(List<string> errores, string codigo = "CATALOGO_INVALIDO")
    {
        var detalles = new Dictionary<string, object>();
        for (var i = 0; i < errores.Count; i++) detalles[$"e{i + 1:D2}"] = errores[i];
        var mensaje = codigo == "CATALOGO_INVALIDO"
            ? $"El catálogo no es válido: {errores.Count} error(es)."
            : $"El catálogo choca con otra aplicación: {errores.Count} elemento(s).";
        return Result<ResultadoInyeccionCatalogoDto>.Failure(new Error(codigo, mensaje, detalles, 400, ErrorType.BadRequest));
    }
}
