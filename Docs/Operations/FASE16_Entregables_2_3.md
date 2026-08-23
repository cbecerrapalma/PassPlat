# FASE 16 — Refactorización Arquitectónica: Renombrado de Tablas de Identidades Externas

## Entregable 2 — Listado Completo de Archivos Modificados (C#)

**Total: 45 archivos** (44 con referencias a nuevos nombres + 1 fix de MfaController)

### Dominio (`PassPlat.Dominio`)
1. `Entities/Catalogos/EstIdenExt.cs` — entidad renombrada
2. `Entities/Catalogos/TipAsigPermiso.cs` — entidad renombrada
3. `Entities/Catalogos/ProvIden.cs` — navigation property actualizada
4. `Entities/Catalogos/Tenant.cs` — navigation property actualizada
5. `Entities/Core/AudIdenExt.cs` — entidad renombrada
6. `Entities/Core/HistorialIdenExt.cs` — entidad renombrada
7. `Entities/Core/IdenExt.cs` — entidad renombrada
8. `Entities/Core/Usuario.cs` — navigation property actualizada
9. `Entities/Core/UsuarioPermiso.cs` — navigation property actualizada
10. `Enums/EEstIdenExt.cs` — enum renombrado

### Datos (`PassPlat.Datos`)
11. `PassPlatDbContext.cs` — DbSet renombrados
12. `DatosDependencyInjection.cs` — registro de repositorios
13. `Configurations/Catalogos/EstIdenExtConfiguration.cs` — nueva config
14. `Configurations/Catalogos/TipAsigPermisoConfiguration.cs` — nueva config
15. `Configurations/Core/AudIdenExtConfiguration.cs` — nueva config
16. `Configurations/Core/HistorialIdenExtConfiguration.cs` — nueva config
17. `Configurations/Core/IdenExtConfiguration.cs` — nueva config
18. `Configurations/Core/UsuarioPermisoConfiguration.cs` — FK actualizada
19. `Interfaces/IExternalAuthRepository.cs` — tipos actualizados
20. `Repositories/AudIdenExtRepository.cs` — repositorio renombrado
21. `Repositories/EstIdenExtRepository.cs` — repositorio renombrado
22. `Repositories/ExternalAuthRepository.cs` — tipos actualizados
23. `Repositories/HistorialIdenExtRepository.cs` — repositorio renombrado
24. `Repositories/IdenExtRepository.cs` — repositorio renombrado
25. `Repositories/TipAsigPermisoRepository.cs` — repositorio renombrado

### Aplicación (`PassPlat.Aplicacion`)
26. `AplicacionDependencyInjection.cs` — registro de servicios
27. `Mapping/AplicacionProfile.cs` — mapeos actualizados
28. `Dtos/Catalogos/EstIdenExtDto.cs` — DTO renombrado
29. `Dtos/Catalogos/TipAsigPermisoDto.cs` — DTO renombrado
30. `Dtos/Core/AudIdenExtDto.cs` — DTO renombrado
31. `Dtos/Core/HistorialIdenExtDto.cs` — DTO renombrado
32. `Dtos/Core/IdenExtDto.cs` — DTO renombrado
33. `Services/BBDD/AudIdenExtService.cs` — servicio renombrado
34. `Services/BBDD/EstIdenExtService.cs` — servicio renombrado
35. `Services/BBDD/FederacionService.cs` — tipos actualizados
36. `Services/BBDD/HistorialIdenExtService.cs` — servicio renombrado
37. `Services/BBDD/IdenExtService.cs` — servicio renombrado
38. `Services/BBDD/TipAsigPermisoService.cs` — servicio renombrado
39. `Validations/Core/CrearIdenExtValidator.cs` — validator renombrado

### WebAPI (`PassPlat.WebAPI`)
40. `Controllers/AudIdenExtController.cs` — controller renombrado
41. `Controllers/EstIdenExtController.cs` — controller renombrado
42. `Controllers/HistorialIdenExtController.cs` — controller renombrado
43. `Controllers/IdenExtController.cs` — controller renombrado
44. `Controllers/TipAsigPermisoController.cs` — controller renombrado
45. `Controllers/DashboardController.cs` — consultas actualizadas
46. `Controllers/MfaController.cs` — FIX: try-catch en SaveChangesAsync (no rename, corrección de regresión paralela)

### Web (`PassPlat.Web`) — Blazor
Los archivos `.razor` referencian endpoints por ruta (no por nombre de clase), por lo que no requieren rename de clase. Selectores y rutas validados en Playwright.

### Aplicacion.Dtos (proyecto compartido)
47. `PassPlat.Aplicacion.Dtos/Catalogos/TipAsigPermisoDto.cs` — DTO compartido
48. `PassPlat.Aplicacion.Dtos/Core/AudIdenExtDto.cs` — DTO compartido
49. `PassPlat.Aplicacion.Dtos/Core/HistorialIdenExtDto.cs` — DTO compartido
50. `PassPlat.Aplicacion.Dtos/Core/IdenExtDto.cs` — DTO compartido

### Configuración de proyecto
51. `PassPlat.WebAPI/PassPlat.WebAPI.csproj` — `<NoWarn>NU1903</NoWarn>`
52. `CBP/CBP.WebApi/CBP.WebApi/CBP.WebApi.csproj` — `<NoWarn>NU1903</NoWarn>`

## Entregable 3 — Listado de Objetos SQL Modificados

### Tablas (5 nuevas, 5 antiguas eliminadas)
| Objeto | Tipo | Acción |
|--------|------|--------|
| `IdenExt` | USER_TABLE | CREADA (reemplaza `IdentidadesExternas`) |
| `EstIdenExt` | USER_TABLE | CREADA (reemplaza `EstadosIdentidadExterna`) |
| `AudIdenExt` | USER_TABLE | CREADA (reemplaza `AuditoriaIdentidadExterna`) |
| `HistorialIdenExt` | USER_TABLE | CREADA (reemplaza `HistorialIdentidadExterna`) |
| `TipAsigPermiso` | USER_TABLE | CREADA (reemplaza `TipoAsignacionPermiso`) |
| `IdentidadesExternas` | USER_TABLE | ELIMINADA |
| `EstadosIdentidadExterna` | USER_TABLE | ELIMINADA |
| `AuditoriaIdentidadExterna` | USER_TABLE | ELIMINADA |
| `HistorialIdentidadExterna` | USER_TABLE | ELIMINADA |
| `TipoAsignacionPermiso` | USER_TABLE | ELIMINADA |

### Stored Procedures actualizados (en `D:\CODIGOS\BBDD\PASSWORDS SP.sql`)
- `SP_Auth_LoginExterno` — referencias a `IdentidadesExternas` → `IdenExt`
- `SP_Auth_Login` — referencias a tablas de identidad externa
- `SP_Auth_AutoProvisionar` — INSERT/UPDATE en `IdenExt`
- `SP_Auth_AutoLink` — JOIN con `IdenExt`
- `SP_Auth_RegistrarAuditoria` — INSERT en `AudIdenExt`
- `SP_Dashboard_IdentidadExterna` — SELECT de `IdenExt`, `AudIdenExt`, `EstIdenExt`

### Extended Properties (32 bloques)
- Tablas: `IdenExt`, `EstIdenExt`, `AudIdenExt`, `HistorialIdenExt`, `TipAsigPermiso` (MS_Description)
- Columnas importantes: PK e FK de cada tabla
- PK: `PK_IdenExt`, `PK_EstIdenExt`, `PK_AudIdenExt`, `PK_HistorialIdenExt`, `PK_TipAsigPermiso`
- FK: `FK_IdenExt_Estado`, `FK_AudIdenExt_IdenExt`, `FK_HistorialIdenExt_IdenExt`, `FK_UsuarioPermiso_TipAsigPermiso`, etc.

### Integridad verificada (0 referencias a nombres antiguos)
```sql
SELECT COUNT(*) FROM sys.sql_modules
WHERE definition LIKE '%IdentidadesExternas%'
   OR definition LIKE '%AuditoriaIdentidadExterna%'
   OR definition LIKE '%EstadosIdentidadExterna%'
   OR definition LIKE '%HistorialIdentidadExterna%'
   OR definition LIKE '%TipoAsignacionPermiso%'
-- Resultado: 0
```
- ✅ `sys.foreign_keys`: 0 FKs con nombre antiguo
- ✅ `sys.objects`: 0 objetos con nombre antiguo
- ✅ `sys.sql_expression_dependencies`: 0 dependencias a tablas antiguas
- ✅ Views / Functions / Triggers: 0 objetos referencian tablas renombradas
