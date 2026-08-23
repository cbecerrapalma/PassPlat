using System.Security.Claims;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Dashboard;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "USUARIOS_VER")]
[Route("api/dashboard-enterprise")]
public class DashboardEnterpriseController : BaseApiController
{
    private readonly IDashboardEnterpriseService _service;
    private readonly IBackgroundStatusService _bgService;

    public DashboardEnterpriseController(IDashboardEnterpriseService service, IBackgroundStatusService bgService)
    {
        _service = service;
        _bgService = bgService;
    }

    private int? TenantId
    {
        get
        {
            var claim = User.FindFirst("TenantId")?.Value;
            return claim != null && int.TryParse(claim, out var id) ? id : null;
        }
    }

    [HttpGet("ejecutivo")]
    public async Task<IActionResult> GetEjecutivo(CancellationToken ct)
    {
        var result = await _service.GetEjecutivoAsync(TenantId, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(result.Value);
    }

    [HttpGet("seguridad")]
    public async Task<IActionResult> GetSeguridad(CancellationToken ct)
    {
        var result = await _service.GetSeguridadAsync(TenantId, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(result.Value);
    }

    [HttpGet("oauth")]
    public async Task<IActionResult> GetOAuth(CancellationToken ct)
    {
        var result = await _service.GetOAuthAsync(TenantId, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(result.Value);
    }

    [HttpGet("email")]
    public async Task<IActionResult> GetEmail(CancellationToken ct)
    {
        var result = await _service.GetEmailAsync(TenantId, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(result.Value);
    }

    [HttpGet("operacional")]
    public async Task<IActionResult> GetOperacional(CancellationToken ct)
    {
        var result = await _service.GetOperacionalAsync(TenantId, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(result.Value);
    }

    [HttpGet("auditoria")]
    public async Task<IActionResult> GetAuditoria(CancellationToken ct)
    {
        var result = await _service.GetAuditoriaAsync(TenantId, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(result.Value);
    }

    [HttpGet("dispositivos")]
    public async Task<IActionResult> GetDispositivos(CancellationToken ct)
    {
        var result = await _service.GetDispositivosAsync(TenantId, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(result.Value);
    }

    [HttpGet("tendencias")]
    public async Task<IActionResult> GetTendencias(CancellationToken ct)
    {
        var result = await _service.GetTendenciasAsync(TenantId, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(result.Value);
    }

    [HttpGet("estado-general")]
    public async Task<IActionResult> GetEstadoGeneral(CancellationToken ct)
    {
        var result = await _service.GetEstadoGeneralAsync(TenantId, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(result.Value);
    }

    [HttpGet("ejecutivo-avanzado")]
    public async Task<IActionResult> GetEjecutivoAvanzado(CancellationToken ct)
    {
        var result = await _service.GetEjecutivoAvanzadoAsync(TenantId, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(result.Value);
    }

    [HttpGet("background")]
    public async Task<IActionResult> GetBackground(CancellationToken ct)
    {
        var result = await _bgService.GetBackgroundJobsAsync(ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(result.Value);
    }
}