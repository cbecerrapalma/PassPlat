# A1 — Implementation Plan

**Status**: DRAFT
**Date**: 2026-07-28
**Depends on**: A0 FROZEN (ADR-001..005, A0.2, A0.3, A0.4 APPROVED)
**Next**: A1.1 — SQL / Schema

---

## 0. Traceability: A0 Decisions → A1 Tasks

Cada decisión de A0 se traza a tareas concretas de A1 para garantizar que ninguna decisión arquitectónica quede sin implementar.

| A0 ID | Decisión | A1 Tasks |
|--------|----------|----------|
| D01 | Usuario = identidad global SIN IdTenant | A1.1-001, A1.3-001, A1.4-003 |
| D02 | UsuarioTenant = membership explícita | A1.1-002, A1.1-003, A1.3-002, A1.4-001 |
| D03 | Acceso Modelo A: IdUsuarioTenant? NULL = Platform Scope | A1.1-005, A1.3-003, A1.4-004 |
| D04 | Platform Scope: IdUsuarioTenant=NULL + Rol.IdTenant=NULL | A1.5-004, A1.6-002 |
| D05 | AuthenticationContext como Execution Context | A1.5-001, A1.5-002 |
| D06 | IdenExt.IdTenant = KEEP como INITIAL_CONTEXT | A1.2-005 (validar, no migrar) |
| D07 | MFA.IdTenant = KEEP como EXECUTION_CONTEXT | U01 (A1.2-006) |
| D08 | EsTenantPrincipal = solo background services | A1.3-002, A1.5-006 |
| D09 | HTTP 428 = responsabilidad de Application layer | A1.5-005 |
| D10 | 3 entidades migran, 26 KEEP, 1 nueva | A1.1-001..006, A1.2-001..004 |
| D11 | Triggers: 2 eliminar, 2 reescribir, 1 modificar | A1.1-007..011 |
| D12 | SPs: 5 cambian directamente, 9 indirectamente | A1.1-012..016, A1.4-005..009 |
| D13 | 16 puntos de falla en servicios C# | A1.5-003 |
| D14 | Preflight (Phase 1) = 10 queries read-only | A1.2-001 |
| D15 | Rollback pre-cutover ≠ post-cutover | A1.1-017 (scripts DOWN) |
| D16 | Expand→Migrate→Deploy→Contract | A1.2 (orden de ejecución) |
| D17 | Legacy columns READ-ONLY post-CUTOVER | A1.3-004 (config), A1.5-007 (validación) |

---

## 1. Solución y proyectos afectados

### Proyectos que cambian

| Proyecto | Ruta | Tipo de cambio |
|----------|------|----------------|
| PassPlat.Dominio | `PassPlat.Dominio/` | Nueva entidad UsuarioTenant + modificar Usuario, Acceso, UsuarioPermiso |
| PassPlat.Datos | `PassPlat.Datos/` | Nuevas configs EF, repositorios, SPs, DbContext |
| PassPlat.Aplicacion | `PassPlat.Aplicacion/` | Nuevos servicios, DTOs, AuthenticationContext, cambios en AuthService |
| PassPlat.Aplicacion.Dtos | `PassPlat.Aplicacion.Dtos/` | Nuevos DTOs UsuarioTenant |
| PassPlat.WebAPI | `PassPlat.WebAPI/` | Nuevos endpoints + modificar autenticación |
| PassPlat.Web | `PassPlat.Web/` | UI multi-tenant, Platform scope |

### Proyectos que NO cambian

Framework CBP (15 proyectos en `D:\CODIGOS\CBP\`): ninguno requiere cambios.

---

## 2. Secuencia de implementación

```
A1.1 SQL / Schema ──────► A1.2 Data Migration ──────► A1.3 Domain / EF
                                                              │
                                                              ▼
                                              A1.4 Repository / SP
                                                      │
                                                      ▼
                                              A1.5 Application
                                                      │
                                                      ▼
                                              A1.6 WebAPI
                                                      │
                                                      ▼
                                              A1.7 Blazor
                                                      │
                                                      ▼
                                              A1.8 Testing
```

**Dependencia estricta**: Cada bloque depende del anterior. No comenzar A1.3 sin completar A1.2. No comenzar A1.5 sin A1.4.

---

## 3. A1.1 — SQL / Schema

### 3.1 Inventario de objetos SQL afectados

#### Tablas (4)

| Tabla | Acción | Tipo |
|-------|--------|------|
| UsuarioTenant | CREAR | Nueva |
| Usuarios | MODIFICAR — eliminar IdTenant, FK, índices | Migración |
| Accesos | MODIFICAR — agregar IdUsuarioTenant?, FK compuesta | Migración |
| UsuariosPermisos | MODIFICAR — agregar IdUsuarioTenant?, FK compuesta | Migración |

#### Tablas KEEP (26)

Todas las demás tablas (Tenants, Apps, Roles, MFA, IdenExt, etc.) no cambian su estructura. Validar en Preflight.

#### Stored Procedures (5 cambian directamente, 9 indirectamente)

| SP | Cambio | Prioridad |
|----|--------|-----------|
| SP_Auth_Login | Búsqueda global de usuario (sin IdTenant). Resolver tenancy post-password | P0 |
| SP_Auth_LoginExterno | Sin filtro IdTenant en auto-link | P0 |
| SP_Usuario_Crear | Sin IdTenant en unicidad de NomUsuario/Email | P0 |
| SP_Permisos_Usuario_Efectivos | JOIN a UsuarioTenant en lugar de Accesos.IdTenant | P0 |
| SP_Dashboard_* (5 SPs) | COUNT sobre UsuarioTenant en lugar de Usuarios.IdTenant | P1 |

#### Triggers (5 cambian)

| Trigger | Acción |
|---------|--------|
| TR_Accesos_ValidarTenant | ELIMINAR (reemplazado por FK compuesta) |
| TR_UsuariosPermisos_ValidarTenant | ELIMINAR (reemplazado por FK compuesta) |
| TR_GruposUsuarios_ValidarTenant | REESCRIBIR contra UsuarioTenant |
| TR_Usuarios_ValidarEsSistema | REESCRIBIR contra UsuarioTenant |
| TR_Usuarios_Mod | MODIFICAR — quitar `OR UPDATE(IdTenant)` |

#### Índices (7 cambian)

| Índice actual | Acción | Nuevo índice |
|---------------|--------|-------------|
| UX_Usuarios_Tenant_NomUsuario | ELIMINAR | UX_Usuarios_NomUsuario (global) WHERE Eliminado=0 |
| UX_Usuarios_TenantEmail | ELIMINAR | UX_Usuarios_Email (global) WHERE Eliminado=0 AND Email NOT NULL |
| IX_Usuarios_Tenant | ELIMINAR | — |
| IX_Accesos_UsuarioTenantAppActivo | RECREAR | (IdUsuario, IdUsuarioTenant, IdApp, Activo) INCLUDE (IdRol) |
| IX_Accesos_Tenant | ELIMINAR | IX_UsuarioTenant_Tenant |
| UX_UsuariosPermisos_Activo_App | RECREAR | (IdUsuario, IdPermiso, IdUsuarioTenant, IdApp) |
| UX_UsuariosPermisos_Activo_Global | RECREAR | (IdUsuario, IdPermiso, IdUsuarioTenant) |
| — | NUEVO | **UX_UsuarioTenant_Usuario_Tenant**: UNIQUE(IdUsuario, IdTenant) |
| — | NUEVO | **IX_UsuarioTenant_Principal**: (IdUsuario) INCLUDE (IdTenant) WHERE EsTenantPrincipal=1 |

#### FKs (6 cambian)

| FK actual | Acción | Nueva FK |
|-----------|--------|----------|
| FK_Usuarios_Tenant (Usuarios.IdTenant → Tenants.Id) | ELIMINAR | — |
| FK_Accesos_Tenant (Accesos.IdTenant → Tenants.Id) | ELIMINAR | — |
| FK_UsuariosPermisos_Tenant (UP.IdTenant → Tenants.Id) | ELIMINAR | — |
| — | NUEVA | FK_UsuarioTenant_Usuario (UsuarioTenant.IdUsuario → Usuarios.Id) |
| — | NUEVA | FK_UsuarioTenant_Tenant (UsuarioTenant.IdTenant → Tenants.Id) |
| — | NUEVA | **FK_Accesos_UsuarioTenant** compuesta: (Accesos.IdUsuarioTenant, Accesos.IdUsuario) → UsuarioTenant(Id, IdUsuario). Solo se evalúa cuando IdUsuarioTenant IS NOT NULL |
| — | NUEVA | **FK_UP_UsuarioTenant** compuesta: (UP.IdUsuarioTenant, UP.IdUsuario) → UsuarioTenant(Id, IdUsuario). Solo se evalúa cuando IdUsuarioTenant IS NOT NULL |

### 3.2 Scripts UP/DOWN

Cada objeto debe tener script UP + DOWN probado antes de la migración.

```
Migrations/A1/
├── A1.1_UP__Create_UsuarioTenant.sql
├── A1.1_DOWN__Drop_UsuarioTenant.sql
├── A1.1_UP__Alter_Accesos.sql
├── A1.1_DOWN__Revert_Accesos.sql
├── A1.1_UP__Alter_UsuariosPermisos.sql
├── A1.1_DOWN__Revert_UsuariosPermisos.sql
├── A1.1_UP__Drop_UsuarioIdTenant.sql
├── A1.1_DOWN__Restore_UsuarioIdTenant.sql
├── A1.1_UP__Indexes_FKs.sql
├── A1.1_DOWN__Drop_Indexes_FKs.sql
├── A1.1_UP__Triggers.sql
├── A1.1_DOWN__Restore_Triggers.sql
├── A1.1_UP__SPs.sql
├── A1.1_DOWN__Restore_SPs.sql
├── Preflight.sql              (Phase 1 — 10 queries)
├── Contract.sql               (Phase 8C — eliminar legacy)
└── Rollback_Contract.sql      (restore pre-contract)
```

### 3.3 Orden de ejecución (Phase 2)

```
1. CREATE TABLE UsuarioTenant (con FKs, índices, defaults)
2. ALTER TABLE Accesos ADD IdUsuarioTenant int NULL
3. ALTER TABLE UsuariosPermisos ADD IdUsuarioTenant int NULL
4. CREATE INDEX IX_UsuarioTenant_Tenant
5. CREATE UNIQUE INDEX UX_UsuarioTenant_Usuario_Tenant
6. CREATE INDEX IX_UsuarioTenant_Principal (filtered)
7. CREATE FK compuesta Accesos → UsuarioTenant
8. CREATE FK compuesta UsuariosPermisos → UsuarioTenant
9. (Data migration en A1.2)
10. (Post-migración) DROP Usuarios.IdTenant, FKs legacy, índices legacy
```

---

## 4. A1.2 — Data Migration

### 4.1 Preflight (Phase 1 — 10 queries)

Todas deben retornar 0 filas. Si alguna retorna filas → ABORT.

| # | Query | Detecta |
|---|-------|---------|
| 1.1 | Usuarios con Eliminado=0 pero IdTenant inválido | FK huérfana |
| 1.2 | Accesos con IdTenant ≠ Usuario.IdTenant | Violación de trigger actual |
| 1.3 | Accesos con Rol.IdTenant ≠ Accesos.IdTenant | Error de configuración |
| 1.4 | UsuariosPermisos con IdTenant ≠ Usuario.IdTenant | Inconsistencia |
| 1.5 | NomUsuario duplicado global | Conflicto de unicidad |
| 1.6 | Email duplicado global | Conflicto de unicidad |
| 1.7 | Roles sin IdTenant (posibles Platform roles) | Inventario |
| 1.8 | Accesos con Rol.IdTenant=NULL (Platform potencial) | Inventario |
| 1.9 | Accesos con Usuario.Eliminado=1 | Datos huérfanos |
| 1.10 | UsuariosPermisos con Usuario.Eliminado=1 | Datos huérfanos |

### 4.2 Migración de datos (Phase 3)

Orden:

```
1. INSERT UsuarioTenant desde Usuarios activos
   - Cada usuario activo (Eliminado=0) genera 1 UsuarioTenant
   - EsTenantPrincipal = 1 (todos son mono-tenant hoy)
   - IdEstado = Usuario.IdEstado
   - Origen = 'MIGRATION'

2. Validar: COUNT(UsuarioTenant) = COUNT(Usuarios WHERE Eliminado=0)

3. UPDATE Accesos SET IdUsuarioTenant = UsuarioTenant.Id
   FROM UsuarioTenant WHERE UsuarioTenant.IdUsuario = Accesos.IdUsuario
   AND UsuarioTenant.IdTenant = Accesos.IdTenant

4. Validar: todos los Accesos con Usuario activo tienen IdUsuarioTenant NOT NULL

5. UPDATE UsuariosPermisos SET IdUsuarioTenant = UsuarioTenant.Id
   (misma lógica que Accesos)

6. Validar: todos los UP con Usuario activo tienen IdUsuarioTenant NOT NULL
```

### 4.3 Post-migración (Phase 4)

```
1. Verificar roles PLATFORM_* existen
2. Ejecutar SP_Permisos_Usuario_Efectivos (versión actual vs nueva)
3. Comparar resultados: deben ser idénticos
4. Si difieren → ABORT
```

### 4.4 U01 — Revisión MFA.IdTenant

**No migrar**. Solo validar que MFA.IdTenant sigue siendo semánticamente correcto como EXECUTION_CONTEXT. Documentar dependencias con Usuario/UsuarioTenant. Resolver antes de Contract.

---

## 5. A1.3 — Domain / EF

### 5.1 Nueva entidad: UsuarioTenant

```csharp
// PassPlat.Dominio/Entities/Core/UsuarioTenant.cs
public class UsuarioTenant
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public byte IdEstado { get; set; }
    public bool EsTenantPrincipal { get; set; }
    public DateTime FechaIngreso { get; set; }
    public DateTime? FechaFin { get; set; }
    public int? InvitadoPor { get; set; }
    public string Origen { get; set; } = "MANUAL";
    public DateTime? UltimoAcceso { get; set; }
    public bool Activo { get; set; } = true;

    public Usuario? Usuario { get; set; }
    public Tenant? Tenant { get; set; }

    public static UsuarioTenant Crear(int idUsuario, int idTenant, string origen)
    {
        return new UsuarioTenant
        {
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            IdEstado = 1, // Activo
            EsTenantPrincipal = false,
            FechaIngreso = DateTime.UtcNow,
            Origen = origen,
            Activo = true
        };
    }
}
```

### 5.2 Entity Usuario — cambios

```csharp
// ELIMINAR:
public int IdTenant { get; set; }              // ← quitar
public Tenant? Tenant { get; set; }            // ← quitar

// AGREGAR:
public ICollection<UsuarioTenant> UsuariosTenant { get; set; } = [];

// MODIFICAR factory method:
// Usuario.Crear() ya no recibe idTenant
public static Usuario Crear(int idEstado, string nomUsuario, string? email,
                             string nombre, string apellido)
```

### 5.3 Entity Acceso — cambios

```csharp
// CAMBIAR:
public int IdTenant { get; set; }              // ← reemplazar
public int? IdUsuarioTenant { get; set; }      // ← nuevo (NULL = Platform Scope)

// ELIMINAR:
public Tenant? Tenant { get; set; }            // ← quitar

// AGREGAR:
public UsuarioTenant? UsuarioTenant { get; set; }

// MODIFICAR factory method:
public static Acceso Crear(int idUsuario, int? idUsuarioTenant, int idApp, int idRol)
```

### 5.4 Entity UsuarioPermiso — cambios (si existe)

Mismos cambios que Acceso: `IdTenant` → `IdUsuarioTenant?`.

### 5.5 EF Configurations

```
PassPlat.Datos/Configurations/Core/
├── UsuarioTenantConfiguration.cs     ← NUEVO
├── UsuarioConfiguration.cs           ← MODIFICAR (quitar IdTenant)
├── AccesoConfiguration.cs            ← MODIFICAR (IdTenant → IdUsuarioTenant?)
└── UsuarioPermisoConfiguration.cs    ← MODIFICAR (ídem)
```

### 5.6 UsuarioTenantConfiguration.cs

```csharp
public class UsuarioTenantConfiguration : IEntityTypeConfiguration<UsuarioTenant>
{
    public void Configure(EntityTypeBuilder<UsuarioTenant> builder)
    {
        builder.ToTable("UsuarioTenant");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.IdUsuario).IsRequired();
        builder.Property(e => e.IdTenant).IsRequired();
        builder.Property(e => e.IdEstado).HasColumnType("tinyint").IsRequired();
        builder.Property(e => e.EsTenantPrincipal).HasDefaultValue(false);
        builder.Property(e => e.FechaIngreso).HasDefaultValueSql("sysdatetime()");
        builder.Property(e => e.FechaFin).HasColumnType("datetime2");
        builder.Property(e => e.Origen).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Activo).HasDefaultValue(true);

        builder.HasOne(e => e.Usuario)
               .WithMany(u => u.UsuariosTenant)
               .HasForeignKey(e => e.IdUsuario)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Tenant)
               .WithMany(t => t.UsuariosTenant)
               .HasForeignKey(e => e.IdTenant)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.IdUsuario, e.IdTenant })
               .IsUnique()
               .HasDatabaseName("UX_UsuarioTenant_Usuario_Tenant");

        builder.HasIndex(e => e.IdUsuario)
               .HasDatabaseName("IX_UsuarioTenant_Principal")
               .HasFilter("EsTenantPrincipal = 1");
    }
}
```

### 5.7 PassPlatDbContext — cambios

```csharp
// AGREGAR:
public DbSet<UsuarioTenant> UsuariosTenant { get; set; } = null!;

// Auto-discovery ya funciona con ApplyConfigurationsFromAssembly
// No requiere registro manual si la configuration está en el assembly correcto
```

---

## 6. A1.4 — Data / Repositories / SPs

### 6.1 Nuevos repositorios

```
PassPlat.Datos/Repositories/
├── UsuarioTenantRepository.cs        ← NUEVO (IUsuarioTenantRepository + impl)
└── ... (los 26 repos existentes KEEP)
```

### 6.2 IUsuarioTenantRepository

```csharp
public interface IUsuarioTenantRepository : IRepositoryAsync<UsuarioTenant>
{
    Task<Result<UsuarioTenant?>> ObtenerPrincipalAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<IReadOnlyList<UsuarioTenant>>> ObtenerActivosAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<IReadOnlyList<UsuarioTenant>>> ObtenerActivosConAccesoAsync(int idUsuario, int idApp, CancellationToken ct = default);
    Task<Result<bool>> ExisteMembresiaAsync(int idUsuario, int idTenant, CancellationToken ct = default);
}
```

### 6.3 Repositorios existentes — cambios

| Repositorio | Cambio |
|-------------|--------|
| AccesoRepository | `AsignarAccesoAsync` validar Platform Scope (IdUsuarioTenant=NULL → Rol.IdTenant=NULL). FK compuesta reemplaza trigger |
| UsuarioRepository | `CrearAsync` sin IdTenant en unicidad. Métodos que usaban `u.IdTenant` actualizados |
| AuthRepository | Login busca usuario sin filtrar por tenant |
| PasswordRepository | Sin cambios estructurales (opera sobre Usuario.Id, no IdTenant) |

### 6.4 SPs — migración

#### SP_Auth_Login (P0)

```
-- ACTUAL:
SELECT * FROM Usuarios WHERE NomUsuario = @NomUsuario AND IdTenant = @IdTenant

-- NUEVO:
SELECT * FROM Usuarios WHERE NomUsuario = @NomUsuario  -- global

-- Post-password: validar UsuarioTenant si @IdTenant IS NOT NULL
-- Retornar código: SUCCESS | INVALID_CREDENTIALS | NO_ACCESS | MULTIPLE_TENANTS
```

#### SP_Permisos_Usuario_Efectivos (P0)

```
-- ACTUAL:
WHERE Accesos.IdTenant = @IdTenant

-- NUEVO:
WHERE (Accesos.IdUsuarioTenant IS NULL AND Roles.IdTenant IS NULL)  -- Platform
   OR (Accesos.IdUsuarioTenant IN (
         SELECT Id FROM UsuarioTenant
         WHERE IdUsuario = @IdUsuario AND IdTenant = @IdTenant AND Activo = 1
       ))  -- Tenant
```

### 6.5 DI Registration (DatosDependencyInjection.cs)

```csharp
// NUEVOS:
services.AddScoped<IUsuarioTenantRepository, UsuarioTenantRepository>();

// MODIFICAR (interfaz ya existe, verificar implementación):
services.AddScoped<IAccesoRepository, AccesoRepository>();  // validación Platform Scope
```

---

## 7. A1.5 — Application / Services

### 7.1 AuthenticationContext

**Nuevo objeto de contexto** que reemplaza `idTenant ?? usuario.IdTenant`.

```csharp
// PassPlat.Aplicacion/Context/AuthenticationContext.cs
public class AuthenticationContext
{
    public int? IdTenant { get; set; }  // NULL = Platform Context
    
    public bool IsPlatformContext => IdTenant == null;
    public bool IsTenantContext => IdTenant != null;
}
```

**Resolución**:
- Endpoints autenticados: desde JWT (claim `id_tenant` o `id_tenant: null`)
- Background services: desde `UsuarioTenant.EsTenantPrincipal = 1`
- Login sin JWT: desde resolución explícita en AuthService

### 7.2 Nuevos servicios

```
PassPlat.Aplicacion/Services/BBDD/
├── UsuarioTenantService.cs           ← NUEVO (interface + impl)
└── ...
```

### 7.3 Servicios existentes — 16 puntos de falla

| Patrón | Ocurrencias | Reemplazo |
|--------|-------------|-----------|
| `idTenant ?? usuario.IdTenant` | 9 | `idTenant ?? authenticationContext.IdTenant` o `usuarioTenantRepository.ObtenerPrincipalAsync(usuario.Id)` |
| `usuario.IdTenant` directo | 7 | `usuarioTenantRepository.ObtenerPrincipalAsync(usuario.Id)` |

**Archivos afectados**:

| Archivo | Línea(s) | Patrón |
|---------|----------|--------|
| `AuthService.cs` | 437, 474 | `idTenant ?? usuario.IdTenant` |
| `AccesoService.cs` | varias | `idTenant ?? usuario.IdTenant` |
| `UsuarioService.cs` | varias | `usuario.IdTenant` |
| `UsuarioPermisoService.cs` | varias | `usuario.IdTenant` |
| `PasswordExpirationBackgroundService.cs` | 132 | `usuario.IdTenant` → UsuarioTenant principal |
| `TokenRestService.cs` | varias | `idTenant ?? usuario.IdTenant` |
| `EmailBackgroundService.cs` | varias | (KEEP — usa log.IdTenant) |
| Otros servicios | ~7 más | Revisar individualmente |

### 7.4 AuthService — cambios principales

```
LoginConTokenAsync:
  1. Buscar usuario por NomUsuario (global, sin IdTenant)
  2. Validar password
  3. Si @IdTenant IS NOT NULL → validar UsuarioTenant activo
  4. Si @IdTenant IS NULL:
     - Si 0 tenants activos → evaluar Platform Scope → JWT Platform o error
     - Si 1 tenant activo → usar automáticamente
     - Si >1 tenants activos → retornar 428 MULTIPLE_TENANTS
  5. Generar JWT con AuthenticationContext.IdTenant

CambiarPasswordAsync:
  - Usa Usuario.Id directamente (no depende de IdTenant)

EnviarCodigoMfaAsync:
  - Usa AuthenticationContext.IdTenant
```

### 7.5 428 MULTIPLE_TENANTS — lógica

```csharp
// En AuthService (NO en SP)
if (idTenant == null)
{
    var tenantsActivos = await _usuarioTenantRepo
        .ObtenerActivosConAccesoAsync(usuario.Id, idApp, ct);
    
    if (tenantsActivos.Count == 0)
    {
        // Evaluar Platform Scope
        if (tienePlatformScope)
            return JWT Platform;
        return SinAccesoApp;
    }
    
    if (tenantsActivos.Count == 1)
        idTenant = tenantsActivos[0].IdTenant;
    else
        return Result<AuthResult>.Failure("MULTIPLE_TENANTS",
            "El usuario tiene acceso a múltiples tenants. Seleccione uno.");
}
```

### 7.6 Background services — EsTenantPrincipal

```csharp
// En PasswordExpirationBackgroundService:
// ANTES: usuario.IdTenant
// DESPUÉS:
var utResult = await _usuarioTenantRepo.ObtenerPrincipalAsync(usuario.Id, ct);
if (utResult.IsFailure || utResult.Value == null) continue; // skip
var tenantId = utResult.Value.IdTenant;
```

### 7.7 U06 — Precedencia de estados

Debe resolverse ANTES de implementar AuthService.

Regla conceptual:
```
Authorization =
    Usuario.IdEstado        (identidad global — debe ser Activo)
    AND
    UsuarioTenant.IdEstado  (membresía — debe ser Activo)
    AND
    UsuarioTenant.Activo    (habilitación administrativa)
```

Implementar como función de validación en AuthenticationContext o AuthService.

---

## 8. A1.6 — WebAPI

### 8.1 Endpoints nuevos

| Método | Ruta | Propósito |
|--------|------|-----------|
| GET | `/api/usuariotenant/me` | Listar UsuarioTenant del usuario autenticado |
| POST | `/api/auth/context-switch` | Cambiar de tenant (emite nuevo JWT) |
| GET | `/api/auth/tenants` | Listar tenants disponibles para context-switch |

### 8.2 Endpoints modificados

| Endpoint | Cambio |
|----------|--------|
| `POST /api/auth/login` | Acepta `NomUsuario` (sin `Email`). Retorna 428 si multi-tenant. JWT sin `id_tenant` si Platform |
| `POST /api/usuarios` | `CrearUsuarioDto` sin `IdTenant`. Crea UsuarioTenant automáticamente |
| `POST /api/accesos` | Acepta `IdUsuarioTenant?`. Valida Platform Scope vs Rol.IdTenant=NULL |
| `DELETE /api/accesos/{id}` | Revoca Acceso (opera sobre IdUsuarioTenant) |

### 8.3 JWT — claims

```csharp
// Platform JWT (IdTenant = null):
Claims: { sub, name, email, id_tenant: null, permissions: [...] }

// Tenant JWT (IdTenant = T):
Claims: { sub, name, email, id_tenant: T, permissions: [...] }
```

### 8.4 Context-switch endpoint

```
POST /api/auth/context-switch
Body: { idTenant: int }
Response: Nuevo JWT con id_tenant = T (Platform + Tenant T)

Validaciones:
- UsuarioTenant activo existe para (usuario.Id, idTenant)
- Usuario tiene al menos un Acceso en ese tenant
- Usuario.IdEstado = Activo
- UsuarioTenant.IdEstado = Activo
- UsuarioTenant.Activo = true
```

---

## 9. A1.7 — Blazor

### 9.1 Cambios en UI existente

| Componente | Cambio |
|------------|--------|
| Login.razor | Campo NomUsuario en lugar de Email. Manejar 428 MULTIPLE_TENANTS (selector de tenant) |
| UsuarioDialog.razor | Sin campo IdTenant. Asignar tenant al crear usuario |
| AccesoDialog.razor | Seleccionar UsuarioTenant en lugar de IdTenant directo |
| Dashboard.razor | KPIs de UsuarioTenant (usuarios por tenant, membresías activas) |

### 9.2 Nuevos componentes

| Componente | Ruta | Propósito |
|------------|------|-----------|
| ContextSwitcher.razor | Compartido | Selector de tenant activo (visible cuando multi-tenant) |
| UsuarioTenantList.razor | `/usuariotenant` | Listar membresías del usuario |
| PlatformScopeIndicator.razor | Compartido | Badge "Platform" cuando JWT es Platform |

### 9.3 U04 — UX selección tenant

No implementar en A1.1. Deferido a fase posterior de A1. El login retorna 428 y el flujo UI se decide en A1.7+.

---

## 10. A1.8 — Testing

### 10.1 Tests unitarios

| Área | Tests mínimos |
|------|---------------|
| UsuarioTenant entity | Creación, propiedades, factory method |
| AuthenticationContext | Resolución Platform/Tenant, claims |
| UsuarioTenantService | CRUD, ObtenerPrincipal, ObtenerActivos |
| AuthService | Login global, 428 MULTIPLE_TENANTS, Platform JWT, Tenant JWT |
| AccesoService | Platform Scope validation, asignación, revocación |
| PasswordExpirationService | UsuarioTenant principal |

### 10.2 Tests de integración

| Área | Tests mínimos |
|------|---------------|
| SP_Auth_Login | Usuario global, validación UsuarioTenant |
| SP_Permisos_Usuario_Efectivos | Platform Scope + Tenant Scope, mismos resultados que versión actual |
| FK compuesta | Insert inválido rechazado, NULL permitido |
| UX_UsuarioTenant_Usuario_Tenant | Duplicado rechazado |

### 10.3 Tests E2E (Playwright)

| Test | Prioridad |
|------|-----------|
| Login con NomUsuario (sin Email) | P0 |
| Login con tenant específico | P0 |
| Login multi-tenant → 428 | P0 |
| Context-switch | P1 |
| Platform Scope: admin sin tenant | P0 |
| Platform Scope: permisos correctos | P0 |
| Usuario sin tenant → error | P0 |
| Background service con EsTenantPrincipal | P1 |

---

## 11. U01–U08: Work Items de A1

| ID | Título | Prioridad | Depende de | Bloquea | Asignado a |
|----|--------|-----------|------------|---------|------------|
| U01 | Revisar MFA.IdTenant: confirmar KEEP como EXECUTION_CONTEXT o migrar | P0 | A1.2 (datos) | Contract | Architecture |
| U02 | Implementar context-switch endpoint | P1 | A1.5 (AuthService) | — | Backend |
| U03 | Seed roles PLATFORM_ADMIN, PLATFORM_EDITOR, PLATFORM_SUPERVISOR, PLATFORM_CONSULTA | P0 | A1.1 (schema) | A1.2 (migración) | Data |
| U04 | UX selección tenant (login multi-tenant) | P1 | A1.7 (Blazor) | — | Frontend |
| U05 | AuthenticationContext en background jobs (PasswordExpiration, Email, IdenExtTokens) | P0 | A1.5 (AuthContext) | Contract | Backend |
| U06 | Definir precedencia Usuario.IdEstado / UsuarioTenant.IdEstado / UsuarioTenant.Activo | P0 | A0 (deferido) | A1.5 (AuthService) | Architecture |
| U07 | Revisar índices filtrados/descendentes | P1 | A1.1 (schema) | — | Data |
| U08 | Migrar datos de Grupos (trigger reescrito, datos no migran) | P1 | A1.1 (triggers) | — | Data |

---

## 12. Matriz de riesgos

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|-------------|---------|------------|
| Preflight detecta inconsistencias en datos legacy | Media | Alto | Abort migration. Resolver manualmente antes de continuar |
| SP_Permisos_Usuario_Efectivos retorna resultados diferentes | Media | Alto | Phase 4 (comparación). No avanzar hasta entender causa |
| Login post-migración falla para usuarios existentes | Baja | Crítico | Phase 8 smoke tests. Rollback inmediato |
| Background services interrumpidos por cambio de contexto | Baja | Medio | Phase 7 validación. EsTenantPrincipal ya implementado |
| MFA dependiente de IdTenant legacy | Media | Medio | U01 revisión pre-Contract |
| Performance de SP_Permisos con JOIN a UsuarioTenant | Baja | Medio | Índice UX_UsuarioTenant_Usuario_Tenant cubre el JOIN |
| Rollback post-cutover pierde transacciones | Baja (esperado) | Alto (aceptado) | Backup pre-cutover. Pérdida documentada como consecuencia aceptada |

---

## 13. Secuencia de commits recomendada

```
1. SQL: Schema UsuarioTenant + FKs + índices
2. SQL: Data migration scripts
3. SQL: SPs nuevos
4. SQL: Triggers (eliminar/reescribir)
5. Domain: Entidad UsuarioTenant
6. Domain: Modificar Usuario (quitar IdTenant)
7. Domain: Modificar Acceso (IdUsuarioTenant?)
8. EF: Configuraciones
9. EF: DbContext
10. Data: UsuarioTenantRepository
11. Data: Modificar AccesoRepository (Platform validation)
12. Data: Modificar AuthRepository (login global)
13. Data: DI registration
14. Application: AuthenticationContext
15. Application: UsuarioTenantService
16. Application: Modificar AuthService (428, Platform JWT)
17. Application: Modificar servicios con 16 puntos de falla
18. Application: Background services (EsTenantPrincipal)
19. Application: DI registration
20. WebAPI: Endpoints nuevos (context-switch, tenants)
21. WebAPI: Modificar endpoints existentes
22. Blazor: Login + context-switch + UI
23. Tests: Unitarios
24. Tests: Integración
25. Tests: E2E
```

---

## 14. Definition of Done (por fase)

| Fase | Criterio |
|------|----------|
| A1.1 | Scripts UP/DOWN probados. Preflight retorna 0 filas en BD target |
| A1.2 | Todos los datos migrados. Validaciones pasan. SP_Permisos idéntico |
| A1.3 | Entidades compilan. Configs EF reflejan schema exacto |
| A1.4 | Repositorios pasan tests. SPs retornan mismos resultados |
| A1.5 | AuthService maneja Platform/Tenant/428. 16 puntos de falla reemplazados |
| A1.6 | Endpoints documentados. Context-switch funcional |
| A1.7 | UI refleja modelo multi-tenant. Login funciona con NomUsuario |
| A1.8 | Tests unitarios + integración + E2E pasan. Cobertura > 80% en flujos críticos |

---

## 15. Gates de aprobación entre fases

```
A1.1 ──► Gate: Schema validado. Preflight OK.
              ↓
A1.2 ──► Gate: Datos migrados. SP_Permisos resultados idénticos.
              ↓
A1.3 ──► Gate: Entidades + configs compilan. Build 0 errores.
              ↓
A1.4 ──► Gate: Repos + SPs pasan tests de integración.
              ↓
A1.5 ──► Gate: AuthService certificado (Platform, Tenant, 428, context).
              ↓
A1.6 ──► Gate: Endpoints pasan tests de API.
              ↓
A1.7 ──► Gate: UI multi-tenant funcional. Playwright tests OK.
              ↓
A1.8 ──► Gate: Cobertura ≥ umbral. Build 0 errores, 0 warnings nuevos.
              ↓
        A1 COMPLETE → Ready for Phase 0 (migration)
```

---

## Appendix A: A0 → A1 traceability matrix (completa)

| A0 Documento | Decisión | A1 Tasks |
|-------------|----------|----------|
| ADR-001 | Usuario global sin IdTenant | A1.1-001 (schema), A1.3-001 (entity), A1.4-003 (repo) |
| ADR-002 | UsuarioTenant membership | A1.1-002 (table), A1.3-002 (entity), A1.4-001 (repo) |
| ADR-003 | Acceso Modelo A | A1.1-005 (FK), A1.3-003 (entity) |
| ADR-004 | Platform Scope | A1.5-004 (service), A1.6-002 (API) |
| ADR-005 | AuthenticationContext | A1.5-001 (class), A1.5-002 (DI) |
| A0.2 | 3 entidades migran, 26 KEEP | A1.1-001..006, A1.2-001..004 |
| A0.2 | 16 puntos de falla | A1.5-003 |
| A0.2 | MFA.IdTenant KEEP | U01 |
| A0.2 | IdenExt.IdTenant KEEP | A1.2-005 |
| A0.3 | 17 casos access matrix | A1.5-005, A1.6-001..003 |
| A0.3 | JWT Platform vs Tenant | A1.6-003 |
| A0.4 | Expand→Migrate→Deploy→Contract | A1.2 (orden) |
| A0.4 | Preflight 10 queries | A1.2-001 |
| A0.4 | Compatibility Matrix | A1.1-017 (scripts DOWN), A1.2 (rollback) |
| A0.4 | Legacy READ-ONLY | A1.3-004 (config), A1.5-007 (validación) |
| A0.4 | Phase 5 ≠ Phase 8.6 | A1.1 (desarrollo) vs A1.2 (PROMOTE) |
| A0.4 | Contract Approval Gate | Documentado en A0.4 Phase 8 |
| A0.4 | U01 blocker pre-Contract | U01 |
| A0.5 | Approval Gate firmado | A1 completo |

---

## Appendix B: Proyectos y archivos afectados (inventario completo)

| Archivo | Cambio | Fase A1 |
|---------|--------|---------|
| `BBDD/PASSWORDS.sql` | Schema UsuarioTenant + alter tables | A1.1 |
| `BBDD/PASSWORDS SP.sql` | SPs nuevos | A1.1 |
| `BBDD/Migrations/A1/*.sql` | 14+ scripts UP/DOWN | A1.1 |
| `PassPlat.Dominio/Entities/Core/UsuarioTenant.cs` | NUEVO | A1.3 |
| `PassPlat.Dominio/Entities/Core/Usuario.cs` | Quitar IdTenant, Tenant nav | A1.3 |
| `PassPlat.Dominio/Entities/Core/Acceso.cs` | IdTenant → IdUsuarioTenant? | A1.3 |
| `PassPlat.Dominio/GlobalUsings.cs` | Agregar UsuarioTenant si necesario | A1.3 |
| `PassPlat.Datos/PassPlatDbContext.cs` | DbSet UsuariosTenant | A1.3 |
| `PassPlat.Datos/Configurations/Core/UsuarioTenantConfiguration.cs` | NUEVO | A1.3 |
| `PassPlat.Datos/Configurations/Core/UsuarioConfiguration.cs` | Quitar IdTenant config | A1.3 |
| `PassPlat.Datos/Configurations/Core/AccesoConfiguration.cs` | IdTenant → IdUsuarioTenant? | A1.3 |
| `PassPlat.Datos/Repositories/UsuarioTenantRepository.cs` | NUEVO | A1.4 |
| `PassPlat.Datos/Repositories/AccesoRepository.cs` | Platform validation | A1.4 |
| `PassPlat.Datos/Repositories/UsuarioRepository.cs` | Login global | A1.4 |
| `PassPlat.Datos/DatosDependencyInjection.cs` | Registrar UsuarioTenantRepo | A1.4 |
| `PassPlat.Aplicacion/Context/AuthenticationContext.cs` | NUEVO | A1.5 |
| `PassPlat.Aplicacion/Services/BBDD/UsuarioTenantService.cs` | NUEVO | A1.5 |
| `PassPlat.Aplicacion/Services/SPro/AuthService.cs` | 428, Platform JWT | A1.5 |
| `PassPlat.Aplicacion/Services/BBDD/AccesoService.cs` | Platform validation | A1.5 |
| `PassPlat.Aplicacion/Services/Security/PasswordExpirationBackgroundService.cs` | EsTenantPrincipal | A1.5 |
| `PassPlat.Aplicacion/AplicacionDependencyInjection.cs` | Registrar servicios | A1.5 |
| `PassPlat.WebAPI/Controllers/AuthController.cs` | Login global, context-switch | A1.6 |
| `PassPlat.WebAPI/Controllers/AccesosController.cs` | Platform Scope | A1.6 |
| `PassPlat.WebAPI/Controllers/UsuariosController.cs` | Sin IdTenant | A1.6 |
| `PassPlat.Web/Pages/Login.razor` | NomUsuario, 428, selector tenant | A1.7 |
| `PassPlat.Web/Pages/Usuarios/*.razor` | Sin IdTenant en forms | A1.7 |
| `PassPlat.Web/Shared/ContextSwitcher.razor` | NUEVO | A1.7 |
| `tests/faseA1-multi-tenant.spec.ts` | NUEVO — Playwright | A1.8 |
