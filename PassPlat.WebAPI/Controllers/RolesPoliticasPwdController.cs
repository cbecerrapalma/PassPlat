using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "POLITICAS_PWD_VER")]
public class RolesPoliticasPwdController : BaseApiController
{
    private readonly IRolPoliticaPwdService _service;
    private readonly IUnitOfWorkAsync _uow;
    public RolesPoliticasPwdController(IRolPoliticaPwdService service, IUnitOfWorkAsync uow)
    {
        _service = service;
        _uow = uow;
    }

    [HttpGet("rol/{idRol}")] public async Task<IActionResult> GetByRol(int idRol, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorRolAsync(idRol));
    [HttpPost] public async Task<IActionResult> Create([FromBody] CrearRolPoliticaPwdDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetByRol), new { idRol = dto.IdRol }, result.Value);
    }
    [HttpPost("{id}/desactivar")] public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var result = await _service.DesactivarAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
