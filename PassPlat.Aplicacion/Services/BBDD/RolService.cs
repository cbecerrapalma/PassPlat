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

public interface IRolService : IServiceAsync<Rol, RolDto>
{
    Task<Result<IReadOnlyList<RolDto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<RolDto>>> ObtenerParaTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RolLookupDto>>> ObtenerLookupPorTenantAsync(int idTenant, CancellationToken ct = default);
    // S111 · D2 — roles acotados a las apps indicadas (y al tenant si hay alcance)
    Task<Result<IReadOnlyList<RolDto>>> ObtenerPorAppsAsync(int? idTenant, IReadOnlyCollection<int> idsApp, bool soloActivos, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RolLookupDto>>> ObtenerLookupPorAppsAsync(int idTenant, IReadOnlyCollection<int> idsApp, CancellationToken ct = default);
    Task<Result<RolDto>> CrearAsync(CrearRolDto dto, int? idUsrEjecutor = null, CancellationToken ct = default);
    // S116 · D1 — alcanceTenant: null = usuario de sistema (todo); n = solo los roles del tenant n
    Task<Result<RolDto>> ActualizarAsync(int id, string nombre, string? descripcion, bool activo, int? alcanceTenant, CancellationToken ct = default);
    Task<Result> DesactivarAsync(int id, int? alcanceTenant, CancellationToken ct = default);
}

public class RolService : ServiceAsync<Rol, RolDto>, IRolService
{
    private readonly RolRepository _repo;
    private readonly IUnitOfWorkAsync _uow;

    public RolService(RolRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _uow = uow; }

    public async Task<Result<IReadOnlyList<RolDto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        var result = await _repo.ObtenerTodosAsync(ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolDto>>.Success(Mapper.Map<IReadOnlyList<RolDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<RolDto>>> ObtenerParaTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerParaTenantAsync(idTenant, ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolDto>>.Success(Mapper.Map<IReadOnlyList<RolDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<RolLookupDto>>> ObtenerLookupPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerParaTenantAsync(idTenant, ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolLookupDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolLookupDto>>.Success(Mapper.Map<IReadOnlyList<RolLookupDto>>(result.Value));
    }

    // S111 · D2
    public async Task<Result<IReadOnlyList<RolDto>>> ObtenerPorAppsAsync(int? idTenant, IReadOnlyCollection<int> idsApp, bool soloActivos, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorAppsAsync(idTenant, idsApp, soloActivos, ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolDto>>.Success(Mapper.Map<IReadOnlyList<RolDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<RolLookupDto>>> ObtenerLookupPorAppsAsync(int idTenant, IReadOnlyCollection<int> idsApp, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorAppsAsync(idTenant, idsApp, true, ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolLookupDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolLookupDto>>.Success(Mapper.Map<IReadOnlyList<RolLookupDto>>(result.Value));
    }

    public async Task<Result<RolDto>> CrearAsync(CrearRolDto dto, int? idUsrEjecutor = null, CancellationToken ct = default)
    {
        var idsPermisos = dto.IdsPermisos is { Count: > 0 }
            ? string.Join(",", dto.IdsPermisos)
            : null;

        var spResult = await _repo.CrearConSPAsync(
            dto.IdTenant, dto.Codigo, dto.Nombre, dto.Descripcion,
            dto.IdPolitica, idsPermisos, ct);

        if (spResult.IsFailure)
            return Result<RolDto>.Failure(spResult.Error!);

        if (spResult.Value.Resultado != 0)
            return Result<RolDto>.Failure(ErrorDeSpCrear(spResult.Value.Resultado, spResult.Value.Mensaje));

        var entityResult = await Repository.GetByIdAsync(spResult.Value.Id!.Value, ct);
        if (entityResult.IsFailure)
            return Result<RolDto>.Failure(entityResult.Error!);

        return Result<RolDto>.Success(Mapper.Map<RolDto>(entityResult.Value));
    }

    // S116 · D2 — SP_Rol_Crear: 2 = código duplicado (409); 1 tenant y 3 política no válidos (400);
    // cualquier otro, técnico (500) sin el texto del SQL.
    private static Error ErrorDeSpCrear(int resultado, string? mensaje) => resultado switch
    {
        2 => new Error("CODIGO_DUPLICADO", mensaje ?? "El código de rol ya existe en este tenant", null, 409, ErrorType.Conflict),
        1 or 3 => new Error("DATOS_INVALIDOS", mensaje ?? "Datos del rol no válidos", null, 400, ErrorType.Validation),
        _ => new Error("DB_ERROR", "Error al crear el rol", null, 500, ErrorType.Database)
    };

    // S116 · D1 — SP_Rol_Actualizar y SP_Rol_Desactivar nunca existieron en la BD: edición y baja por EF.
    // Rol de otro tenant → 404 (como si no existiera); rol global con alcance de tenant → 403.
    private async Task<Result<Rol>> ObtenerParaEscribirAsync(int id, int? alcanceTenant, CancellationToken ct)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return r;
        if (alcanceTenant is int propio && r.Value.IdTenant != propio)
            return Result<Rol>.Failure(r.Value.IdTenant is null
                ? CommonErrors.Authorization.Forbidden("un rol global")
                : CommonErrors.NotFound.Entity("Rol", id));
        return r;
    }

    public async Task<Result> DesactivarAsync(int id, int? alcanceTenant, CancellationToken ct = default)
    {
        var r = await ObtenerParaEscribirAsync(id, alcanceTenant, ct);
        if (r.IsFailure) return Result.Failure(r.Error!);

        r.Value.Desactivar();
        var upd = Repository.Update(r.Value);
        if (upd.IsFailure) return Result.Failure(upd.Error!);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<RolDto>> ActualizarAsync(int id, string nombre, string? descripcion, bool activo, int? alcanceTenant, CancellationToken ct = default)
    {
        var r = await ObtenerParaEscribirAsync(id, alcanceTenant, ct);
        if (r.IsFailure) return Result<RolDto>.Failure(r.Error!);

        var rol = r.Value;
        rol.Nombre = nombre;
        rol.Descripcion = descripcion;
        rol.Activo = activo;
        var upd = Repository.Update(rol);
        if (upd.IsFailure) return Result<RolDto>.Failure(upd.Error!);

        await _uow.SaveChangesAsync(ct);
        return Result<RolDto>.Success(Mapper.Map<RolDto>(rol));
    }
}
