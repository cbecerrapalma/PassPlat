using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using CBP.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "HISTORIAL_PWD_VER")]
public class HistorialPwdController : BaseApiController
{
    private readonly IHistorialPwdService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;
    public HistorialPwdController(IHistorialPwdService service, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (User.HasClaim("is_system", "true"))
            return FromResultQuery(await _service.GetAllAsync());
        return Forbid();
    }
    [HttpGet("{id}")] public async Task<IActionResult> GetById(long id, CancellationToken ct) => FromResultQuery(await _service.GetByIdAsync(id));

    [HttpGet("page")]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request, CancellationToken ct)
    {
        var isSystem = User.HasClaim("is_system", "true");
        if (isSystem)
        {
            var options = new PaginationOptions<HistorialPwd>(request.PageNumber, request.PageSize);
            var result = await _service.GetPagedAsync(options, ct);
            return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
        }
        var tenantResult = await _service.ObtenerPaginadoPorTenantAsync(_tenantContext.CurrentId, request.PageNumber, request.PageSize, ct);
        if (tenantResult.IsFailure) return FromResult(tenantResult);
        var p = request;
        var totalPages = (int)Math.Ceiling(tenantResult.Value.TotalCount / (double)Math.Max(p.PageSize, 1));
        return Ok(new
        {
            items = tenantResult.Value.Items.ToList(), totalCount = tenantResult.Value.TotalCount,
            pageNumber = p.PageNumber, pageSize = p.PageSize, totalPages,
            hasPreviousPage = p.PageNumber > 1, hasNextPage = p.PageNumber < totalPages
        });
    }
    [HttpGet("reciente/{idUsuario}/{cantidad}")] public async Task<IActionResult> GetReciente(int idUsuario, int cantidad, CancellationToken ct) => FromResultQuery(await _service.ObtenerHistorialRecienteAsync(idUsuario, cantidad, ct));
    [HttpPost("marcar-comprometidas")] public async Task<IActionResult> MarcarComprometidas([FromBody] MarcarComprometidasRequest request, CancellationToken ct) => FromResult(await _service.MarcarComprometidasPorHashAsync(request.HashPwd, ct));
    [HttpGet("comprometidas")] public async Task<IActionResult> GetComprometidas(CancellationToken ct) => FromResultQuery(await _service.ObtenerPasswordsComprometidasAsync(ct));
}

public class MarcarComprometidasRequest
{
    public string HashPwd { get; init; } = string.Empty;
}
