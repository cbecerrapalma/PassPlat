# Unit of Work Pattern

**Interface**: `IUnitOfWorkSync<PassPlatDbContext>` from `CBP.Data.Synchronous`
**Implementation**: `UnitOfWorkSync<TDbContext>` from `CBP.Data.Synchronous`

## Purpose

The Unit of Work pattern:
1. Tracks changes through EF Core's `ChangeTracker`
2. Provides a single `SaveChanges()` point
3. Manages database transactions
4. Provides access to repositories and raw query execution

## Methods

| Method | Description |
|--------|-------------|
| `SaveChanges()` | Persists all pending changes to database |
| `SaveEntities()` | Calls `SaveChanges()` and returns bool |
| `BeginTransaction()` | Starts a new database transaction |
| `CommitTransaction()` | Commits + disposes current transaction |
| `RollbackTransaction()` | Rolls back + disposes current transaction |
| `ExecuteInTransaction(Action)` | Executes operation within a transaction (auto commit/rollback) |
| `ExecuteInTransaction<T>(Func<T>)` | Same but with return value |
| `GetRepository<TEntity>()` | Gets standard `RepositorySync<TEntity>` |
| `GetCustomRepository<TRepository>()` | Gets custom repository by interface |
| `RawQuery` | `IRawQueryRepositorySync` for SP execution |
| `HasChanges` | Whether ChangeTracker has pending changes |
| `RejectChanges()` | Discards all pending changes |
| `Detach<TEntity>(entity)` | Detaches entity from context |

## Critical Rule: Commit from Consumer

**`SaveChanges()` is called ONLY from the consumer** (WebAPI/web/other), NOT from repositories or services.

### WRONG — Service calls SaveChanges:
```csharp
public Result<TenantDto> Crear(CrearTenantDto dto)
{
    var entity = Tenant.Crear(dto.Codigo, dto.Nombre);
    _repo.Add(entity);
    _uow.SaveChanges();  // WRONG — commit happens here
    return _mapper.Map<TenantDto>(entity);
}
```

### RIGHT — Consumer calls SaveChanges:
```csharp
// In service:
public Result<TenantDto> Crear(CrearTenantDto dto)
{
    var entity = Tenant.Crear(dto.Codigo, dto.Nombre);
    _repo.Add(entity);
    return _mapper.Map<TenantDto>(entity);
    // NO SaveChanges here!
}

// In WebAPI consumer:
var result = _service.Crear(dto);
if (result.IsSuccess)
    _uow.SaveChanges();  // Commit from consumer
```

## Exception: SP-based Services

Services that execute stored procedures (Auth, Password, Sesion, TokenRest, MFA, Maintenance) do NOT use UoW. The SPs handle their own transactions internally (BEGIN TRAN / COMMIT / ROLLBACK inside the SP).

## Transaction Usage

For operations spanning multiple repositories:

```csharp
_uow.BeginTransaction();
try
{
    _repo1.Add(entity1);
    _repo2.Update(entity2);
    _uow.SaveChanges();  // Save before commit
    _uow.CommitTransaction();
}
catch
{
    _uow.RollbackTransaction();
    throw;
}
```

Or use the convenience method:
```csharp
_uow.ExecuteInTransaction(() =>
{
    _repo1.Add(entity1);
    _repo2.Update(entity2);
});
```
