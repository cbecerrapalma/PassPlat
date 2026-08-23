using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "PERMISOS_VER")]
public class PermisosController : BaseApiController
{
    private readonly IPermisoService _permisoService;
    private readonly IRolPermisoService _rolPermisoService;
    private readonly IUnitOfWorkAsync _uow;
    public PermisosController(IPermisoService permisoService, IRolPermisoService rolPermisoService, IUnitOfWorkAsync uow)
    {
        _permisoService = permisoService;
        _rolPermisoService = rolPermisoService;
        _uow = uow;
    }

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _permisoService.ObtenerTodosAsync(ct));
    [HttpGet("activos")] public async Task<IActionResult> GetActivos(CancellationToken ct) => FromResultQuery(await _permisoService.ObtenerActivosAsync(ct));
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _permisoService.ObtenerPorIdAsync(id, ct));
    [HttpGet("rol/{idRol}")] public async Task<IActionResult> GetPermisosPorRol(int idRol, CancellationToken ct) => FromResultQuery(await _rolPermisoService.ObtenerPermisosPorRolAsync(idRol, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] CrearPermisoDto dto, CancellationToken ct)
    {
        var result = await _permisoService.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return Created(string.Empty, result.Value);
    }
    [HttpPost("rol")] public async Task<IActionResult> AsignarPermiso([FromBody] AsignarPermisoDto dto, CancellationToken ct)
    {
        var result = await _rolPermisoService.AsignarPermisoAsync(dto, ct);
        if (result.IsFailure) return FromResultQuery(result);
        await _uow.SaveChangesAsync(ct);
        return FromResultQuery(result);
    }
    [HttpPut("{id}")] public async Task<IActionResult> Update(int id, [FromBody] ActualizarPermisoDto dto, CancellationToken ct)
    {
        var result = await _permisoService.ActualizarAsync(id, dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return Ok(result.Value);
    }
    [HttpDelete("rol/{idRol}/{idPermiso}")] public async Task<IActionResult> DesasignarPermiso(int idRol, int idPermiso, CancellationToken ct)
    {
        var result = await _rolPermisoService.DesasignarPermisoAsync(idRol, idPermiso, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpDelete("{id}")] public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var result = await _permisoService.EliminarAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
