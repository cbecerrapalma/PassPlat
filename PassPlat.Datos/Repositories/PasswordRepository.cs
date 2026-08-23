using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public class PasswordRepository : RepositoryAsync<HistorialPwd>, IPasswordRepository
{
    private readonly IRawQueryRepositoryAsync _rawQuery;

    public PasswordRepository(PassPlatDbContext dbContext, IUnitOfWorkAsync uow)
        : base(dbContext)
    {
        _rawQuery = uow.RawQuery;
    }

    public async Task<Result<CambiarPwdResult>> CambiarPasswordAsync(int idUsuario, int idTenant, string hashPwdNuevo, byte pepperVersion, int idTipoCambio, int? idDisp = null, int? idIP = null, int? idAgente = null, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdUsuario", idUsuario),
            RawParameter.Int("@IdTenant", idTenant),
            RawParameter.NVarChar("@HashPwdNuevo", hashPwdNuevo, 512),
            RawParameter.In("@PepperVersion", pepperVersion, System.Data.DbType.Byte),
            RawParameter.Int("@IdTipoCambio", idTipoCambio),
            RawParameter.Int("@IdDisp", idDisp),
            RawParameter.Int("@IdIP", idIP),
            RawParameter.Int("@IdAgente", idAgente)
        };

        return await SpHelper.ExecuteSPAsync<CambiarPwdResult>(_rawQuery, "SP_Pwd_Cambiar", parameters, "Sin resultado del SP", ct);
    }
}