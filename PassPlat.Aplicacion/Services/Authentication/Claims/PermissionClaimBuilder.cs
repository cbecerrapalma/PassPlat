using System.Security.Claims;
using CBP.Results;
using Microsoft.Extensions.Logging;
using PassPlat.Datos.Repositories;

namespace PassPlat.Aplicacion.Services.Authentication.Claims;

public class PermissionClaimBuilder : IPermissionClaimBuilder
{
    private readonly AuthRepository _authRepo;
    private readonly ILogger<PermissionClaimBuilder> _logger;

    public PermissionClaimBuilder(AuthRepository authRepo, ILogger<PermissionClaimBuilder> logger)
    {
        _authRepo = authRepo;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyCollection<Claim>>> BuildPermissionClaimsAsync(
        AuthenticationContext context, CancellationToken ct)
    {
        try
        {
            Result<IReadOnlyList<string>> result;

            if (context.IdTenant == null)
            {
                result = await _authRepo.ObtenerCodigosPermisosPlatformAsync(context.IdUsuario, context.IdApp, ct);
            }
            else if (context.IdUsuarioTenant.HasValue)
            {
                result = await _authRepo.ObtenerCodigosPermisosPorUsuarioTenantAsync(context.IdUsuarioTenant.Value, context.IdApp, ct);
            }
            else
            {
                result = await _authRepo.ObtenerCodigosPermisosPorUsuarioAsync(context.IdUsuario, context.IdTenant.Value, context.IdApp, ct);
            }

            if (result.IsFailure)
                return Result<IReadOnlyCollection<Claim>>.Failure(result.Error!);

            var permisos = result.Value;
            var claims = permisos.Select(c => new Claim("permiso", c)).ToList();

            _logger.LogInformation(AuthenticationEvents.PermissionClaimsBuilt,
                "Claims de permisos construidos | Usuario={IdUsuario} Tenant={IdTenant} App={IdApp} UsuarioTenant={IdUsuarioTenant} Permisos={Count}",
                context.IdUsuario, context.IdTenant, context.IdApp, context.IdUsuarioTenant, claims.Count);

            return Result<IReadOnlyCollection<Claim>>.Success(claims.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo permisos efectivos del usuario {IdUsuario}", context.IdUsuario);
            return Result<IReadOnlyCollection<Claim>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
