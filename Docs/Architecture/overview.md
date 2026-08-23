# Architecture Overview

## Solution Structure

```
PassPlat.slnx
├── PassPlat.Dominio/          # Domain layer
├── PassPlat.Datos/            # Data layer
├── PassPlat.Aplicacion/       # Application layer
└── (external) → D:\CODIGOS\CBP\   # Shared framework
```

## Layer Responsibilities

| Layer | Responsibility |
|-------|---------------|
| **PassPlat.Dominio** | Pure domain. Entities, enums, constants. No external dependencies. |
| **PassPlat.Datos** | Data access. EF Core configurations, repositories, SP execution. |
| **PassPlat.Aplicacion** | DTOs, AutoMapper profiles, FluentValidation, service orchestration. |

## Dependency Flow

```
PassPlat.Aplicacion
    ↓
PassPlat.Datos
    ↓
PassPlat.Dominio
    ↓
CBP.Security.Password    CBP.Results    CBP.Events
```

## Data Model Source of Truth

**File**: `D:\CODIGOS\BBDD\PASSWORDS.sql`

All EF Core configurations, entity definitions, and repository implementations must be validated against this SQL schema. The database design dictates:

- Table names (exact match, including `IPs`, `Disp`)
- Column types (`tinyint` → `byte`, `bigint` → `long`, `uniqueidentifier` → `Guid`)
- Constraints (PK, FK, unique, check, default)
- Indexes (filtered, descending, covering)
- Computed columns (PERSISTED)
- Stored procedures (8 SPs for transactional logic)
- Triggers (3 for auto-update behavior)

## Design Principles

1. **Data model first**: PASSWORDS.sql is the authoritative schema
2. **Repository-only data access**: All data operations go through repositories, never direct DbContext/DbSet in services
3. **Result pattern**: All operations return `Result<T>` from CBP.Results
4. **Unit of Work**: SaveChanges is called from consumer (WebAPI), not from services or repositories
5. **No cascading deletes**: All FK relationships use `OnDelete(DeleteBehavior.Restrict)`
6. **Spanish naming**: All entities, properties, and code use Spanish terminology
