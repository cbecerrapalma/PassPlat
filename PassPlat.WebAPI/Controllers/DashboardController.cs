using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "USUARIOS_VER")]
public class DashboardController : BaseApiController
{
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IIdenExtRepository _identidadRepo;
    private readonly IMFARepository _mfaRepo;
    private readonly IIntentoAccesoRepository _intentoRepo;

    public DashboardController(
        IUsuarioRepository usuarioRepo,
        IIdenExtRepository identidadRepo,
        IMFARepository mfaRepo,
        IIntentoAccesoRepository intentoRepo)
    {
        _usuarioRepo = usuarioRepo;
        _identidadRepo = identidadRepo;
        _mfaRepo = mfaRepo;
        _intentoRepo = intentoRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var usuariosResult = await _usuarioRepo.GetAllAsync(asNoTracking: true, ct: ct);
        if (usuariosResult.IsFailure) return FromResult(usuariosResult);

        var usuarios = usuariosResult.Value;
        var total = usuarios.Count;
        var locales = usuarios.Count(u => u.TienePasswordLocal);
        var bloqueados = usuarios.Count(u => u.IdEstado == 3);
        var inactivos = usuarios.Count(u => u.IdEstado == 2);

        var identidadesResult = await _identidadRepo.GetAllAsync(asNoTracking: true, ct: ct);
        var identidades = identidadesResult.IsSuccess ? identidadesResult.Value : [];

        var usuariosConIdentidad = identidades.Where(i => !i.Eliminado).Select(i => i.IdUsuario).Distinct().ToHashSet();
        var oauth = usuarios.Count(u => !u.TienePasswordLocal && usuariosConIdentidad.Contains(u.Id));
        var hibridos = usuarios.Count(u => u.TienePasswordLocal && usuariosConIdentidad.Contains(u.Id));

        var mfaResult = await _mfaRepo.GetAllAsync(asNoTracking: true, ct: ct);
        var conMfa = mfaResult.IsSuccess ? mfaResult.Value.Count(m => m.EsPrincipal && m.IdEstado == 1) : 0;

        var proveedores = identidades
            .Where(i => !i.Eliminado && i.ProvIden != null)
            .GroupBy(i => new { i.ProvIden!.Codigo, i.ProvIden!.Nombre })
            .Select(g => new ProveedorConteoDto
            {
                Codigo = g.Key.Codigo,
                Nombre = g.Key.Nombre,
                Vinculaciones = g.Count()
            })
            .OrderByDescending(p => p.Vinculaciones)
            .ToList();

        var intentosResult = await _intentoRepo.GetAllAsync(asNoTracking: true, ct: ct);
        var intentosRecientes = intentosResult.IsSuccess
            ? intentosResult.Value
                .OrderByDescending(i => i.FecIntento)
                .Take(10)
                .Select(i => new IntentoRecienteDto
                {
                    Id = i.Id,
                    NomUsuario = i.NomUsuarioIntentado,
                    MetodoAutenticacion = i.MetodoAutenticacion,
                    Exitoso = i.Exitoso,
                    FecIntento = i.FecIntento
                })
                .ToList()
            : [];

        return Ok(new DashboardDto
        {
            TotalUsuarios = total,
            UsuariosLocales = locales,
            UsuariosOAuth = oauth,
            UsuariosHibridos = hibridos,
            UsuariosConMFA = conMfa,
            UsuariosBloqueados = bloqueados,
            UsuariosInactivos = inactivos,
            Proveedores = proveedores,
            IntentosRecientes = intentosRecientes
        });
    }
}
