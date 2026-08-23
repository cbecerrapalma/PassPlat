using Microsoft.Extensions.Logging;

namespace PassPlat.Aplicacion.Services.Authentication;

public static class AuthenticationEvents
{
    public static readonly EventId TokenGenerated = new(1000, nameof(TokenGenerated));
    public static readonly EventId SessionCreated = new(1001, nameof(SessionCreated));
    public static readonly EventId SessionUpdated = new(1002, nameof(SessionUpdated));
    public static readonly EventId SessionRevoked = new(1003, nameof(SessionRevoked));
    public static readonly EventId PermissionClaimsBuilt = new(1004, nameof(PermissionClaimsBuilt));
}
