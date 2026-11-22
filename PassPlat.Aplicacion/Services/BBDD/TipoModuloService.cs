using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface ITipoModuloService : IServiceAsync<TipoModulo, TipoModuloDto>
{
}

public class TipoModuloService : ServiceAsync<TipoModulo, TipoModuloDto>, ITipoModuloService
{
    public TipoModuloService(TipoModuloRepository repo, IMapper mapper)
        : base(repo, mapper) { }
}
