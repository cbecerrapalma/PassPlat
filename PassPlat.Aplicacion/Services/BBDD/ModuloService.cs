using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface IModuloService : IServiceAsync<Modulo, ModuloDto>
{
    Task<Result<IReadOnlyList<ModuloDto>>> ObtenerArbolCompletoAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<ModuloDto>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default);
    // S110 · D4 — árbol y raíces de los módulos activos de esas apps (unión de ObtenerPorAppAsync por app)
    Task<Result<IReadOnlyList<ModuloDto>>> ObtenerArbolPorAppsAsync(IReadOnlyCollection<int> idsApp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ModuloMenuDto>>> ObtenerMenuAsync(int idApp, IReadOnlySet<string> permisos, bool esSistema, CancellationToken ct = default);
    Task<Result<ModuloDto>> CrearAsync(CrearModuloDto dto, CancellationToken ct = default);
    Task<Result<ModuloDto>> ActualizarAsync(int id, ActualizarModuloDto dto, CancellationToken ct = default);
}

public class ModuloService : ServiceAsync<Modulo, ModuloDto>, IModuloService
{
    private readonly IModuloRepository _repo;
    private readonly IUnitOfWorkAsync _uow;

    public ModuloService(IModuloRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Result<IReadOnlyList<ModuloDto>>> ObtenerArbolCompletoAsync(CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerArbolCompletoAsync(ct);
        if (repoResult.IsFailure) return Result<IReadOnlyList<ModuloDto>>.Failure(repoResult.Error!);
        var raices = repoResult.Value.Where(m => m.IdModuloPadre == null).OrderBy(m => m.Orden).ToList();
        var arbol = ConstruirArbol(repoResult.Value, null);
        return Result<IReadOnlyList<ModuloDto>>.Success(arbol);
    }

    public async Task<Result<IReadOnlyList<ModuloDto>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerPorAppAsync(idApp, ct);
        if (repoResult.IsFailure) return Result<IReadOnlyList<ModuloDto>>.Failure(repoResult.Error!);
        var arbol = ConstruirArbol(repoResult.Value, null);
        return Result<IReadOnlyList<ModuloDto>>.Success(arbol);
    }

    // S110 · D4 — árbol de los módulos activos de esas apps; un módulo cuyo padre no está en el conjunto queda como raíz
    public async Task<Result<IReadOnlyList<ModuloDto>>> ObtenerArbolPorAppsAsync(IReadOnlyCollection<int> idsApp, CancellationToken ct = default)
    {
        var modulos = await ObtenerModulosDeAppsAsync(idsApp, ct);
        if (modulos.IsFailure) return Result<IReadOnlyList<ModuloDto>>.Failure(modulos.Error!);
        return Result<IReadOnlyList<ModuloDto>>.Success(ConstruirArbol(modulos.Value));
    }

    // Unión por Id de ObtenerPorAppAsync de cada app (módulo activo y AppsModulos activa); un fallo del repositorio se propaga
    private async Task<Result<IReadOnlyList<Modulo>>> ObtenerModulosDeAppsAsync(IReadOnlyCollection<int> idsApp, CancellationToken ct)
    {
        var porId = new Dictionary<int, Modulo>();
        foreach (var idApp in idsApp.Distinct())
        {
            var deApp = await _repo.ObtenerPorAppAsync(idApp, ct);
            if (deApp.IsFailure) return Result<IReadOnlyList<Modulo>>.Failure(deApp.Error!);
            foreach (var m in deApp.Value) porId.TryAdd(m.Id, m);
        }
        return Result<IReadOnlyList<Modulo>>.Success([.. porId.Values]);
    }

    /// <summary>
    /// S83 · D3 — menú de la app: módulos activos de <paramref name="idApp"/> que el usuario puede ver
    /// según sus permisos <c>_VER</c>. El tipo de módulo no decide la visibilidad.
    /// </summary>
    public async Task<Result<IReadOnlyList<ModuloMenuDto>>> ObtenerMenuAsync(int idApp, IReadOnlySet<string> permisos, bool esSistema, CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerMenuPorAppAsync(idApp, ct);
        if (repoResult.IsFailure) return Result<IReadOnlyList<ModuloMenuDto>>.Failure(repoResult.Error!);
        return Result<IReadOnlyList<ModuloMenuDto>>.Success(ConstruirMenu(repoResult.Value, permisos, esSistema));
    }

    private static List<ModuloMenuDto> ConstruirMenu(IReadOnlyList<Modulo> modulos, IReadOnlySet<string> permisos, bool esSistema)
    {
        var porId = modulos.ToDictionary(m => m.Id);
        var hijos = modulos.ToLookup(m => m.IdModuloPadre is int p && porId.ContainsKey(p) ? p : (int?)null);
        var permitidos = new Dictionary<int, bool>();

        bool Permitido(Modulo m)
        {
            if (esSistema) return true;
            if (permitidos.TryGetValue(m.Id, out var cache)) return cache;
            var ver = m.Permisos.Where(p => p.Activo && p.Codigo.EndsWith("_VER", StringComparison.Ordinal)).Select(p => p.Codigo).ToList();
            var resultado = ver.Count > 0
                ? ver.Any(permisos.Contains)
                : m.IdModuloPadre is not int idPadre || !porId.TryGetValue(idPadre, out var padre) || Permitido(padre);
            permitidos[m.Id] = resultado;
            return resultado;
        }

        List<ModuloMenuDto> Nivel(int? idPadre) => hijos[idPadre]
            .OrderBy(m => m.Orden).ThenBy(m => m.Nombre)
            .Where(Permitido)
            .Select(m => new ModuloMenuDto
            {
                Codigo = m.Codigo,
                Nombre = m.Nombre,
                Descripcion = m.Descripcion,
                Ruta = m.Ruta,
                Icono = m.Icono,
                Orden = m.Orden,
                EsVisibleMenu = m.EsVisibleMenu,
                Hijos = Nivel(m.Id)
            })
            .Where(n => !string.IsNullOrWhiteSpace(n.Ruta) || n.Hijos.Count > 0)
            .ToList();

        return Nivel(null);
    }

    public async Task<Result<ModuloDto>> CrearAsync(CrearModuloDto dto, CancellationToken ct = default)
    {
        var entity = Mapper.Map<Modulo>(dto);
        entity.FecCrea = DateTime.Now;
        var result = _repo.Add(entity);
        if (result.IsFailure) return Result<ModuloDto>.Failure(result.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<ModuloDto>.Success(Mapper.Map<ModuloDto>(entity));
    }

    public async Task<Result<ModuloDto>> ActualizarAsync(int id, ActualizarModuloDto dto, CancellationToken ct = default)
    {
        var entityResult = await _repo.GetByIdAsync(id, ct);
        if (entityResult.IsFailure) return Result<ModuloDto>.Failure(entityResult.Error!);
        var entity = entityResult.Value;
        Mapper.Map(dto, entity);
        entity.FecMod = DateTime.Now;
        var updateResult = _repo.Update(entity);
        if (updateResult.IsFailure) return Result<ModuloDto>.Failure(updateResult.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<ModuloDto>.Success(Mapper.Map<ModuloDto>(entity));
    }

    private List<ModuloDto> ConstruirArbol(IEnumerable<Modulo> todos, int? idPadre)
    {
        return todos.Where(m => m.IdModuloPadre == idPadre)
            .OrderBy(m => m.Orden).ThenBy(m => m.Nombre)
            .Select(m =>
            {
                var dto = Mapper.Map<ModuloDto>(m);
                dto.SubModulos = ConstruirArbol(todos, m.Id);
                return dto;
            }).ToList();
    }

    // S110 · D4 — sobrecarga para un subconjunto: raíz = IdModuloPadre null o padre fuera del conjunto (arregla H4)
    private List<ModuloDto> ConstruirArbol(IReadOnlyList<Modulo> conjunto)
    {
        var ids = conjunto.Select(m => m.Id).ToHashSet();
        return conjunto.Where(m => m.IdModuloPadre is not int idPadre || !ids.Contains(idPadre))
            .OrderBy(m => m.Orden).ThenBy(m => m.Nombre)
            .Select(m =>
            {
                var dto = Mapper.Map<ModuloDto>(m);
                dto.SubModulos = ConstruirArbol(conjunto, m.Id);
                return dto;
            }).ToList();
    }
}
