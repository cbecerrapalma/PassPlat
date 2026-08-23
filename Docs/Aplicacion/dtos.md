# DTO Pattern

**Location**: `PassPlat.Aplicacion\Dtos\{Catalogos|Contexto|Core}\`

## Naming Conventions

| DTO Type | Naming Pattern | Example |
|----------|---------------|---------|
| Read DTO | `{Entity}Dto` | `TenantDto`, `UsuarioDto` |
| Create DTO | `Crear{Entity}Dto` | `CrearTenantDto`, `CrearUsuarioDto` |
| Register DTO | `Registrar{Entity}Dto` | `RegistrarIntentoAccesoDto` |
| Assign DTO | `Asignar{Entity}Dto` | `AsignarAccesoDto` |
| Generate DTO | `Generar{Entity}Dto` | `GenerarTokenRestDto` |
| Update DTO | `Actualizar{Entity}Dto` | `ActualizarUsuarioDto`, `ActualizarPoliticaPwdDto` |

## Structure

- All DTOs for a layer are in one file per layer:
  - `Catalogos/CatalogosDto.cs`
  - `Contexto/ContextoDto.cs`
  - `Core/` — one file per DTO pair

## Read DTO Pattern

Mirrors entity properties exactly, with optional navigation display names:

```csharp
public class RolDto
{
    public int Id { get; set; }
    public int? IdTenant { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
    public DateTime FecCrea { get; set; }
    public string? TenantNombre { get; set; }  // Navigation display name
}
```

## Create DTO Pattern

Subset of entity properties — only what's needed for creation:

```csharp
public class CrearRolDto
{
    public int? IdTenant { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}
```

## Update DTO Pattern

All properties optional (null = no change):

```csharp
public class ActualizarUsuarioDto
{
    public int Id { get; set; }
    public int? IdEstado { get; set; }
    public string? Nombre { get; set; }
    public string? Apellido { get; set; }
    public bool? EmailVerificado { get; set; }
}
```
