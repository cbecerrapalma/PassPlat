# CBP.Data.Synchronous Reference

**Location**: `D:\CODIGOS\CBP\CBP.Data\CBP.Data.Synchronous\`

## Overview

Provides synchronous implementations of repository and unit of work patterns for EF Core. Designed for WinForms/WPF scenarios where `async` usage is impractical, but also supports async overrides.

## Key Classes

### `RepositorySync<TEntity>`
- Base class for all repositories
- Partial class split across 4 files:
  - `RepositorySync.cs` — Constructor, Query(), CreateError()
  - `RepositorySync.Read.cs` — GetById, GetAll, Exists, FirstOrDefault, Where, Any, Count
  - `RepositorySync.Write.cs` — Add, Update, Remove, RemoveById, AddRange, UpdateRange, RemoveRange
  - `RepositorySync.Pagination.cs` — GetPaged, GetSeekPaged

### `UnitOfWorkSync<TDbContext>`
- Implements `IUnitOfWorkSync<TDbContext>`
- Manages transactions, SaveChanges, repository access
- Thread-safe lazy initialization of RawQuery

### `RawQueryRepositorySync`
- Internal class implementing `IRawQueryRepositorySyncSqlServer`
- Both RawParameter and SqlParameter[] interfaces converge here
- Supports: QuerySP, ExecuteSP, ScalarSP, QuerySPMultiple (2-3 result sets)
- Auto-maps result sets to DTOs via reflection
- Auto-captures RETURN VALUE and OUTPUT parameters

## Error Handling

All base methods wrap exceptions in `Result<T>.Failure()` with:
- Error code: `"DATABASE_ERROR"`
- Operation name (e.g., `"GetById"`)
- Entity type name

## Thread Safety

- `UnitOfWorkSync` uses `Interlocked.CompareExchange` for thread-safe lazy `RawQuery` initialization
- `ThreadSafeGuard` utility for dispose detection
