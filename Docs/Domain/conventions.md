# Domain Conventions (PassPlat.Dominio)

## Entity Naming

| Convention | Rule | Example |
|------------|------|---------|
| **PK property** | Always `Id` | `public int Id { get; set; }` |
| **FK property** | `Id{RelatedTable}` | `IdTenant`, `IdUsuario`, `IdApp` |
| **Navigation** | Simple name, no suffix | `Tenant?`, `Usuario?`, `App?` (NOT `TenantNav`) |
| **Collections** | `ICollection<T>` = `[]` | `public ICollection<Acceso> Accesos { get; set; } = [];` |
| **Language** | Spanish | `NomUsuario`, `FecCrea`, `EsActivo` |
| **Factory method** | Static `Crear(...)` | `Tenant.Crear(codigo, nombre)` |

## PK Types by Table

| Type | Tables |
|------|--------|
| `int` (IDENTITY) | All catalog + most core tables (Tenant, App, Rol, Usuario, etc.) |
| `long` (bigint IDENTITY) | `HistorialPwd`, `TokensRest`, `IntentosAcceso`, `AuditoriaPwd`, `Notificacion` |
| `Guid` (uniqueidentifier) | `Sesion` |

## Enum Naming

| Convention | Rule | Example |
|------------|------|---------|
| **Prefix** | `E` to avoid collisions | `EEstadoUsuario` (not `EstadoUsuario`) vs entity `EstadoUsr` |
| **Values** | PascalCase Spanish | `Activo`, `Inactivo`, `Bloqueado` |
| **Backing type** | `byte` | `public enum EEstadoUsuario : byte` |

## Enum Catalog

| Enum | Values (Id) |
|------|-------------|
| `EEstadoUsuario` | Activo(1), Inactivo(2), Bloqueado(3), Eliminado(4), Pendiente(5), Suspendido(6) |
| `EEstadoMFA` | Activo(1), Inactivo(2), Pendiente(3), Revocado(4) |
| `EResultadoAcceso` | Exitoso(1), CredencialesInvalidas(2), CuentaBloqueada(3), SinAccesoApp(4), ErrorSistema(5), CuentaInactiva(6), MFARequerido(7), TokenExpirado(8), IPBloqueada(9) |
| `ETipoAuditoria` | LoginExitoso(1), LoginFallido(2), CambioPassword(3), ResetPassword(4), RevocacionSesiones(5), RegistroMFA(6), EliminacionCuenta(7), CambioPolitica(8), BloqueoCuenta(9), DesbloqueoCuenta(10) |
| `ETipoBloqueo` | Temporal(1), Permanente(2), Administrativo(3), Seguridad(4) |
| `ETipoCambioPwd` | Voluntario(1), Forzado(2), Reset(3), PrimerUso(4), Expiracion(5), Comprometida(6) |
| `ETipoDisp` | Desktop(1), Movil(2), Tablet(3), Servidor(4), Otro(5) |
| `ETipoMFA` | TOTP(1), SMS(2), Email(3), WebAuthn(4), Push(5), BackupCodes(6) |

## Constants

`Codigos.cs` contains string constants with SQL-friendly codes for each enum value:

```csharp
Codigos.EstadoUsuario.Activo       // "ACTIVO"
Codigos.ResultadoAcceso.Exitoso    // "EXITOSO"
Codigos.TipoBloqueo.Temporal      // "TEMPORAL"
```

## Global Usings

`PassPlat.Dominio\GlobalUsings.cs` auto-imports:
- All entity namespaces
- All enum namespaces
- All constants namespaces

No explicit `using` directives needed in configurations or repositories for domain entities.

## Factory Method Pattern

Entities use static factory methods instead of `new`:

```csharp
// Good
var tenant = Tenant.Crear("COD01", "Mi Tenant");

// Avoid
var tenant = new Tenant { Codigo = "COD01", Nombre = "Mi Tenant" };
```

Factory methods set sensible defaults (Activo = true, FecCrea = DateTime.UtcNow, etc.).
