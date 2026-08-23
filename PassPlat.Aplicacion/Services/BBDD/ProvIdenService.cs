using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services.OAuth;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Models;

namespace PassPlat.Aplicacion.Services;

public interface IProvIdenService : IServiceAsync<ProvIden, ProvIdenDto>
{
    Task<Result<IReadOnlyList<ProvIdenDto>>> ObtenerActivosAsync(CancellationToken ct = default);
    Task<Result<ProvIdenDto?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<Result<ProviderConfigurationInfoDto?>> ObtenerInfoConfiguracionAsync(int id, CancellationToken ct = default);
    Task<Result<ProvIdenDto>> CrearAsync(CrearProvIdenDto dto, CancellationToken ct = default);
    Task<Result<ProvIdenDto>> ActualizarAsync(int id, ActualizarProvIdenDto dto, CancellationToken ct = default);
    Task<Result> DesactivarAsync(int id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProvIdenCatalogDto>>> ObtenerCatalogAsync(int idTenant, CancellationToken ct = default);
}

public class ProvIdenService : ServiceAsync<ProvIden, ProvIdenDto>, IProvIdenService
{
    private readonly ProvIdenRepository _repo;
    private readonly IConfProvIdenRepository _confRepo;
    private readonly IUnitOfWorkAsync _uow;

    public ProvIdenService(ProvIdenRepository repo, IConfProvIdenRepository confRepo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper)
    {
        _repo = repo;
        _confRepo = confRepo;
        _uow = uow;
    }

    public async Task<Result<IReadOnlyList<ProvIdenDto>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        var listResult = await _repo.ObtenerActivosAsync(ct);
        if (listResult.IsFailure) return Result<IReadOnlyList<ProvIdenDto>>.Failure(listResult.Error!);
        return Result<IReadOnlyList<ProvIdenDto>>.Success(Mapper.Map<IReadOnlyList<ProvIdenDto>>(listResult.Value));
    }

    public async Task<Result<ProvIdenDto?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorCodigoAsync(codigo, ct);
        if (entityResult.IsFailure) return Result<ProvIdenDto?>.Failure(entityResult.Error!);
        var dto = entityResult.Value != null ? Mapper.Map<ProvIdenDto>(entityResult.Value) : null;
        return Result<ProvIdenDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<ProviderConfigurationInfoDto?>> ObtenerInfoConfiguracionAsync(int id, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result<ProviderConfigurationInfoDto?>.Failure(r.Error!);
        if (r.Value is null) return Result<ProviderConfigurationInfoDto?>.Success(null, allowNull: true);
        var dto = Mapper.Map<ProviderConfigurationInfoDto>(r.Value);
        return Result<ProviderConfigurationInfoDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<IReadOnlyList<ProvIdenCatalogDto>>> ObtenerCatalogAsync(int idTenant, CancellationToken ct = default)
    {
        var proveedoresResult = await _repo.ObtenerTodosOrdenadosAsync(ct);
        if (proveedoresResult.IsFailure)
            return Result<IReadOnlyList<ProvIdenCatalogDto>>.Failure(proveedoresResult.Error!);

        var configsResult = await _confRepo.ObtenerPorTenantAsync(idTenant, ct);
        if (configsResult.IsFailure)
            return Result<IReadOnlyList<ProvIdenCatalogDto>>.Failure(configsResult.Error!);

        var configsPorProvIden = configsResult.Value.ToDictionary(c => c.IdProvIden);

        var catalog = proveedoresResult.Value.Select(prov =>
        {
            configsPorProvIden.TryGetValue(prov.Id, out var config);
            var status = OAuthConfigurationStatusCalculator.Calculate(prov, config);
            return new ProvIdenCatalogDto
            {
                Proveedor = Mapper.Map<ProvIdenDto>(prov),
                Status = status
            };
        }).ToList().AsReadOnly();

        return Result<IReadOnlyList<ProvIdenCatalogDto>>.Success(catalog);
    }

    public async Task<Result<ProvIdenDto>> CrearAsync(CrearProvIdenDto dto, CancellationToken ct = default)
    {
        var existe = await _repo.ObtenerPorCodigoAsync(dto.Codigo, ct);
        if (existe.IsSuccess && existe.Value != null)
            return Result<ProvIdenDto>.Failure("CODIGO_DUPLICADO", $"Ya existe un proveedor con el código '{dto.Codigo}'");

        OAuthProviderMetadata? metadata = null;
        if (!string.IsNullOrWhiteSpace(dto.Metadata))
        {
            try { metadata = System.Text.Json.JsonSerializer.Deserialize<OAuthProviderMetadata>(dto.Metadata); }
            catch { metadata = null; }
        }

        var entity = ProvIden.Crear(dto.Codigo, dto.Nombre, dto.TipoProveedor, dto.Protocolo, dto.Version,
            dto.UrlIssuer, dto.EndpointAutorizacion, dto.EndpointToken, dto.EndpointUserInfo, dto.JwksUri, dto.EndpointRevocacion,
            dto.SoportaPKCE, dto.SoportaRefreshToken, dto.SoportaMFA, dto.Icono, dto.Orden, metadata);

        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<ProvIdenDto>.Failure(addResult.Error!);

        await _uow.SaveChangesAsync(ct);
        return Result<ProvIdenDto>.Success(Mapper.Map<ProvIdenDto>(entity));
    }

    public async Task<Result<ProvIdenDto>> ActualizarAsync(int id, ActualizarProvIdenDto dto, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result<ProvIdenDto>.Failure(r.Error!);

        Mapper.Map(dto, r.Value);
        var updResult = Repository.Update(r.Value);
        if (updResult.IsFailure) return Result<ProvIdenDto>.Failure(updResult.Error!);

        await _uow.SaveChangesAsync(ct);
        return Result<ProvIdenDto>.Success(Mapper.Map<ProvIdenDto>(r.Value));
    }

    public async Task<Result> DesactivarAsync(int id, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result.Failure(r.Error!);

        r.Value.Activo = false;
        var updResult = Repository.Update(r.Value);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
