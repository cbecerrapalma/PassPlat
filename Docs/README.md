# PassPlat Documentation

## Getting Started

- [Architecture Overview](Architecture/overview.md) — Solution structure, dependency flow, design principles
- [Project Map](Architecture/project-map.md) — Full file tree per layer

## Database

- [PASSWORDS Schema](Database/PASSWORDS.md) — 29 tables, 8 SPs, 3 triggers, computed columns, indexes

## Domain Layer

- [Conventions](Domain/conventions.md) — Entity naming, PK types, enums (E prefix), factory methods

## Data Layer

- [EF Configuration](Datos/ef-configuration.md) — Property mapping, defaults, indexes, constraints
- [Repositories](Datos/repositories.md) — Base methods, custom repos, SP execution, DI registration
- [Unit of Work](Datos/unit-of-work.md) — UoW methods, commit-from-consumer rule, transaction usage

## Application Layer

- [DTOs](Aplicacion/dtos.md) — Read/create DTO patterns, naming conventions
- [AutoMapper](Aplicacion/automapper.md) — Entity→DTO mapping, conditional updates, nav properties
- [Validators](Aplicacion/validators.md) — FluentValidation, catalog/core validator tables
- [Services](Aplicacion/services.md) — UoW vs SP patterns, 28-service catalog, DI registration

## Framework

- [CBP.Data.Synchronous](Framework/CBP.Data.Synchronous.md) — RepositorySync, UnitOfWorkSync, RawQueryRepositorySync
- [CBP.Results](Framework/CBP.Results.md) — Result<T>, Error, factory methods, null handling

## Reference

- [Changelog](Operations/changelog.md) — Version history
- `AGENTS.md` (project root) — Primary agent instructions with conventions and pitfalls
