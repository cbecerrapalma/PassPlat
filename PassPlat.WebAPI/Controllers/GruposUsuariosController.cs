using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using PassPlat.WebAPI.Auth;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = "GRUPOS_VER")]
public class GruposUsuariosController : BaseApiController
{
    private readonly IGrupoUsuarioService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;

    public GruposUsuariosController(IGrupoUsuarioService service, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    // S91 · D1 — mismo alcance que GruposController: un no-sistema solo los grupos de su tenant.
    private int? AlcanceTenant => User.AlcanceTenant(_tenantContext);

    // S92 · D1 — los errores salen por FromResult (ProblemDetails): NotFound → 404; SIN_MEMBRESIA_TENANT → 400 (ErrorCodigoHttpFilter).
    [HttpGet("grupo/{idGrupo}")]
    public async Task<IActionResult> GetByGrupo(int idGrupo, CancellationToken ct)
        => FromResult(await _service.ObtenerPorGrupoAsync(idGrupo, AlcanceTenant, ct));

    [HttpGet("usuario/{idUsuario}")]
    public async Task<IActionResult> GetByUsuario(int idUsuario, CancellationToken ct)
        => FromResult(await _service.ObtenerPorUsuarioAsync(idUsuario, AlcanceTenant, ct));

    [HttpPost]
    [Authorize(Policy = "GRUPOS_EDITAR")]
    public async Task<IActionResult> Agregar([FromBody] CrearGrupoUsuarioDto dto, CancellationToken ct)
    {
        var idUsrMod = User.IdUsuario() ?? 0;
        var result = await _service.AgregarMiembroAsync(dto, AlcanceTenant, idUsrMod, ct);
        if (result.IsFailure) return FromResult(result);

        await _uow.SaveChangesAsync(ct);

        return CreatedFromResult(nameof(GetByGrupo), new { idGrupo = dto.IdGrupo }, result);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "GRUPOS_EDITAR")]
    public async Task<IActionResult> Remover(int id, CancellationToken ct)
    {
        var result = await _service.RemoverMiembroAsync(id, AlcanceTenant, ct);
        if (result.IsFailure) return FromResult(result);

        await _uow.SaveChangesAsync(ct);

        return FromResult(result);
    }
}
