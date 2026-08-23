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
    Task<Result<IReadOnlyList<ModuloDto>>> ObtenerRaicesAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<ModuloDto>>> ObtenerArbolCompletoAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<ModuloDto>>> ObtenerVisiblesMenuAsync(int idUsuario, int idApp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ModuloDto>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default);
    Task<Result<ModuloDto>> CrearAsync(CrearModuloDto dto, CancellationToken ct = default);
    Task<Result<ModuloDto>> ActualizarAsync(int id, ActualizarModuloDto dto, CancellationToken ct = default);
}

public class ModuloService : ServiceAsync<Modulo, ModuloDto>, IModuloService
{
    private readonly IModuloRepository _repo;
    private readonly IUnitOfWorkAsync _uow;
    private readonly AuthRepository _authRepo;

    public ModuloService(IModuloRepository repo, IUnitOfWorkAsync uow, AuthRepository authRepo, IMapper mapper)
        : base(repo, mapper)
    {
        _repo = repo;
        _uow = uow;
        _authRepo = authRepo;
    }

    public async Task<Result<IReadOnlyList<ModuloDto>>> ObtenerRaicesAsync(CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerRaicesAsync(ct);
        if (repoResult.IsFailure) return Result<IReadOnlyList<ModuloDto>>.Failure(repoResult.Error!);
        var arbol = ConstruirArbol(repoResult.Value, null);
        return Result<IReadOnlyList<ModuloDto>>.Success(arbol);
    }

    public async Task<Result<IReadOnlyList<ModuloDto>>> ObtenerArbolCompletoAsync(CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerArbolCompletoAsync(ct);
        if (repoResult.IsFailure) return Result<IReadOnlyList<ModuloDto>>.Failure(repoResult.Error!);
        var raices = repoResult.Value.Where(m => m.IdModuloPadre == null).OrderBy(m => m.Orden).ToList();
        var arbol = ConstruirArbol(repoResult.Value, null);
        return Result<IReadOnlyList<ModuloDto>>.Success(arbol);
    }

    public async Task<Result<IReadOnlyList<ModuloDto>>> ObtenerVisiblesMenuAsync(int idUsuario, int idApp, CancellationToken ct = default)
    {
        try
        {
            var esSistemaResult = await _authRepo.ObtenerUsuarioBasicoAsync(idUsuario, ct);
            if (esSistemaResult.IsFailure) return Result<IReadOnlyList<ModuloDto>>.Failure(esSistemaResult.Error!);
            var esSistema = esSistemaResult.Value?.EsSistema == true;

            var repoResult = await _repo.ObtenerVisiblesMenuAsync(ct);
            if (repoResult.IsFailure) return Result<IReadOnlyList<ModuloDto>>.Failure(repoResult.Error!);

            var modulos = repoResult.Value.AsEnumerable();

            if (!esSistema)
                modulos = modulos.Where(m => m.IdTipoModulo != 1);

            var list = modulos.OrderBy(m => m.Orden).ToList();
            var arbol = ConstruirArbol(list, null);
            return Result<IReadOnlyList<ModuloDto>>.Success(arbol);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ModuloDto>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<ModuloDto>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerPorAppAsync(idApp, ct);
        if (repoResult.IsFailure) return Result<IReadOnlyList<ModuloDto>>.Failure(repoResult.Error!);
        var arbol = ConstruirArbol(repoResult.Value, null);
        return Result<IReadOnlyList<ModuloDto>>.Success(arbol);
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
}
