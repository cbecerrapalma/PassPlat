# Changelog

## [1.0.0] — 2026-05-21

### Added
- Complete PassPlat.Aplicacion layer:
  - DTOs for all 29 tables (Catalogos, Contexto, Core)
  - AutoMapper profiles for all entity→DTO mappings
  - FluentValidation validators for all creation DTOs
  - 28 service implementations across 8 files
  - Service interfaces in ICustomServices.cs
  - DI registration in AplicacionDependencyInjection.cs
- Missing repository registrations in DatosDependencyInjection.cs
- Updated AGENTS.md with comprehensive architecture documentation
- Created Docs/ folder with structured documentation:
  - Architecture overview and project map
  - Database reference (PASSWORDS.sql)
  - Domain conventions
  - Data layer patterns (EF config, repositories, UoW)
  - Application layer patterns (DTOs, AutoMapper, validators, services)
  - Framework references (CBP.Data.Synchronous, CBP.Results)

### Fixed
- CatalogServices.cs: Proper Result-pattern handling for `GetById` calls
- All build warnings resolved (0 errors, 0 warnings)
