# FluentValidation Validators

**Location**: `PassPlat.Aplicacion\Validations\{Catalogos|Core}\`

## Naming Convention

```
{DtoName}Validator : AbstractValidator<{DtoName}>
```

## Auto-Registration

Validators are auto-discovered via assembly scanning:

```csharp
services.AddValidatorsFromAssembly(typeof(AplicacionDependencyInjection).Assembly);
```

No manual registration needed for individual validators.

## Validation Rules

### Catalog Validators

| Validator | Rules |
|-----------|-------|
| `CrearTenantDtoValidator` | Codigo: not empty, max 20. Nombre: not empty, max 100 |
| `CrearAppDtoValidator` | Codigo: not empty, max 50. Nombre: not empty, max 100. UrlBase: max 255 (when provided) |
| `CrearRolDtoValidator` | Codigo: not empty, max 20. Nombre: not empty, max 50. Descripcion: max 200 (when provided) |
| `CrearDominioTenantDtoValidator` | IdTenant: > 0. Dominio: not empty, max 100 |

### Core Validators

| Validator | Key Rules |
|-----------|-----------|
| `CrearUsuarioDtoValidator` | IdTenant/IdEstado > 0, NomUsuario max 100, Email max 255 + valid format, Nombre/Apellido max 100 |
| `CrearPoliticaPwdDtoValidator` | Codigo max 20, LongMin >= 8, LongMax > LongMin, PwdRecordadas >= 1, MaxIntentos >= 1, DurBloqueoMin > 0 |
| `CrearSesionDtoValidator` | All FK > 0, IdTokenExt max 128, FecExpira > UtcNow |
| `CrearBloqueoDtoValidator` | FK > 0, Motivo max 200 |
| `AsignarAccesoDtoValidator` | All FK > 0 |
| `RegistrarMFADtoValidator` | FK > 0, IdMFA max 200 |
| `RegistrarIntentoAccesoDtoValidator` | NomUsuarioIntentado max 100, IdResultado > 0 |
| `RegistrarAuditoriaPwdDtoValidator` | FK > 0, Detalles max 500 |
| `CrearNotificacionDtoValidator` | FK > 0, TipoNotif max 50, Asunto max 200 |
| `CrearRolPoliticaPwdDtoValidator` | All FK > 0 |
| `CrearDispConfiableDtoValidator` | FK > 0, Nombre max 100 |
| `GenerarTokenRestDtoValidator` | FK > 0, FecVence > UtcNow |
