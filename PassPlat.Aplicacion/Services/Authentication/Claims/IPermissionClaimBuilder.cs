using System.Security.Claims;
using CBP.Results;

namespace PassPlat.Aplicacion.Services.Authentication.Claims;

public interface IPermissionClaimBuilder
{
    Task<Result<IReadOnlyCollection<Claim>>> BuildPermissionClaimsAsync(
        AuthenticationContext context, CancellationToken ct = default);
}
