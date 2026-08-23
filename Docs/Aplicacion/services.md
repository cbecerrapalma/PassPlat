# Service Pattern

**Location**: `PassPlat.Aplicacion\Services\`
**Interfaces**: `PassPlat.Aplicacion\Interfaces\ICustomServices.cs`

## Architecture

Two service patterns coexist:

### Pattern 1: New Catalog/Contexto Services (Use UoW)

```csharp
public class TenantService : ITenantService
{
    private readonly TenantRepository _repo;
    private readonly IUnitOfWorkSync<PassPlatDbContext> _uow;
    private readonly IMapper _mapper;

    public TenantService(TenantRepository repo, IUnitOfWorkSync<PassPlatDbContext> uow, IMapper mapper)
    {
        _repo = repo; _uow = uow; _mapper = mapper;
    }

    // Read operations: direct repo
    public async Task<Result<TenantDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var r = _repo.GetById(id);
        var entity = r.IsSuccess ? r.Value : null;
        return await Task.FromResult(Result<TenantDto?>.Success(_mapper.Map<TenantDto?>(entity), allowNull: true));
    }

    // Write operations: repo + SaveChanges
    public async Task<Result<TenantDto>> CrearAsync(CrearTenantDto dto, CancellationToken ct = default)
    {
        var entity = Tenant.Crear(dto.Codigo, dto.Nombre);
        _repo.Add(entity);
        _uow.SaveChanges();
        return await Task.FromResult(Result<TenantDto>.Success(_mapper.Map<TenantDto>(entity)));
    }
}
```

### Pattern 2: Existing Core/SP Services (No UoW)

These remain as-is to avoid breaking changes:

```csharp
public class AuthService : IAuthService
{
    private readonly AuthRepository _authRepo;

    public AuthService(AuthRepository authRepo) => _authRepo = authRepo;

    public async Task<Result<LoginResult>> LoginAsync(...) => ...
}

public class UsuarioService : IUsuarioService
{
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IMapper _mapper;

    public UsuarioService(IUsuarioRepository usuarioRepo, IMapper mapper) { ... }
}
```

## Key Rules

1. **All data access through repositories** — never direct DbContext/DbSet in services
2. **Result pattern** — all methods return `Result<T>` or `Result`
3. **Handle GetById properly** — it returns `Result<T>`, not `T?`
4. **Commit from consumer** — SaveChanges is called from WebAPI layer, not inside services
5. **Async wrappers** — all service methods are async, wrapping sync repo calls with `Task.FromResult`
6. **Result propagation from repositories** — every call to a repository method that returns `Result<T>` MUST check `.IsFailure` before accessing `.Value`:
   ```csharp
   // CORRECTO
   var repoResult = await _repo.ObtenerPorCodigoAsync(codigo, ct);
   if (repoResult.IsFailure) return Result<TenantDto?>.Failure(repoResult.Error!);
   var entity = repoResult.Value;
   var dto = entity != null ? Mapper.Map<TenantDto>(entity) : null;
   return Result<TenantDto?>.Success(dto, allowNull: true);
   
   // INCORRECTO — traga el error, lo convierte en Success(null)
   var entity = await _repo.ObtenerPorCodigoAsync(codigo, ct);
   var dto = entity != null ? Mapper.Map<TenantDto>(entity) : null;
   return Result<TenantDto?>.Success(dto, allowNull: true);
   ```

## Service List (28 services)

### SP-based (no UoW)
- `AuthService` — Login
- `PasswordService` — Cambiar password
- `SesionService` — Crear/revocar sesiones
- `TokenRestService` — Generar/validar tokens
- `MFAService` — Validar MFA
- `MaintenanceService` — Purge datos antiguos

### Core (no UoW)
- `UsuarioService` — CRUD usuarios
- `AccesoService` — Asignar/revocar accesos
- `PoliticaPwdService` — Obtener políticas
- `BloqueoService` — Crear/desactivar bloqueos
- `HistorialPwdService` — Consultar historial
- `AuditoriaPwdService` — Consultar/registrar auditoría
- `NotificacionService` — CRUD notificaciones
- `DispConfiableService` — Gestionar dispositivos confiables
- `IntentoAccesoService` — Consultar/registrar intentos

### Catalog (with UoW)
- `TenantService` — CRUD tenants
- `AppService` — CRUD apps
- `RolService` — CRUD roles
- `ConfigTenantService` — Configuración de tenant
- `DominioTenantService` — Dominios de tenant

### Catalog (read-only, with UoW)
- `EstadoUsrService`, `ResultadoAccesoService`, `TipoMFAService`, `EstadoMFAService`, `TipoDispService`, `TipoCambioPwdService`, `TipoBloqueoService`, `TipoAuditoriaService`

### Contexto (with UoW, ObtenerOCrear pattern)
- `DispService` — Obtener o crear dispositivo
- `DireccionIPService` — Obtener o crear IP
- `UserAgentService` — Obtener o crear user agent

### Core extra (with UoW)
- `RolPoliticaPwdService` — CRUD asignación política-rol

## DI Registration

Manual in `AplicacionDependencyInjection.cs`:

```csharp
services.AddServiceAsync<ITenantService, TenantService>();
services.AddServiceAsync<IAppService, AppService>();
// ... one entry per service
```

The `AddServiceAsync<TInterface, TImplementation>()` extension method registers the service with scoped lifetime.
