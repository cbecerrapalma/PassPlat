using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public interface IGrupoUsuarioRepository : IRepositoryAsync<GrupoUsuario>
{
    Task<Result<IReadOnlyList<GrupoUsuario>>> ObtenerPorGrupoAsync(int idGrupo, CancellationToken ct = default);
    Task<Result<IReadOnlyList<GrupoUsuario>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
}

public class GrupoUsuarioRepository : RepositoryAsync<GrupoUsuario>, IGrupoUsuarioRepository
{
    public GrupoUsuarioRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<GrupoUsuario>>> ObtenerPorGrupoAsync(int idGrupo, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(gu => gu.IdGrupo == idGrupo)
                .Include(gu => gu.Usuario)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<GrupoUsuario>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<GrupoUsuario>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<GrupoUsuario>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(gu => gu.IdUsuario == idUsuario)
                .Include(gu => gu.Grupo)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<GrupoUsuario>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<GrupoUsuario>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
