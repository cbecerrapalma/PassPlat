using AutoMapper;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Contexto;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Contexto;

namespace PassPlat.Aplicacion.Services;

public interface IUserAgentService : IServiceAsync<UserAgent, UserAgentDto>
{
    Task<Result<UserAgentDto?>> ObtenerPorHashAsync(string hashAgente, CancellationToken ct = default);
    Task<Result<UserAgentDto>> ObtenerOCrearAsync(string agente, string hashAgente, string? navegador = null, string? version = null, string? sistemaOperativo = null, bool? esMovil = null, CancellationToken ct = default);
}

public class UserAgentService : ServiceAsync<UserAgent, UserAgentDto>, IUserAgentService
{
    private readonly UserAgentRepository _repo;

    public UserAgentService(UserAgentRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<UserAgentDto?>> ObtenerPorHashAsync(string hashAgente, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorHashAsync(hashAgente, ct);
        if (entityResult.IsFailure)
            return Result<UserAgentDto?>.Failure(entityResult.Error!);
        var entity = entityResult.Value;
        return Result<UserAgentDto?>.Success(Mapper.Map<UserAgentDto?>(entity), allowNull: true);
    }

    public async Task<Result<UserAgentDto>> ObtenerOCrearAsync(string agente, string hashAgente, string? navegador = null, string? version = null, string? sistemaOperativo = null, bool? esMovil = null, CancellationToken ct = default)
    {
        var repoResult = _repo.ObtenerOCrear(agente, hashAgente, navegador, version, sistemaOperativo, esMovil);
        if (repoResult.IsFailure) return Result<UserAgentDto>.Failure(repoResult.Error!);
        return Result<UserAgentDto>.Success(Mapper.Map<UserAgentDto>(repoResult.Value));
    }

}
