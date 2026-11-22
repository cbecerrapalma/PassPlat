using CBP.Results;
using CBP.Results.Errors;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;

namespace PassPlat.Aplicacion.Services;

/// <summary>
/// S113 · D3 (P41) — «no puedes dar lo que no tienes»: un ejecutor que no es sistema solo concede
/// (a un rol, a un usuario o con un Acceso) permisos que él mismo tiene en su tenant, sumando todas sus apps.
/// Los claims del token traen solo los permisos de la app del login (PASSPLAT): por eso se leen de la BD.
/// Los controladores no la llaman para is_system.
/// </summary>
public interface IConcesionPermisosService
{
    Task<Result> ValidarPermisosAsync(IReadOnlyCollection<int> idsPermiso, int idUsrEjecutor, int idTenant, CancellationToken ct = default);
    Task<Result> ValidarRolAsync(int idRol, int idUsrEjecutor, int idTenant, CancellationToken ct = default);
}

public sealed class ConcesionPermisosService(
    IAccesoRepository accesos, IAuthRepository auth, IPermisoRepository permisos, IRolPermisoRepository rolesPermisos)
    : IConcesionPermisosService
{
    private static readonly Error NoConcedible =
        new("PERMISO_NO_CONCEDIBLE", "No puede conceder un permiso que usted no tiene", null, 403, ErrorType.Forbidden);

    public async Task<Result> ValidarPermisosAsync(IReadOnlyCollection<int> idsPermiso, int idUsrEjecutor, int idTenant, CancellationToken ct = default)
    {
        if (idsPermiso.Count == 0) return Result.Success();
        var ids = idsPermiso.ToList();
        var filas = await permisos.WhereAsync(p => ids.Contains(p.Id), asNoTracking: true, ct);
        if (filas.IsFailure) return Result.Failure(filas.Error!);
        return await ValidarCodigosAsync([.. filas.Value.Select(p => p.Codigo)], idUsrEjecutor, idTenant, ct);
    }

    public async Task<Result> ValidarRolAsync(int idRol, int idUsrEjecutor, int idTenant, CancellationToken ct = default)
    {
        var filas = await rolesPermisos.ObtenerPermisosPorRolAsync(idRol, ct);
        if (filas.IsFailure) return Result.Failure(filas.Error!);
        return await ValidarCodigosAsync([.. filas.Value.Where(rp => rp.Permiso is not null).Select(rp => rp.Permiso!.Codigo)], idUsrEjecutor, idTenant, ct);
    }

    private async Task<Result> ValidarCodigosAsync(IReadOnlyList<string> codigos, int idUsrEjecutor, int idTenant, CancellationToken ct)
    {
        if (codigos.Count == 0) return Result.Success();
        var propios = await accesos.WhereAsync(a => a.IdUsuario == idUsrEjecutor && a.IdTenant == idTenant && a.Activo, asNoTracking: true, ct);
        if (propios.IsFailure) return Result.Failure(propios.Error!);
        var tiene = new HashSet<string>(StringComparer.Ordinal);
        foreach (var idApp in propios.Value.Select(a => a.IdApp).Distinct())
        {
            var codigosApp = await auth.ObtenerCodigosPermisosPorUsuarioAsync(idUsrEjecutor, idTenant, idApp, ct);
            if (codigosApp.IsFailure) return Result.Failure(codigosApp.Error!);
            tiene.UnionWith(codigosApp.Value);
        }
        return codigos.All(tiene.Contains) ? Result.Success() : Result.Failure(NoConcedible);
    }
}
