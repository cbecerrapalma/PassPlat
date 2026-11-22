using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IResultadoAccesoRepository : IRepositoryAsync<ResultadoAcceso>
{
}

public class ResultadoAccesoRepository : RepositoryAsync<ResultadoAcceso>, IResultadoAccesoRepository
{
    public ResultadoAccesoRepository(PassPlatDbContext dbContext) : base(dbContext)
    {
    }
}
