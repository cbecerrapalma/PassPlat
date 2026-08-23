# Repository Patterns

## Base Repository

**Project**: `CBP.Data.Synchronous`
**Base class**: `RepositorySync<TEntity>` (inherits from `CBP.Data.Synchronous`)
**Generic arg**: Single — `RepositorySync<Usuario>` (NOT `RepositorySync<Usuario, DbContext>`)

## Available Base Methods

All return `Result<T>` from `CBP.Results`:

### Read Operations

| Method | Return Type | Description |
|--------|-------------|-------------|
| `Query()` | `IQueryable<TEntity>` | Base queryable for LINQ composition |
| `GetById<TId>(TId id)` | `Result<TEntity>` | Find by primary key |
| `GetAll(limit, asNoTracking)` | `Result<IReadOnlyList<TEntity>>` | All records (default limit 1000, max 10000) |
| `Exists<TId>(TId id)` | `Result<bool>` | Check if PK exists |
| `FirstOrDefault(predicate)` | `Result<TEntity?>` | First match or null |
| `Where(predicate)` | `Result<IReadOnlyList<TEntity>>` | Filtered list |
| `Any(predicate)` | `Result<bool>` | Any match |
| `Count()` | `Result<int>` | Total count |
| `Count(predicate)` | `Result<int>` | Count with predicate |
| `GetPaged(options)` | `Result<IPagedResult<TEntity>>` | Paginated with sorting |
| `GetSeekPaged(keySelector, cursor, pageSize)` | `Result<ISeekPagedResult<TEntity, TKey>>` | Keyset pagination |

### Write Operations

| Method | Return Type | Description |
|--------|-------------|-------------|
| `Add(entity)` | `Result<TEntity>` | Add new entity |
| `Update(entity)` | `Result<TEntity>` | Mark as modified |
| `Remove(entity)` | `Result` | Delete entity |
| `RemoveById(key)` | `Result` | Delete by PK |
| `AddRange(entities)` | `Result<IReadOnlyList<TEntity>>` | Bulk add |
| `UpdateRange(entities)` | `Result<IReadOnlyList<TEntity>>` | Bulk update |
| `RemoveRange(entities)` | `Result` | Bulk delete |

## Critical: GetById Returns Result

```csharp
// WRONG — GetById returns Result<T>, not T
var entity = _repo.GetById(id);

// RIGHT
var result = _repo.GetById(id);
if (result.IsFailure) return Result<T>.Failure(result.Error!);
var entity = result.Value;
```

## Custom Repository Pattern

Interface + implementation in the **same file**. Todos los métodos públicos asíncronos deben retornar `Task<Result<T>>` — nunca tipos raw (`T?`, `List<T>`, `IReadOnlyList<T>`, `int`, `bool`, tuplas). Las operaciones async se envuelven en try-catch con código `"DB_ERROR"`:

```csharp
// PassPlat.Datos.Repositories namespace
using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;

public interface ITenantRepository : IRepositoryAsync<Tenant>
{
    Task<Result<Tenant?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Tenant>>> ObtenerActivosAsync(CancellationToken ct = default);
}

public class TenantRepository : RepositoryAsync<Tenant>, ITenantRepository
{
    public TenantRepository(PassPlatDbContext context) : base(context) { }

    public async Task<Result<Tenant?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(t => t.Codigo == codigo, ct);
            return Result<Tenant?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Tenant?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Tenant>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(t => t.Activo).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Tenant>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Tenant>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
```

## DI Registration

Manual in `DatosDependencyInjection.cs`:

```csharp
// Interface-based repositories
services.AddScoped<ITenantRepository, TenantRepository>();
services.AddScoped<TenantRepository>(); // concrete for services that inject directly

// SP repositories (concrete only)
services.AddScoped<AuthRepository>();
services.AddScoped<PasswordRepository>();

// Catalog repositories without interfaces (concrete only)
services.AddScoped<EstadoUsrRepository>();
services.AddScoped<TipoMFARepository>();
```

## Stored Procedure Execution

**Entry point**: `IUnitOfWorkSync<PassPlatDbContext>.RawQuery` (type `IRawQueryRepositorySync`)
**NOT** `IRawQueryRepositorySyncSqlServer` for RawParameter methods.

### RawParameter API

```csharp
RawParameter.Int("@Name", value)
RawParameter.NVarChar("@Name", value, size)  // nvarchar(size)
RawParameter.Date("@Name", dateTime)          // DbType.DateTime2
RawParameter.BigInt("@Name", value)
RawParameter.Bit("@Name", value)
RawParameter.Decimal("@Name", value)
RawParameter.In("@Name", value, DbType, size)
RawParameter.Out("@Name", DbType, size)       // Output parameter
```

### SP Query Pattern

```csharp
public class AuthRepository
{
    private readonly IRawQueryRepositorySync _rawQuery;

    public AuthRepository(IUnitOfWorkSync<PassPlatDbContext> uow)
    {
        _rawQuery = uow.RawQuery;
    }

    public Result<LoginResult> Login(...)
    {
        var parameters = new List<RawParameter>
        {
            RawParameter.NVarChar("@NomUsuario", nomUsuario, 100),
            RawParameter.Int("@IdApp", idApp),
            RawParameter.NVarChar("@HashPwdCalculado", hashPwdCalculado, 512)
        };

        var result = _rawQuery.QuerySP<LoginResult>("SP_Auth_Login", parameters);
        if (!result.IsSuccess)
            return Result<LoginResult>.Failure(result.Error!);

        var dto = result.Value.FirstOrDefault();
        return dto != null
            ? Result<LoginResult>.Success(dto)
            : Result<LoginResult>.Failure("SP_NO_RESULT", "Sin resultado");
    }
}
```

### Raw SqlParameter Overload

For TVPs, UDTs, or precise decimal specifications:
```csharp
_rawQuery.QuerySPRaw<MyDto>("SP_Name", new SqlParameter("@Param", value));
_rawQuery.ExecuteSPRaw("SP_Name", new SqlParameter("@Param", value));
```
