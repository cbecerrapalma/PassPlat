using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IRolPoliticaPwdRepository : IRepositoryAsync<RolPoliticaPwd>
{
    Task<Result<IReadOnlyList<RolPoliticaPwd>>> ObtenerPorRolAsync(int idRol, CancellationToken ct = default);
}

public class RolPoliticaPwdRepository : RepositoryAsync<RolPoliticaPwd>, IRolPoliticaPwdRepository
{
    public RolPoliticaPwdRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<RolPoliticaPwd>>> ObtenerPorRolAsync(int idRol, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(r => r.Rol)
                .Include(r => r.Politica)
                .Where(r => r.IdRol == idRol && r.Activo)
                .ToListAsync(ct);
            return Result<IReadOnlyList<RolPoliticaPwd>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<RolPoliticaPwd>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
