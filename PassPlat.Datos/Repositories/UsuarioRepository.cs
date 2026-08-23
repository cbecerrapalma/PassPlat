using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IUsuarioRepository : IRepositoryAsync<Usuario>
{
    Task<Result<Usuario?>> ObtenerPorEmailAsync(int tenantId, string email, CancellationToken ct = default);
    Task<Result<Usuario?>> ObtenerCompletoPorIdAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<Usuario?>> ObtenerPorNomUsuarioAsync(int idTenant, string nomUsuario, CancellationToken ct = default);
    Task<Result<Usuario?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Usuario>>> ObtenerConIntentosExcedidosAsync(int idTenant, byte maxIntentos, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Usuario>>> ObtenerConPasswordExpiradaAsync(int idTenant, int diasVigencia, CancellationToken ct = default);
    Task<Result<CrearUsuarioResult>> CrearConPasswordAsync(int idTenant, int idEstado, string nomUsuario, string? email, string nombre, string apellido, string? hashPwd, string algoritmo, byte pepperVersion, int? idTipoCambio, bool emailVerificado, int? idUsrEjecutor, int? idTipoAccion = null, int? idDisp = null, int? idIP = null, int? idAgente = null, CancellationToken ct = default);
    Task<Result> IncrementarIntentosFallidosAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<Usuario> Items, int TotalCount)>> BuscarPaginadoAsync(string search, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<Usuario> Items, int TotalCount)>> BuscarPaginadoPorTenantAsync(int idTenant, string search, int pageNumber, int pageSize, CancellationToken ct = default);
}

public class UsuarioRepository : RepositoryAsync<Usuario>, IUsuarioRepository
{
    private readonly IRawQueryRepositoryAsync _rawQuery;

    public UsuarioRepository(PassPlatDbContext dbContext, IUnitOfWorkAsync uow) : base(dbContext)
    {
        _rawQuery = uow.RawQuery;
    }

    public async Task<Result<Usuario?>> ObtenerPorEmailAsync(int tenantId, string email, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(u => u.IdTenant == tenantId && u.Email == email && !u.Eliminado, ct);
            return Result<Usuario?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Usuario?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Usuario?>> ObtenerCompletoPorIdAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.Include(u => u.Tenant).Include(u => u.Estado)
                .FirstOrDefaultAsync(u => u.Id == idUsuario && !u.Eliminado, ct);
            return Result<Usuario?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Usuario?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Usuario?>> ObtenerPorNomUsuarioAsync(int idTenant, string nomUsuario, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(u => u.IdTenant == idTenant && u.NomUsuario == nomUsuario && !u.Eliminado, ct);
            return Result<Usuario?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Usuario?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Usuario?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(u => u.Id == id && !u.Eliminado, ct);
            return Result<Usuario?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Usuario?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Usuario>>> ObtenerConIntentosExcedidosAsync(int idTenant, byte maxIntentos, CancellationToken ct = default)
    {
        try
        {
            var list = await Query().Where(u => u.IdTenant == idTenant && u.IntentosFallidos >= maxIntentos && !u.Eliminado).ToListAsync(ct);
            return Result<IReadOnlyList<Usuario>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Usuario>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Usuario>>> ObtenerConPasswordExpiradaAsync(int idTenant, int diasVigencia, CancellationToken ct = default)
    {
        try
        {
            var fechaLimite = DateTime.Now.AddDays(-diasVigencia);
            var list = await Query().Where(u => u.IdTenant == idTenant && !u.Eliminado && u.FecUltCambioPwd.HasValue && u.FecUltCambioPwd.Value <= fechaLimite).ToListAsync(ct);
            return Result<IReadOnlyList<Usuario>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Usuario>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<CrearUsuarioResult>> CrearConPasswordAsync(int idTenant, int idEstado, string nomUsuario, string? email, string nombre, string apellido, string? hashPwd, string algoritmo, byte pepperVersion, int? idTipoCambio, bool emailVerificado, int? idUsrEjecutor, int? idTipoAccion = null, int? idDisp = null, int? idIP = null, int? idAgente = null, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdTenant", idTenant),
            RawParameter.Int("@IdEstado", idEstado),
            RawParameter.NVarChar("@NomUsuario", nomUsuario, 100),
            RawParameter.NVarChar("@Email", email, 255),
            RawParameter.NVarChar("@Nombre", nombre, 100),
            RawParameter.NVarChar("@Apellido", apellido, 100),
            RawParameter.NVarChar("@HashPwd", hashPwd, 512),
            RawParameter.NVarChar("@Algoritmo", algoritmo, 50),
            RawParameter.In("@PepperVersion", pepperVersion, System.Data.DbType.Byte),
            RawParameter.Int("@IdTipoCambio", idTipoCambio),
            RawParameter.Bit("@EmailVerificado", emailVerificado),
            RawParameter.Int("@IdUsrEjecutor", idUsrEjecutor),
            RawParameter.Int("@IdTipoAccion", idTipoAccion),
            RawParameter.Int("@IdDisp", idDisp),
            RawParameter.Int("@IdIP", idIP),
            RawParameter.Int("@IdAgente", idAgente)
        };

        return await SpHelper.ExecuteSPAsync<CrearUsuarioResult>(_rawQuery, "SP_Usuario_Crear", parameters, "Sin resultado del SP", ct);
    }

    public async Task<Result> IncrementarIntentosFallidosAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var rows = await Context.Database.ExecuteSqlRawAsync(
                "UPDATE Usuarios SET IntentosFallidos = IntentosFallidos + 1, FecUltIntentoFallido = SYSUTCDATETIME() WHERE Id = {0}",
                idUsuario, ct);
            return rows > 0 ? Result.Success() : Result.Failure("NOT_FOUND", "Usuario no encontrado");
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<(IReadOnlyList<Usuario> Items, int TotalCount)>> BuscarPaginadoAsync(string search, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        try
        {
            var query = DbSet.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    u.NomUsuario.Contains(search) ||
                    u.Nombre.Contains(search) ||
                    u.Apellido.Contains(search) ||
                    (u.Email ?? "").Contains(search));

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(u => u.NomUsuario)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            return Result<(IReadOnlyList<Usuario>, int)>.Success((items, total));
        }
        catch (Exception ex)
        {
            return Result<(IReadOnlyList<Usuario>, int)>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<(IReadOnlyList<Usuario> Items, int TotalCount)>> BuscarPaginadoPorTenantAsync(int idTenant, string search, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        try
        {
            var query = DbSet.AsNoTracking().Where(u => u.IdTenant == idTenant);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    u.NomUsuario.Contains(search) ||
                    u.Nombre.Contains(search) ||
                    u.Apellido.Contains(search) ||
                    (u.Email ?? "").Contains(search));

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(u => u.NomUsuario)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            return Result<(IReadOnlyList<Usuario>, int)>.Success((items, total));
        }
        catch (Exception ex)
        {
            return Result<(IReadOnlyList<Usuario>, int)>.Failure("DB_ERROR", ex.Message);
        }
    }
}
