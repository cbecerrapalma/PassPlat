using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public class AuthRepository : RepositoryAsync<Usuario>, IAuthRepository
{
    private readonly IRawQueryRepositoryAsync _rawQuery;

    public AuthRepository(PassPlatDbContext dbContext, IUnitOfWorkAsync uow)
        : base(dbContext)
    {
        _rawQuery = uow.RawQuery;
    }

    public async Task<Result<LoginResult>> LoginAsync(string? nomUsuario, string? email, int idApp, string hashPwdCalculado, int idTenant, int? idDisp = null, int? idIP = null, int? idAgente = null, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdTenant", idTenant),
            RawParameter.Int("@IdApp", idApp),
            RawParameter.NVarChar("@HashPwdCalculado", hashPwdCalculado, 512),
            RawParameter.NVarChar("@NomUsuario", nomUsuario, 100),
            RawParameter.NVarChar("@Email", email, 255),
            RawParameter.Int("@IdDisp", idDisp),
            RawParameter.Int("@IdIP", idIP),
            RawParameter.Int("@IdAgente", idAgente)
        };

        return await SpHelper.ExecuteSPAsync<LoginResult>(_rawQuery, "SP_Auth_Login", parameters, "Sin resultado del SP", ct);
    }

    public async Task<Result<Usuario?>> ObtenerUsuarioBasicoAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet
                .Where(u => u.Id == idUsuario && !u.Eliminado)
                .Select(u => new Usuario
                {
                    Id = u.Id,
                    IdTenant = u.IdTenant,
                    IdEstado = u.IdEstado,
                    Eliminado = u.Eliminado,
                    NomUsuario = u.NomUsuario,
                    Email = u.Email,
                    Nombre = u.Nombre,
                    Apellido = u.Apellido,
                    EsSistema = u.EsSistema
                })
                .FirstOrDefaultAsync(ct);
            return Result<Usuario?>.Success(entity, allowNull: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Usuario?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Usuario?>> ObtenerUsuarioPorNomAsync(string? nomUsuario, string? email, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet
                .Where(u => !u.Eliminado)
                .Where(u => (nomUsuario != null && u.NomUsuario == nomUsuario) || (email != null && u.Email == email))
                .Select(u => new Usuario
                {
                    Id = u.Id,
                    IdTenant = u.IdTenant,
                    IdEstado = u.IdEstado,
                    NomUsuario = u.NomUsuario,
                    Email = u.Email,
                    Nombre = u.Nombre,
                    Apellido = u.Apellido,
                    EsSistema = u.EsSistema
                })
                .FirstOrDefaultAsync(ct);
            return Result<Usuario?>.Success(entity, allowNull: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Usuario?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<string?>> ObtenerHashActualAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var entity = await Context.Set<HistorialPwd>()
                .Where(h => h.IdUsuario == idUsuario && h.EsActual)
                .Select(h => h.HashPwd)
                .FirstOrDefaultAsync(ct);
            return Result<string?>.Success(entity, allowNull: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<string?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<bool>> ObtenerPwdExpiradaAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdUsuario", idUsuario),
            RawParameter.Int("@IdTenant", idTenant)
        };

        var spResult = await _rawQuery.QuerySPAsync<PwdEstadoVencimientoResult>(
            "SP_Pwd_EstadoVencimiento", parameters, ct);

        if (spResult.IsFailure)
            return Result<bool>.Failure(spResult.Error!);

        var fila = spResult.Value.FirstOrDefault();
        if (fila == null)
            return Result<bool>.Failure("DB_ERROR", "SP_Pwd_EstadoVencimiento no devolvio fila");

        return Result<bool>.Success(fila.PwdExpirada);
    }

    public async Task<Result<IReadOnlyList<string>>> ObtenerCodigosPermisosPorUsuarioAsync(int idUsuario, int idTenant, int idApp, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdUsuario", idUsuario),
            RawParameter.Int("@IdTenant", idTenant),
            RawParameter.Int("@IdApp", idApp)
        };

        var spResult = await _rawQuery.QuerySPAsync<PermisosUsuarioEfectivosResult>(
            "SP_Permisos_Usuario_Efectivos", parameters, ct);

        if (spResult.IsFailure)
            return Result<IReadOnlyList<string>>.Failure(spResult.Error!);

        var codigos = spResult.Value.Select(p => p.Codigo).Distinct().ToList();
        return Result<IReadOnlyList<string>>.Success(codigos);
    }

    public async Task<Result<IReadOnlyList<string>>> ObtenerCodigosPermisosPlatformAsync(int idUsuario, int idApp, CancellationToken ct = default)
    {
        try
        {
            // S61 D3: el scope de plataforma solo aporta permisos de roles globales.
            var codigos = await Context.Set<Acceso>()
                .Where(a => a.IdUsuario == idUsuario && a.IdApp == idApp && a.Activo && a.IdUsuarioTenant == null
                            && a.Rol!.IdTenant == null)
                .SelectMany(a => a.Rol!.RolesPermisos)
                .Where(rp => rp.Activo)
                .Select(rp => rp.Permiso!.Codigo)
                .Distinct()
                .ToListAsync(ct);

            return Result<IReadOnlyList<string>>.Success(codigos);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<string>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<string>>> ObtenerCodigosPermisosPorUsuarioTenantAsync(int idUsuarioTenant, int idApp, CancellationToken ct = default)
    {
        try
        {
            var usuarioTenant = await Context.Set<UsuarioTenant>()
                .Where(ut => ut.Id == idUsuarioTenant && ut.Activo)
                .Select(ut => new { ut.IdUsuario, ut.IdTenant })
                .FirstOrDefaultAsync(ct);

            if (usuarioTenant == null)
                return Result<IReadOnlyList<string>>.Success(new List<string>());

            var parameters = new[]
            {
                RawParameter.Int("@IdUsuario", usuarioTenant.IdUsuario),
                RawParameter.Int("@IdTenant", usuarioTenant.IdTenant),
                RawParameter.Int("@IdApp", idApp)
            };

            var spResult = await _rawQuery.QuerySPAsync<PermisosUsuarioEfectivosResult>(
                "SP_Permisos_Usuario_Efectivos", parameters, ct);

            if (spResult.IsFailure)
                return Result<IReadOnlyList<string>>.Failure(spResult.Error!);

            var codigos = spResult.Value.Select(p => p.Codigo).Distinct().ToList();
            return Result<IReadOnlyList<string>>.Success(codigos);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<string>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<bool>> ExisteAccesoPlatformActivoAsync(int idUsuario, int idApp, CancellationToken ct = default)
    {
        try
        {
            // S61 D1/D3: acceso activo sin membresía + rol global + membresía activa en el tenant de plataforma.
            var exists = await Context.Set<Acceso>()
                .AnyAsync(a => a.IdUsuario == idUsuario && a.IdApp == idApp && a.Activo
                               && a.IdUsuarioTenant == null
                               && a.Rol!.IdTenant == null
                               && Context.Set<UsuarioTenant>().Any(ut =>
                                   ut.IdUsuario == idUsuario && ut.Activo
                                   && Context.Set<Tenant>().Any(t => t.Id == ut.IdTenant && t.EsSistema)),
                    ct);
            return Result<bool>.Success(exists);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<bool>.Failure("DB_ERROR", ex.Message);
        }
    }

    // Tope de niveles de herencia de SP_Permisos_Usuario_Efectivos
    private const int MaxNivelesHerencia = 32;

    public async Task<Result<IReadOnlyList<PermisoDeRol>>> ObtenerPermisosDeRolesAsync(int idUsuario, int idTenant, int idApp, CancellationToken ct = default)
    {
        try
        {
            // roles base: Accesos activos en (tenant, app) con rol activo del tenant o global
            var baseRoles = await Context.Set<Acceso>()
                .Where(a => a.IdUsuario == idUsuario && a.IdTenant == idTenant && a.IdApp == idApp && a.Activo
                            && a.Rol!.Activo && (a.Rol.IdTenant == null || a.Rol.IdTenant == idTenant))
                .Select(a => new { a.IdRol, a.Rol!.Nombre })
                .Distinct()
                .ToListAsync(ct);

            var roles = baseRoles.ToDictionary(r => r.IdRol, r => (Nombre: r.Nombre, Heredado: false));
            var frontera = roles.Keys.ToList();

            // herencia por niveles: mismo tenant, relación activa y padre activo; los visitados cortan los ciclos
            for (var nivel = 0; nivel < MaxNivelesHerencia && frontera.Count > 0; nivel++)
            {
                var padres = await Context.Set<RolesHerencia>()
                    .Where(h => h.Activo && h.IdTenant == idTenant && frontera.Contains(h.IdRolHijo) && h.RolPadre!.Activo)
                    .Select(h => new { h.IdRolPadre, h.RolPadre!.Nombre })
                    .Distinct()
                    .ToListAsync(ct);

                frontera = [];
                foreach (var padre in padres.Where(p => !roles.ContainsKey(p.IdRolPadre)))
                {
                    roles[padre.IdRolPadre] = (padre.Nombre, true);
                    frontera.Add(padre.IdRolPadre);
                }
            }

            var idsRoles = roles.Keys.ToList();
            var asignados = await Context.Set<RolPermiso>()
                .Where(rp => rp.Activo && rp.Permiso!.Activo && idsRoles.Contains(rp.IdRol))
                .Select(rp => new { rp.IdPermiso, rp.IdRol })
                .ToListAsync(ct);

            IReadOnlyList<PermisoDeRol> lista =
            [
                .. asignados.Select(a => new PermisoDeRol(a.IdPermiso, a.IdRol, roles[a.IdRol].Nombre, roles[a.IdRol].Heredado))
            ];
            return Result<IReadOnlyList<PermisoDeRol>>.Success(lista);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<PermisoDeRol>>.Failure("DB_ERROR", ex.Message);
        }
    }
}