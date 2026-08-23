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

[Authorize(Policy = "INTENTOS_ACCESO_VER")]
public class IntentosAccesoController : BaseApiController
{
    private readonly IIntentoAccesoService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;
    public IntentosAccesoController(IIntentoAccesoService service, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    [HttpGet("page")] public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request, CancellationToken ct)
    {
        var options = new PaginationOptions<IntentoAcceso>(request.PageNumber, request.PageSize);
        var result = await _service.GetPagedAsync(options, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }
    [HttpGet("recientes/{idUsuario}/{minutos}")] public async Task<IActionResult> GetRecientes(int idUsuario, int minutos, CancellationToken ct) => FromResultQuery(await _service.ObtenerIntentosRecientesAsync(idUsuario, minutos));
    [HttpGet("contar-fallidos/{idUsuario}/{minutos}")] public async Task<IActionResult> ContarFallidos(int idUsuario, int minutos, CancellationToken ct) => Ok(await _service.ContarIntentosFallidosRecientesAsync(idUsuario, minutos));
    [HttpGet("contar-fallidos-ip/{idIP}/{minutos}")] public async Task<IActionResult> ContarFallidosPorIP(int idIP, int minutos, CancellationToken ct) => Ok(await _service.ContarIntentosFallidosPorIPAsync(idIP, minutos));
    [HttpPost("registrar")] public async Task<IActionResult> Registrar([FromBody] RegistrarIntentoAccesoDto dto, CancellationToken ct)
    {
        var result = await _service.RegistrarIntentoAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return Ok(result.Value);
    }
}
