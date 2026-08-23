using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.Results;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;
namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "USUARIOS_VERDISP")]
public class DispConfiablesController : BaseApiController
{
    private readonly IDispConfiableService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;
    public DispConfiablesController(IDispConfiableService service, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    [HttpGet("es-confiable/{idUsuario}/{idDisp}")] public async Task<IActionResult> EsConfiable(int idUsuario, int idDisp, CancellationToken ct) => Ok(await _service.EsConfiableAsync(idUsuario, idDisp));
    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.ObtenerTodosConDispositivoAsync(ct));
    [HttpGet("usuario/{idUsuario}")] public async Task<IActionResult> GetByUsuario(int idUsuario, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorUsuarioAsync(idUsuario));
    [HttpPost("marcar-confiable/{idUsuario}/{idDisp}")]
    public async Task<IActionResult> MarcarConfiable(int idUsuario, int idDisp, [FromQuery] string? nombre, [FromQuery] int? idAgente, CancellationToken ct)
    {
        var result = await _service.MarcarComoConfiableAsync(idUsuario, _tenantContext.CurrentId, idDisp, nombre, idAgente, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpPost("revocar-confianza/{idUsuario}/{idDisp}")]
    public async Task<IActionResult> RevocarConfianza(int idUsuario, int idDisp, CancellationToken ct)
    {
        var result = await _service.RevocarConfianzaAsync(idUsuario, idDisp, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var result = await _service.EliminarAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("bloquear/{id}")]
    public async Task<IActionResult> Bloquear(int id, CancellationToken ct)
    {
        var result = await _service.BloquearAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("trigger-new-device/{idUsuario}")]
    public async Task<IActionResult> TriggerNewDevice(int idUsuario, [FromQuery] int idDisp, CancellationToken ct)
    {
        var result = await _service.DetectarNuevoDispositivoAsync(idUsuario, _tenantContext.CurrentId, idDisp, "Test Device", null, "192.168.1.1", "TestAgent/1.0", ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return Ok(new { mensaje = "NewDevice event queued" });
    }

    // Endpoint de diagnóstico/prueba (convención trigger-*): desde `ip` se inyecta
    // una IP controlada para validar el flujo de detección. NO es una vía funcional
    // de negocio: en producción la IP se obtiene del contexto de red, nunca del cliente.
    [HttpPost("trigger-new-ip/{idUsuario}")]
    public async Task<IActionResult> TriggerNewIp(int idUsuario, [FromQuery] string? ip, CancellationToken ct)
    {
        var ipService = HttpContext.RequestServices.GetRequiredService<PassPlat.Aplicacion.Services.IIPService>();
        var outboxRepo = HttpContext.RequestServices.GetRequiredService<IOutboxRepository>();

        Result<NewIpDetectionResult?> txResult;

        try
        {
            txResult = await _uow.ExecuteInTransactionAsync(async () =>
            {
                var result = await ipService.DetectarNuevaIPConOutboxAsync(
                    idUsuario, _tenantContext.CurrentId, ip ?? "10.0.0.99", "TestAgent/1.0", "Test Device", ct);

                if (result.IsFailure) return result;

                if (result.Value?.Outbox != null)
                    await outboxRepo.AddAsync(result.Value.Outbox!, ct);

                return result;
            }, ct);
        }
        catch (DbUpdateException ex) when (EsViolacionIndiceUnico(ex))
        {
            // Carrera: otro request ganó el INSERT IP (UQ_IPs_Direccion). El perdedor
            // se rechaza limpio — el creador real persistió su Outbox. Es el diseño
            // S21: el índice único es el árbitro determinista del creador.
            return Ok(new { mensaje = "NewIp event ya encolado por otra solicitud", ip = ip ?? "10.0.0.99", queued = false });
        }

        if (txResult.IsFailure) return FromResult(txResult);
        return Ok(new { mensaje = "NewIp event queued", ip = ip ?? "10.0.0.99", queued = txResult.Value?.Outbox != null });
    }

    private static bool EsViolacionIndiceUnico(DbUpdateException ex)
    {
        for (var e = ex.InnerException; e != null; e = e.InnerException)
        {
            if (e is Microsoft.Data.SqlClient.SqlException sqlex &&
                (sqlex.Number == 2601 || sqlex.Number == 2627))
                return true;
        }
        return false;
    }
}
