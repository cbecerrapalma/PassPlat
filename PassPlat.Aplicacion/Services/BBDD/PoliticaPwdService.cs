using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IPoliticaPwdService : IServiceAsync<PoliticaPwd, PoliticaPwdDto>
{
    Task<Result<PoliticaPwdDto?>> ObtenerPoliticaAplicableAsync(int idTenant, int? idApp, CancellationToken ct = default);
    Task<Result<PoliticaPwdDto?>> ObtenerPoliticaGlobalAsync(CancellationToken ct = default);
    Task<Result<PoliticaPwdDto?>> ObtenerPoliticaParaRolAsync(int idTenant, int idRol, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PoliticaPwdDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result> DesactivarPoliticaAsync(int idPolitica, CancellationToken ct = default);
    Task<Result<PoliticaPwdDto>> CrearAsync(CrearPoliticaPwdDto dto, CancellationToken ct = default);
    Task<Result> ActualizarAsync(int id, ActualizarPoliticaPwdDto dto, CancellationToken ct = default);
}

public class PoliticaPwdService : ServiceAsync<PoliticaPwd, PoliticaPwdDto>, IPoliticaPwdService
{
    private readonly PoliticaPwdRepository _repo;
    private readonly IUnitOfWorkAsync _uow;

    public PoliticaPwdService(PoliticaPwdRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _uow = uow; }

    public async Task<Result<PoliticaPwdDto?>> ObtenerPoliticaAplicableAsync(int idTenant, int? idApp, CancellationToken ct = default)
    {
        var politicaResult = await _repo.ObtenerPoliticaAplicableAsync(idTenant, idApp, ct);
        if (politicaResult.IsFailure)
            return Result<PoliticaPwdDto?>.Failure(politicaResult.Error!);
        var politica = politicaResult.Value;
        var dto = politica != null ? Mapper.Map<PoliticaPwdDto>(politica) : null;
        return Result<PoliticaPwdDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<PoliticaPwdDto?>> ObtenerPoliticaGlobalAsync(CancellationToken ct = default)
    {
        var politicaResult = await _repo.ObtenerPoliticaGlobalAsync(ct);
        if (politicaResult.IsFailure)
            return Result<PoliticaPwdDto?>.Failure(politicaResult.Error!);
        var politica = politicaResult.Value;
        var dto = politica != null ? Mapper.Map<PoliticaPwdDto>(politica) : null;
        return Result<PoliticaPwdDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<PoliticaPwdDto?>> ObtenerPoliticaParaRolAsync(int idTenant, int idRol, CancellationToken ct = default)
    {
        var politicaResult = await _repo.ObtenerPoliticaParaRolAsync(idTenant, idRol, ct);
        if (politicaResult.IsFailure)
            return Result<PoliticaPwdDto?>.Failure(politicaResult.Error!);
        var politica = politicaResult.Value;
        var dto = politica != null ? Mapper.Map<PoliticaPwdDto>(politica) : null;
        return Result<PoliticaPwdDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<IReadOnlyList<PoliticaPwdDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (repoResult.IsFailure) return Result<IReadOnlyList<PoliticaPwdDto>>.Failure(repoResult.Error!);
        return Result<IReadOnlyList<PoliticaPwdDto>>.Success(Mapper.Map<IReadOnlyList<PoliticaPwdDto>>(repoResult.Value));
    }

    public async Task<Result> DesactivarPoliticaAsync(int idPolitica, CancellationToken ct = default)
    {
        var repoResult = _repo.DesactivarPolitica(idPolitica);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        await _repo.InvalidarCacheAsync(ct);
        return Result.Success();
    }

    public async Task<Result<PoliticaPwdDto>> CrearAsync(CrearPoliticaPwdDto dto, CancellationToken ct = default)
    {
        var entity = Mapper.Map<PoliticaPwd>(dto);
        entity.Version = 1;
        entity.Activa = true;
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<PoliticaPwdDto>.Failure(addResult.Error!);
        await _uow.SaveChangesAsync(ct);
        await _repo.InvalidarCacheAsync(ct);
        return Result<PoliticaPwdDto>.Success(Mapper.Map<PoliticaPwdDto>(entity));
    }

    public async Task<Result> ActualizarAsync(int id, ActualizarPoliticaPwdDto dto, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure)
            return Result.Failure("NO_ENCONTRADO", "Política no encontrada");

        var entity = r.Value;
        if (dto.Nombre != null) entity.Nombre = dto.Nombre;
        if (dto.LongMin.HasValue) entity.LongMin = dto.LongMin.Value;
        if (dto.LongMax.HasValue) entity.LongMax = dto.LongMax.Value;
        if (dto.ReqMayuscula.HasValue) entity.ReqMayuscula = dto.ReqMayuscula.Value;
        if (dto.ReqMinuscula.HasValue) entity.ReqMinuscula = dto.ReqMinuscula.Value;
        if (dto.ReqNumero.HasValue) entity.ReqNumero = dto.ReqNumero.Value;
        if (dto.ReqEspecial.HasValue) entity.ReqEspecial = dto.ReqEspecial.Value;
        if (dto.CaracteresEspeciales != null) entity.CaracteresEspeciales = dto.CaracteresEspeciales;
        if (dto.ProhSecuenciales.HasValue) entity.ProhSecuenciales = dto.ProhSecuenciales.Value;
        if (dto.ProhRepetitivos.HasValue) entity.ProhRepetitivos = dto.ProhRepetitivos.Value;
        if (dto.ProhPatrones.HasValue) entity.ProhPatrones = dto.ProhPatrones.Value;
        if (dto.ProhPwdComun.HasValue) entity.ProhPwdComun = dto.ProhPwdComun.Value;
        if (dto.ProhInfoUsuario.HasValue) entity.ProhInfoUsuario = dto.ProhInfoUsuario.Value;
        if (dto.VerificarBrechas.HasValue) entity.VerificarBrechas = dto.VerificarBrechas.Value;
        if (dto.PermitirEspacios.HasValue) entity.PermitirEspacios = dto.PermitirEspacios.Value;
        if (dto.DiasVigencia.HasValue) entity.DiasVigencia = dto.DiasVigencia.Value;
        if (dto.PwdRecordadas.HasValue) entity.PwdRecordadas = dto.PwdRecordadas.Value;
        if (dto.MaxIntentos.HasValue) entity.MaxIntentos = dto.MaxIntentos.Value;
        if (dto.DurBloqueoMin.HasValue) entity.DurBloqueoMin = dto.DurBloqueoMin.Value;
        entity.FecMod = DateTime.Now;

        var updResult = Repository.Update(entity);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);
        await _repo.InvalidarCacheAsync(ct);
        return Result.Success();
    }
}
