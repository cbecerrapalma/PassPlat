# AutoMapper Profile

**File**: `PassPlat.Aplicacion\Mapping\AplicacionProfile.cs`

## Mapping Conventions

### Entity → Dto

```csharp
CreateMap<Entity, EntityDto>();
```

### Navigation display names

```csharp
CreateMap<Acceso, AccesoDto>()
    .ForMember(d => d.AppNombre, o => o.MapFrom(s => s.App != null ? s.App.Nombre : null))
    .ForMember(d => d.RolNombre, o => o.MapFrom(s => s.Rol != null ? s.Rol.Nombre : null));
```

### CrearDto → Entity

```csharp
CreateMap<CrearTenantDto, Tenant>();
```

### ActualizarDto → Entity (conditional)

Only map non-null values to avoid overwriting existing data:

```csharp
CreateMap<ActualizarUsuarioDto, Usuario>()
    .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));
```

## DI Registration

AutoMapper is registered with assembly scanning in `AplicacionDependencyInjection.cs`:

```csharp
services.AddAutoMapper(cfg => cfg.AddProfile<AplicacionProfile>(), typeof(AplicacionProfile));
```
