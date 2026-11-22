using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Results.Errors;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

// S102 · D5 — alcanceTenant: null = is_system; con valor = solo relaciones de herencia de ese tenant.
public interface IRolesHerenciaService : IServiceAsync<RolesHerencia, RolesHerenciaDto>
{
    Task<Result<IReadOnlyList<RolesHerenciaDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RolesHerenciaDto>>> ObtenerHijosAsync(int idRolPadre, int? alcanceTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RolesHerenciaDto>>> ObtenerPadresAsync(int idRolHijo, int? alcanceTenant, CancellationToken ct = default);
    Task<Result<RolesHerenciaDto>> CrearAsync(CrearRolesHerenciaDto dto, CancellationToken ct = default);
    Task<Result> EliminarAsync(int id, int? alcanceTenant, CancellationToken ct = default);
}

public class RolesHerenciaService : ServiceAsync<RolesHerencia, RolesHerenciaDto>, IRolesHerenciaService
{
    private readonly IRolesHerenciaRepository _repo;
    private readonly IRolRepository _roles;
    private readonly IUnitOfWorkAsync _uow;

    public RolesHerenciaService(IRolesHerenciaRepository repo, IRolRepository roles, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _roles = roles; _uow = uow; }

    private static Error Validacion(string codigo, string mensaje) => new(codigo, mensaje, null, 400, ErrorType.Validation);

    public async Task<Result<IReadOnlyList<RolesHerenciaDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolesHerenciaDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolesHerenciaDto>>.Success(Mapper.Map<IReadOnlyList<RolesHerenciaDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<RolesHerenciaDto>>> ObtenerHijosAsync(int idRolPadre, int? alcanceTenant, CancellationToken ct = default) =>
        EnAlcance(await _repo.ObtenerHijosAsync(idRolPadre, ct), alcanceTenant);

    public async Task<Result<IReadOnlyList<RolesHerenciaDto>>> ObtenerPadresAsync(int idRolHijo, int? alcanceTenant, CancellationToken ct = default) =>
        EnAlcance(await _repo.ObtenerPadresAsync(idRolHijo, ct), alcanceTenant);

    private Result<IReadOnlyList<RolesHerenciaDto>> EnAlcance(Result<IReadOnlyList<RolesHerencia>> result, int? alcanceTenant)
    {
        if (result.IsFailure) return Result<IReadOnlyList<RolesHerenciaDto>>.Failure(result.Error!);
        IReadOnlyList<RolesHerencia> lista = result.Value;
        if (alcanceTenant.HasValue)
            lista = [.. lista.Where(rh => rh.IdTenant == alcanceTenant.Value)];
        return Result<IReadOnlyList<RolesHerenciaDto>>.Success(Mapper.Map<IReadOnlyList<RolesHerenciaDto>>(lista));
    }

    public async Task<Result<RolesHerenciaDto>> CrearAsync(CrearRolesHerenciaDto dto, CancellationToken ct = default)
    {
        var existente = await _repo.ObtenerRelacionAsync(dto.IdRolHijo, dto.IdRolPadre, ct);
        if (existente.IsFailure) return Result<RolesHerenciaDto>.Failure(existente.Error!);
        if (existente.Value != null)
            return Result<RolesHerenciaDto>.Failure("YA_EXISTE", "La relación de herencia ya existe");

        // S108 · D3 — lo que TR_RolesHerencia_ValidarTenant rechazaba con un 500 se valida antes de escribir
        var valida = await ValidarAsync(dto, ct);
        if (valida.IsFailure) return Result<RolesHerenciaDto>.Failure(valida.Error!);

        var entity = Mapper.Map<RolesHerencia>(dto);
        entity.Activo = true;
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<RolesHerenciaDto>.Failure(addResult.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<RolesHerenciaDto>.Success(Mapper.Map<RolesHerenciaDto>(entity));
    }

    // S108 · D3 — mismas reglas que el trigger: roles existentes, del tenant indicado o globales, y sin ciclos
    private async Task<Result> ValidarAsync(CrearRolesHerenciaDto dto, CancellationToken ct)
    {
        if (dto.IdRolHijo == dto.IdRolPadre)
            return Result.Failure(Validacion("HERENCIA_CICLO", "La herencia de roles no puede contener ciclos"));

        var roles = await _roles.WhereAsync(r => r.Id == dto.IdRolHijo || r.Id == dto.IdRolPadre, asNoTracking: true, ct);
        if (roles.IsFailure) return Result.Failure(roles.Error!);
        if (roles.Value.Count != 2)
            return Result.Failure(Validacion("ROL_NO_ENCONTRADO", "El rol hijo o el rol padre no existe"));
        if (roles.Value.Any(r => r.IdTenant is not null && r.IdTenant != dto.IdTenant))
            return Result.Failure(Validacion("ROL_OTRO_TENANT", "Los roles de una herencia deben pertenecer al tenant indicado o ser roles globales"));

        // ciclo: el hijo ya es ancestro del padre (se suben las herencias activas desde el padre)
        var visitados = new HashSet<int> { dto.IdRolPadre };
        var pendientes = new Queue<int>();
        pendientes.Enqueue(dto.IdRolPadre);
        while (pendientes.Count > 0)
        {
            var padres = await _repo.ObtenerPadresAsync(pendientes.Dequeue(), ct);
            if (padres.IsFailure) return Result.Failure(padres.Error!);
            foreach (var idPadre in padres.Value.Select(h => h.IdRolPadre))
            {
                if (idPadre == dto.IdRolHijo)
                    return Result.Failure(Validacion("HERENCIA_CICLO", "La herencia de roles no puede contener ciclos"));
                if (visitados.Add(idPadre)) pendientes.Enqueue(idPadre);
            }
        }
        return Result.Success();
    }

    public async Task<Result> EliminarAsync(int id, int? alcanceTenant, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure) return Result.Failure(result.Error!);
        if (alcanceTenant.HasValue && result.Value.IdTenant != alcanceTenant.Value)
            return Result.Failure(CommonErrors.NotFound.Entity("RolesHerencia", id));

        var removeResult = Repository.Remove(result.Value);
        if (removeResult.IsFailure) return Result.Failure(removeResult.Error!);
        return Result.Success();
    }
}
