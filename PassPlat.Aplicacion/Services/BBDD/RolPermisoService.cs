using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Results.Errors;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IRolPermisoService : IServiceAsync<RolPermiso, RolPermisoDto>
{
    Task<Result<IReadOnlyList<RolPermisoDto>>> ObtenerPermisosPorRolAsync(int idRol, CancellationToken ct = default);
    // S106 · D1 — alcanceTenant: null = is_system; con valor = solo roles de ese tenant (un rol global o de otro tenant → NOT_FOUND)
    // S107 · D2 — con alcance (no sistema), un rol que el ejecutor tiene asignado → AUTOEDICION
    Task<Result<RolPermisoDto>> AsignarPermisoAsync(AsignarPermisoDto dto, int? alcanceTenant, int idUsrEjecutor, CancellationToken ct = default);
    Task<Result> DesasignarPermisoAsync(int idRol, int idPermiso, int? alcanceTenant, int idUsrEjecutor, CancellationToken ct = default);
    Task<Result<ResultadoAplicarPermisosRolDto>> AplicarPermisosAsync(int idRol, IReadOnlyList<CambioPermisoDto> cambios, int? alcanceTenant, int idUsrEjecutor, CancellationToken ct = default);
}

public class RolPermisoService : ServiceAsync<RolPermiso, RolPermisoDto>, IRolPermisoService
{
    private const int MaxCambios = 500;

    private readonly IRolPermisoRepository _repo;
    private readonly IRolRepository _roles;
    private readonly IPermisoRepository _permisos;
    private readonly IAccesoRepository _accesos;

    public RolPermisoService(IRolPermisoRepository repo, IRolRepository roles, IPermisoRepository permisos, IAccesoRepository accesos, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _roles = roles; _permisos = permisos; _accesos = accesos; }

    private static Error Validacion(string codigo, string mensaje) => new(codigo, mensaje, null, 400, ErrorType.Validation);

    // el rol debe existir, estar activo y caer en el alcance; si no, el mismo NOT_FOUND (no revela roles de otro tenant)
    // S107 · D2 — y no debe ser un rol que el ejecutor tiene asignado (alcance null = sistema: sin esta regla)
    private async Task<Result> RolEnAlcanceAsync(int idRol, int? alcanceTenant, int idUsrEjecutor, CancellationToken ct)
    {
        var rol = await _roles.FirstOrDefaultAsync(r => r.Id == idRol && r.Activo, asNoTracking: true, ct);
        if (rol.IsFailure) return Result.Failure(rol.Error!);
        if (rol.Value is null || (alcanceTenant.HasValue && rol.Value.IdTenant != alcanceTenant.Value))
            return Result.Failure(CommonErrors.NotFound.Entity("Rol", idRol));
        if (alcanceTenant.HasValue)
        {
            var propio = await _accesos.AnyAsync(a => a.IdUsuario == idUsrEjecutor && a.IdRol == idRol && a.Activo, ct);
            if (propio.IsFailure) return Result.Failure(propio.Error!);
            if (propio.Value)
                return Result.Failure(Validacion("AUTOEDICION", "No puede cambiar los permisos de un rol que usted tiene asignado"));
        }
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<RolPermisoDto>>> ObtenerPermisosPorRolAsync(int idRol, CancellationToken ct = default)
    {
        var listResult = await _repo.ObtenerPermisosPorRolAsync(idRol, ct);
        if (listResult.IsFailure) return Result<IReadOnlyList<RolPermisoDto>>.Failure(listResult.Error!);
        return Result<IReadOnlyList<RolPermisoDto>>.Success(Mapper.Map<IReadOnlyList<RolPermisoDto>>(listResult.Value));
    }

    public async Task<Result<RolPermisoDto>> AsignarPermisoAsync(AsignarPermisoDto dto, int? alcanceTenant, int idUsrEjecutor, CancellationToken ct = default)
    {
        var alcance = await RolEnAlcanceAsync(dto.IdRol, alcanceTenant, idUsrEjecutor, ct);
        if (alcance.IsFailure) return Result<RolPermisoDto>.Failure(alcance.Error!);

        var existenteResult = await _repo.ObtenerActivoPorRolPermisoAsync(dto.IdRol, dto.IdPermiso, ct);
        if (existenteResult.IsFailure) return Result<RolPermisoDto>.Failure(existenteResult.Error!);
        var existente = existenteResult.Value;
        if (existente != null)
            return Result<RolPermisoDto>.Failure("YA_ASIGNADO", "El permiso ya está asignado a este rol");

        var entity = Mapper.Map<RolPermiso>(dto);
        entity.Activo = true;
        entity.FecCrea = DateTime.Now;
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<RolPermisoDto>.Failure(addResult.Error!);
        return Result<RolPermisoDto>.Success(Mapper.Map<RolPermisoDto>(entity));
    }

    public async Task<Result> DesasignarPermisoAsync(int idRol, int idPermiso, int? alcanceTenant, int idUsrEjecutor, CancellationToken ct = default)
    {
        var alcance = await RolEnAlcanceAsync(idRol, alcanceTenant, idUsrEjecutor, ct);
        if (alcance.IsFailure) return alcance;

        var existenteResult = await _repo.ObtenerActivoPorRolPermisoAsync(idRol, idPermiso, ct);
        if (existenteResult.IsFailure) return Result.Failure(existenteResult.Error!);
        var existente = existenteResult.Value;
        if (existente == null)
            return Result.Failure("NO_ASIGNADO", "El permiso no está asignado a este rol");

        existente.Desactivar();
        var updResult = Repository.Update(existente);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);
        return Result.Success();
    }

    // S106 · D1 — todo o nada: valida, calcula en memoria qué fila crear o desactivar y deja el commit al controlador
    public async Task<Result<ResultadoAplicarPermisosRolDto>> AplicarPermisosAsync(int idRol, IReadOnlyList<CambioPermisoDto> cambios, int? alcanceTenant, int idUsrEjecutor, CancellationToken ct = default)
    {
        if (cambios.Count is < 1 or > MaxCambios || cambios.Select(c => c.IdPermiso).Distinct().Count() != cambios.Count)
            return Result<ResultadoAplicarPermisosRolDto>.Failure(Validacion("CAMBIOS_INVALIDOS", $"Indique de 1 a {MaxCambios} permisos distintos"));

        var alcance = await RolEnAlcanceAsync(idRol, alcanceTenant, idUsrEjecutor, ct);
        if (alcance.IsFailure) return Result<ResultadoAplicarPermisosRolDto>.Failure(alcance.Error!);

        var activos = await _permisos.ObtenerActivosAsync(ct);
        if (activos.IsFailure) return Result<ResultadoAplicarPermisosRolDto>.Failure(activos.Error!);
        var idsActivos = activos.Value.Select(p => p.Id).ToHashSet();
        if (cambios.Any(c => !idsActivos.Contains(c.IdPermiso)))
            return Result<ResultadoAplicarPermisosRolDto>.Failure(Validacion("PERMISO_INVALIDO", "Hay permisos inexistentes o inactivos"));

        var ids = cambios.Select(c => c.IdPermiso).ToList();
        var filas = await _repo.WhereAsync(rp => rp.IdRol == idRol && rp.Activo && ids.Contains(rp.IdPermiso), asNoTracking: false, ct);
        if (filas.IsFailure) return Result<ResultadoAplicarPermisosRolDto>.Failure(filas.Error!);
        var activasPorPermiso = filas.Value.GroupBy(f => f.IdPermiso).ToDictionary(g => g.Key, g => g.First());

        var resultado = new ResultadoAplicarPermisosRolDto();
        foreach (var cambio in cambios)
        {
            activasPorPermiso.TryGetValue(cambio.IdPermiso, out var existente);
            if (cambio.Tiene)
            {
                if (existente is not null) { resultado.SinCambio++; continue; }
                var nueva = RolPermiso.Crear(idRol, cambio.IdPermiso);
                nueva.IdUsrMod = idUsrEjecutor;
                var add = Repository.Add(nueva);
                if (add.IsFailure) return Result<ResultadoAplicarPermisosRolDto>.Failure(add.Error!);
                resultado.Agregados++;
            }
            else
            {
                if (existente is null) { resultado.SinCambio++; continue; }
                existente.Desactivar();
                existente.IdUsrMod = idUsrEjecutor;
                var upd = Repository.Update(existente);
                if (upd.IsFailure) return Result<ResultadoAplicarPermisosRolDto>.Failure(upd.Error!);
                resultado.Quitados++;
            }
        }
        return Result<ResultadoAplicarPermisosRolDto>.Success(resultado);
    }
}
