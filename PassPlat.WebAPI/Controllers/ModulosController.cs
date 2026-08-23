using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "PERMISOS_VER")]
public class ModulosController : BaseApiController
{
    private readonly IModuloService _service;
    private readonly ITenantContext _tenantContext;
    private readonly AuthRepository _authRepo;
    private readonly IUnitOfWorkAsync _uow;

    public ModulosController(IModuloService service, ITenantContext tenantContext, AuthRepository authRepo, IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _authRepo = authRepo;
        _uow = uow;
    }

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.ObtenerArbolCompletoAsync(ct));
    [HttpGet("raices")] public async Task<IActionResult> GetRaices(CancellationToken ct) => FromResultQuery(await _service.ObtenerRaicesAsync(ct));
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _service.GetByIdAsync(id));

    [HttpGet("menu")]
    public async Task<IActionResult> GetMenu([FromServices] IHttpContextAccessor httpContextAccessor, CancellationToken ct)
    {
        var idUsuarioClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(idUsuarioClaim) || !int.TryParse(idUsuarioClaim, out var idUsuario))
            return Unauthorized();

        var idAppClaim = httpContextAccessor.HttpContext?.Request.Headers["X-App-Id"].FirstOrDefault();
        var idApp = int.TryParse(idAppClaim, out var appId) ? appId : 1;

        var result = await _service.ObtenerVisiblesMenuAsync(idUsuario, idApp, ct);
        return FromResultQuery(result);
    }

    [HttpGet("app/{idApp}")]
    public async Task<IActionResult> GetPorApp(int idApp, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorAppAsync(idApp, ct));

    [HttpPost]
    [Authorize(Policy = "PERMISOS_CREAR")]
    public async Task<IActionResult> Create([FromBody] CrearModuloDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "PERMISOS_EDITAR")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarModuloDto dto, CancellationToken ct)
    {
        var result = await _service.ActualizarAsync(id, dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
