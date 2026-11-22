using CBP.Results;
using CBP.Results.Errors;
using PassPlat.Datos.Repositories;

namespace PassPlat.Aplicacion.Services;

public interface IVisibilidadUsuarioService
{
    Task<Result> VerificarAsync(int idSolicitante, int idUsuario, int idTenant, string recurso, CancellationToken ct = default);

    /// <summary>
    /// S120 · D3 — administrar datos de otro usuario: el propio pasa; otro exige <paramref name="puedeAdministrar"/>
    /// (si no, FORBIDDEN sin consultar la BD) y membresía activa en el tenant (<see cref="VerificarAsync"/>).
    /// </summary>
    Task<Result> VerificarAdministracionAsync(int idSolicitante, int idUsuario, int idTenant, bool puedeAdministrar, string recurso, CancellationToken ct = default);
}

public sealed class VisibilidadUsuarioService(IUsuarioTenantRepository usuarioTenantRepo) : IVisibilidadUsuarioService
{
    private readonly IUsuarioTenantRepository _usuarioTenantRepo = usuarioTenantRepo;

    // S69.1 · D1 — Visibilidad de usuario por membresía de tenant
    public async Task<Result> VerificarAsync(int idSolicitante, int idUsuario, int idTenant, string recurso, CancellationToken ct = default)
    {
        if (idSolicitante == idUsuario)
        {
            return Result.Success();
        }

        var membresia = await _usuarioTenantRepo.ObtenerActivoPorTenantAsync(idUsuario, idTenant, ct);
        if (membresia.IsFailure)
        {
            return Result.Failure(membresia.Error!);
        }

        if (membresia.Value is null)
        {
            return Result.Failure(CommonErrors.Authorization.Forbidden(recurso));
        }

        return Result.Success();
    }

    public Task<Result> VerificarAdministracionAsync(int idSolicitante, int idUsuario, int idTenant, bool puedeAdministrar, string recurso, CancellationToken ct = default)
    {
        if (idSolicitante != idUsuario && !puedeAdministrar)
        {
            return Task.FromResult(Result.Failure(CommonErrors.Authorization.Forbidden(recurso)));
        }

        return VerificarAsync(idSolicitante, idUsuario, idTenant, recurso, ct);
    }
}
