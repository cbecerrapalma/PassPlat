using CBP.Data.Asynchronous;
using PassPlat.Datos.Interfaces;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Repositories;

public class TipoDispRepository : RepositoryAsync<TipoDisp>, ITipoDispRepository
{
    public TipoDispRepository(PassPlatDbContext dbContext) : base(dbContext)
    {
    }
}