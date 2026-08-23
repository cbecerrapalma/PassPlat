using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Core;
using PassPlat.Dominio.Enums;

namespace PassPlat.Datos.Repositories;

public class MFARepository : RepositoryAsync<MFA>, IMFARepository
{
    private readonly IRawQueryRepositoryAsync _rawQuery;

    public MFARepository(PassPlatDbContext dbContext, IUnitOfWorkAsync uow)
        : base(dbContext)
    {
        _rawQuery = uow.RawQuery;
    }

    public async Task<Result<ValidarMFAResult>> ValidarMFAAsync(int idUsuario, int idTenant, int idTipoMFA, string idMFA, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdUsuario", idUsuario),
            RawParameter.Int("@IdTenant", idTenant),
            RawParameter.Int("@IdTipoMFA", idTipoMFA),
            RawParameter.NVarChar("@IdMFA", idMFA, 200)
        };

        return await SpHelper.ExecuteSPAsync<ValidarMFAResult>(_rawQuery, "SP_MFA_Validar", parameters, "Sin resultado del SP", ct);
    }

    public async Task<Result<IReadOnlyList<MFA>>> ObtenerMetodosPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var list = await Query()
                .Where(m => m.IdUsuario == idUsuario && m.IdEstado == (int)EEstadoMFA.Activo)
                .OrderByDescending(m => m.EsPrincipal)
                .ThenByDescending(m => m.UltUso)
                .ToListAsync(ct);
            return Result<IReadOnlyList<MFA>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<MFA>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<MFA?>> ObtenerMetodoPrincipalAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var entity = await Query()
                .FirstOrDefaultAsync(m => m.IdUsuario == idUsuario && m.EsPrincipal && m.IdEstado == (int)EEstadoMFA.Activo, ct);
            return Result<MFA?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<MFA?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result RevocarMetodo(int idUsuario, int idMFARegistro)
    {
        try
        {
            var metodo = DbSet.FirstOrDefault(m => m.IdUsuario == idUsuario && m.Id == idMFARegistro);
            if (metodo == null)
                return Result.Failure("MFA_NOT_FOUND", "Método MFA no encontrado");

            metodo.IdEstado = (int)EEstadoMFA.Revocado;
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }
}