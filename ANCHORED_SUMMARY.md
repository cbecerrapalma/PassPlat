# Anchored Summary

## Goal
Refactorizar la arquitectura SEED en un modelo de tres niveles (Catálogo → Plataforma → Tenant), completamente desacoplado, reproducible y certificable, con trazabilidad extremo a extremo (Controller→Tabla). Reemplazar el monolito `SEED_DATA.sql` por 22+ scripts modulares.

## Constraints & Preferences
- **SEED_DATA_LEGACY.sql**: SÓLO LECTURA. Backup histórico.
- **Usuario sistema (Id=1)**: INTOCABLE — preservar siempre.
- **Nunca depender de Id** para lógica funcional; usar siempre Código.
- **Catálogos**: `IF NOT EXISTS` — nunca UPDATE (14 tablas, solo correcciones excepcionales).
- **Configuración Core**: `MERGE` por código (módulos, permisos, roles globales, templates).
- **Configuración Tenant**: `MERGE` por código + IF NOT EXISTS (ConfProvIden, cuentas email, usuarios).
- **Roles por tenant**: NUNCA compartir. Identificados por `{CODIGO}_ROL`, nunca por Id.
- **OAuth**: ProvIden = catálogo global (7 providers). ConfProvIden = por tenant.
- **04_RESET_Runtime**: Sólo elimina OPERACIONAL + AUDITORIA según @RetentionDays. Nunca CATALOGO ni CONFIGURACION.
- **03_VALIDATE**: Niveles CRITICAL/FAIL/WARNING/INFO. Pipeline falla solo en CRITICAL+FAIL.
- **FASE 18 (generador automático)**: Postergada. No implementar hasta seed estabilizado.

## Progress
### Done
- **FASE -2**: `Docs/Dependency_Graph.md` — grafo de dependencias con 3 niveles (N0:14, N1:9, N2:32), orden seguro para DELETE y RESET, tablas que nunca se eliminan
- **FASE -1**: `Docs/Modelo_Tablas.md` — 55 tablas clasificadas en 6 categorías (CATALOGO 14, CONFIGURACION 18, OPERACIONAL 12, AUDITORIA 6, TEMPORAL 1, CACHE 0) con 17 columnas: Tabla, Dominio, Categoría, DependeTenant, DependeApp, Identity, RowVersion, EsSistema, ResetRuntime, TieneSP, TieneRepository, TieneController, TieneBlazor, TieneSeed, SeedOrigen, OrdenSeed, OrdenReset, OrdenValidate, PuedeRegenerarse, PuedeLimpiarse
- **FASE 0/0.5**: Inventario + matriz trazabilidad + gap analysis (refinados con categorías 2026-07-22)
- **FASE 0.5 extendida**: Coverage matrix template con cadena completa Controller→Policy→Permiso→Rol→Menú→Página→ViewModel→Repository→SP→Tabla
- **FASE 1 — SEED_Plataforma.sql**: Orquestador con 2 fases (Catálogos 6, Configuración 7), verificación 12 checks
- **FASE 1 — Catalogo/**: 6 scripts (Estados, Tipos, Resultados, TiposModulo, ProvIden, Apps) — IF NOT EXISTS
- **FASE 1 — Configuracion/**: 7 scripts con headers Core/Tenant/Mixta (Modulos, Permisos, RolesGlobales, Infraestructura, OAuth, EmailConfig, Usuarios) — MERGE por código
- **FASE 2 — SEED_Tenant.sql**: Plantilla con sección CONFIGURACIÓN + línea "NO MODIFICAR BAJO ESTA LÍNEA", 6 sub-scripts (DatosGenerales, RolesTenant, ConfProvIden, EmailTenant, AdminUsuario, Accesos)
- **FASE 3 — 03_VALIDATE.sql**: 3 niveles (5 CRITICAL, 10 FAIL, 2 WARNING) + INFO diagnóstico. Salida estructurada: `CRITICAL PASS Usuario_Sistema`, `FAIL PASS FK_Integrity`, etc. Resumen con conteos.
- **FASE 4 — 04_RESET_Runtime.sql**: Renombrado de 04_RESET_OPERACIONAL. @RetentionDays parametrizado (0=total, 30/365=retención). Limpia OPERACIONAL + AUDITORIA, preserva CATALOGO + CONFIGURACION + usuarios admin.
- **FASE 6**: `Docs/OAuth_Data_Model.md` — modelo actual + hoja de ruta hacia 6 tablas nuevas (OAuthScopes, OAuthClaims, OAuthClaimMappings, OAuthEndpoints, OAuthWellKnown)
- **FASE 7**: `Docs/Architecture_Certification.md` — plantilla de certificación con 5 capas (Modelo Datos, Seguridad, Seed, OAuth, Operaciones), estado actual (✅/🔲) para cada requisito
- **Roles por código auditado**: Todos los scripts ya usan `SELECT Id FROM dbo.Roles WHERE Codigo = '...'` — 0 referencias a IdRol numérico
- **Build**: `dotnet build PassPlat.slnx` — 0 errores, 0 warnings

### Pendiente (FASE 5)
- Ejecutar `SEED_Plataforma.sql` contra BD PassPlat
- Ejecutar `03_VALIDATE.sql` — apuntar a 0 CRITICAL + 0 FAIL
- Cerrar WARNING de controllers sin policy (8 pendientes) + proveedores OAuth (4/7 en BD actual)
- Probar `SEED_Tenant.sql` con tenant de prueba
- Sellar `SEED_DATA_LEGACY.sql` como backup

### Postergado
- **FASE 18** (generador automático de permisos): Postergado hasta seed estabilizado

## Estructura final de Seed
```
/BBDD/Seed/
  SEED_Plataforma.sql      ← Orquestador plataforma
  SEED_Tenant.sql           ← Plantilla tenant (CONFIGURACIÓN + NO MODIFICAR)
  03_VALIDATE.sql           ← Certificación (CRITICAL/FAIL/WARNING/INFO)
  04_RESET_Runtime.sql      ← Limpieza (@RetentionDays)
  Catalogo/                 ← 6 scripts (IF NOT EXISTS)
  Configuracion/            ← 7 scripts (MERGE, headers Core/Tenant/Mixta)
  Tenant/                   ← 6 scripts (roles por código)
  Demo/                     ← .gitkeep (opcional)
  Validacion/               ← .gitkeep (opcional)
```

## Relevant Files (new/modified this session)
- `Docs/Dependency_Graph.md` — FASE -2 (nuevo)
- `Docs/Modelo_Tablas.md` — FASE -1 (nuevo, reemplaza SEED_DATA_Inventory.md expandido)
- `Docs/OAuth_Data_Model.md` — FASE 6 (nuevo)
- `Docs/Architecture_Certification.md` — FASE 7 (nuevo)
- `BBDD/Seed/SEED_Tenant.sql` — Refactorizado (CONFIGURACIÓN + NO MODIFICAR)
- `BBDD/Seed/03_VALIDATE.sql` — Refactorizado (niveles CRITICAL/FAIL/WARNING/INFO)
- `BBDD/Seed/04_RESET_Runtime.sql` — Renombrado + @RetentionDays
- `BBDD/Seed/Configuracion/01-07_*.sql` — Headers Core/Tenant/Mixta
