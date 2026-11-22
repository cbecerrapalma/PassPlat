using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using PassPlat.WebAPI.Auth;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;

namespace PassPlat.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = "GRUPOS_VER")]
public class GruposController : BaseApiController
{
    private readonly IGrupoService _service;
    private readonly IGrupoUsuarioService _grupoUsuarioService;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;

    public GruposController(IGrupoService service, IGrupoUsuarioService grupoUsuarioService, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _grupoUsuarioService = grupoUsuarioService;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    // S91 · D1 — un no-sistema solo ve y modifica los grupos de su tenant (como UsuariosController.GetPaged); is_system ve todos.
    private int? AlcanceTenant => User.AlcanceTenant(_tenantContext);

    // S92 · D1 — los errores salen por FromResult (ProblemDetails): NotFound → 404 fuera del alcance (S91).
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => FromResult(await _service.ObtenerTodosAsync(AlcanceTenant, ct));

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var gruposResult = await _service.ObtenerTodosAsync(AlcanceTenant, ct);
        if (gruposResult.IsFailure) return FromResult(gruposResult);

        var stats = new Dictionary<int, object>();
        foreach (var g in gruposResult.Value!)
        {
            // Los grupos ya vienen acotados al tenant: no se repite el control por grupo.
            var miembrosResult = await _grupoUsuarioService.ObtenerPorGrupoAsync(g.Id, null, ct);
            var count = miembrosResult.IsSuccess ? miembrosResult.Value!.Count : 0;
            var usuarios = miembrosResult.IsSuccess ? miembrosResult.Value!.Select(m => m.IdUsuario).Distinct().Count() : 0;
            stats[g.Id] = new { miembros = count, usuarios };
        }
        return Ok(stats);
    }

    [HttpGet("tenant/{idTenant}")]
    public async Task<IActionResult> GetByTenant(int idTenant, CancellationToken ct)
        => FromResult(await _service.ObtenerPorTenantAsync(idTenant, AlcanceTenant, ct));

    [HttpPost]
    [Authorize(Policy = "GRUPOS_CREAR")]
    public async Task<IActionResult> Create([FromBody] CrearGrupoDto dto, CancellationToken ct)
    {
        dto.IdTenant = _tenantContext.CurrentId;

        // S92 · D2 — CrearAsync es el dueño del commit (lo necesita para devolver el Id generado).
        var result = await _service.CrearAsync(dto, ct);
        return CreatedFromResult(nameof(GetByTenant), new { idTenant = dto.IdTenant }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "GRUPOS_EDITAR")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarGrupoDto dto, CancellationToken ct)
    {
        var result = await _service.ActualizarAsync(id, dto, AlcanceTenant, ct);
        if (result.IsFailure) return FromResult(result);

        await _uow.SaveChangesAsync(ct);

        return FromResult(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "GRUPOS_ELIMINAR")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _service.EliminarAsync(id, AlcanceTenant, ct);
        if (result.IsFailure) return FromResult(result);

        await _uow.SaveChangesAsync(ct);

        return FromResult(result);
    }
}
