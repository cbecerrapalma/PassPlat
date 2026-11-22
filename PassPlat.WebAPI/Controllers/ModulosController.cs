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
using PassPlat.WebAPI.Auth;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "PERMISOS_VER")]
public class ModulosController : BaseApiController
{
    private readonly IModuloService _moduloService;
    private readonly IUsuarioPermisoService _usuarioPermisoService;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;

    public ModulosController(IModuloService moduloService, IUsuarioPermisoService usuarioPermisoService, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _moduloService = moduloService;
        _usuarioPermisoService = usuarioPermisoService;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    private int? AlcanceTenant => User.AlcanceTenant(_tenantContext);

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct, [FromQuery] int? idApp)
    {
        var apps = await _usuarioPermisoService.ObtenerAppsPermitidasAsync(User.IdUsuario() ?? 0, AlcanceTenant, idApp, ct);
        if (apps.IsFailure) return FromResult(apps);
        if (apps.Value == null) return FromResultQuery(await _moduloService.ObtenerArbolCompletoAsync(ct));
        return FromResultQuery(await _moduloService.ObtenerArbolPorAppsAsync(apps.Value, ct));
    }

    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _moduloService.GetByIdAsync(id));

    [HttpGet("app/{idApp}")]
    public async Task<IActionResult> GetPorApp(int idApp, CancellationToken ct)
    {
        // S110 D3 - sin Acceso activo a idApp en su tenant: SIN_ACCESO_APP (403); sistema no se valida
        var apps = await _usuarioPermisoService.ObtenerAppsPermitidasAsync(User.IdUsuario() ?? 0, AlcanceTenant, idApp, ct);
        if (apps.IsFailure) return FromResult(apps);
        return FromResultQuery(await _moduloService.ObtenerPorAppAsync(idApp, ct));
    }

    [HttpPost]
    [Authorize(Policy = "PERMISOS_CREAR")]
    public async Task<IActionResult> Create([FromBody] CrearModuloDto dto, CancellationToken ct)
    {
        var result = await _moduloService.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "PERMISOS_EDITAR")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarModuloDto dto, CancellationToken ct)
    {
        var result = await _moduloService.ActualizarAsync(id, dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
