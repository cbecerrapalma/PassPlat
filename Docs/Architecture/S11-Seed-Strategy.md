# S11 — Seed Strategy

## Estado: COMPLETADO ✅

**Fecha**: 2026-08-01
**Contexto**: FASE 5.6 + FASE 5.7 de Sprint S11 (Login Seed).
**Baseline**: FASE 17.5 S9 RELEASE CANDIDATE, A1.8 24/24, A1.9 17/17, xUnit 66/66, Google 39/39.

## Objetivo

Estrategia definitiva para el pipeline SEED de PassPlat (Multi-Tenant), alineado con el refactor A1 (UsuarioTenant / Accesos.IdUsuarioTenant). Los seeds deben ser idempotentes, reproducibles y certificables contra la BBDD real.

## Pipeline Seed

```
01_PRECHECK → SEED_Plataforma → SEED_Tenant (0..N) → 02_VERIFY_SEED → 02_FIXUP_SEED → 03_VALIDATE → 04_RESET_Runtime
```

### Regla de ejecución crítica

**CWD = `D:\CODIGOS\BBDD\Seed`**. Los orquestadores (`SEED_Plataforma.sql`, `SEED_Tenant.sql`) usan includes `:r` con rutas relativas. Ejecutar con sqlcmd desde otro directorio rompe los includes.

```
sqlcmd -S . -d PassPlat -U sa -P inicio123 -f 65001 -I -i SEED_Plataforma.sql
sqlcmd -S . -d PassPlat -U sa -P inicio123 -f 65001 -I -i SEED_Tenant.sql   # plantilla reutilizable
```

## Decisiones

| # | Decisión | Detalle |
|---|----------|---------|
| D1 | Catalogos (14) `IF NOT EXISTS` | Nunca UPDATE sobre catálogos. |
| D2 | Configuracion Global + Tenant `MERGE` | `WHEN MATCHED THEN UPDATE` + `WHEN NOT MATCHED THEN INSERT` (43/43). |
| D3 | Roles por tenant | Nunca compartir. Cuatro por tenant (ADMIN, EDITOR, SUPERVISOR, CONSULTA). |
| D4 | OAuth | ProvIden = catálogo global. ConfProvIden = por tenant. |
| D5 | SEED_Plataforma = plataforma pura | Nunca datos de clientes. |
| D6 | SEED_Tenant = plantilla reutilizable | Variables T-SQL `DECLARE` (`@TenantCodigo`, `@TenantNombre`). |
| D7 | Hash Admin@123 en seeds | `$argon2id$v=19$m=131072,t=4,p=8$g0mWVEDTVZyHiXD+K1ZNCMUmzkzJU0LeK/zV62kruQ4=$E41z6gnr6RmiskoyNix7z6v+4gxyYdQY8ce8f3y/HhO2wZ92UA/gpn6E+vwGa8F+jAXteQr+ze++lDTFv3lz6w==$pv1` — el seed NO re-hashea usuarios que ya existen (preserva hash existente). |
| D8 | UsuarioTenant + Accesos.IdUsuarioTenant | Los seeds crean la membresía y el acceso con el IdUsuarioTenant resuelto por subquery (G1/G2). |
| D9 | Seed nunca modifica esquema | No crea/elimina/alter tablas, columnas, índices, FK, SP, migraciones. |
| D10 | Transacciones | `SET XACT_ABORT ON`. Sin bloques GO dentro de transacciones. |

## VERIFY vs VALIDATE

| Script | Rol | Resultado |
|--------|-----|-----------|
| `02_VERIFY_SEED.sql` | 34 checks estructurales + A1.4 MULTITENANCY (M1-M6) | **34/34 PASS** |
| `03_VALIDATE.sql` | Certificación funcional por secciones (solo valida) | **41/43** |

### Sección A1.4 MULTITENANCY en VERIFY (M1-M6)

| Check | Qué valida |
|-------|-----------|
| M1 | `UsuarioTenant.IdUsuarioTenant` → FK válida a `Usuarios.Id` |
| M2 | `Accesos.IdUsuarioTenant` → FK válida a `UsuarioTenant.Id` |
| M3 | Accesos tenant-scope (`IdTenant > 1`) deben tener `IdUsuarioTenant` resuelto |
| M4 | Coherencia membresía ↔ acceso (mismo usuario + mismo tenant) |
| M5 | Usuarios activos deben tener membresía activa |
| M6 | Sin caracteres corruptos BIN2 en tablas multi-tenant |

### Casos en VALIDATE (MULTITENANCY A-F)

| Caso | Severidad | Regla |
|------|-----------|-------|
| CRITICAL | `UsuarioTenant` existe | Tabla presente |
| CRITICAL | `Accesos.IdUsuarioTenant` existe | Columna presente |
| CaseA | FAIL | Acceso tenant-scope sin `IdUsuarioTenant` |
| CaseB | FAIL | `IdUsuarioTenant` de acceso apunta a membresía de otro tenant |
| CaseC | FAIL | Orphans: `UsuarioTenant` con usuario inexistente |
| Duplicados | FAIL | Múltiples membresías activas mismo usuario+tenant |
| RoleTenant | WARNING | Rol con `IdTenant <> IdTenant` del acceso |

## Estado de certificación (2026-08-01)

- VERIFY: **34/34 PASS**
- VALIDATE: **41/43** — 2 fallos restantes son **polución runtime de suites previas**, no defectos de seed:
  - **UserAccess (81)**: 73 usuarios `test_*` + 8 `hybrid_*` creados por suites Playwright anteriores sin accesos.
  - **RoleTenant (1)**: acceso Id=14 (fixture `test_inactive_memb`) con rol ABARROTES_CONSULTA (Id=12, IdTenant=3) sobre acceso `IdTenant=1`.

## Idempotencia

- 3ª ejecución confirmada: 30 tablas con recuentos 100% idénticos.
- El seed preserva hashes existentes (D7) — los fixes de hash corrupto se aplican manualmente vía UPDATE.

## Relevant Files

| File | Rol |
|------|-----|
| `D:\CODIGOS\BBDD\Seed\01_PRECHECK.sql` | Pre-validación bloqueante |
| `D:\CODIGOS\BBDD\Seed\SEED_Plataforma.sql` | Orquestador plataforma (`:r`) |
| `D:\CODIGOS\BBDD\Seed\SEED_Tenant.sql` | Plantilla reutilizable (`:r`) |
| `D:\CODIGOS\BBDD\Seed\02_VERIFY_SEED.sql` | 34 checks + A1.4 MULTITENANCY |
| `D:\CODIGOS\BBDD\Seed\02_FIXUP_SEED.sql` | Fixups post-seed |
| `D:\CODIGOS\BBDD\Seed\03_VALIDATE.sql` | Certificación funcional + MULTITENANCY |
| `D:\CODIGOS\BBDD\Seed\04_RESET_Runtime.sql` | Reset operacional |
| `D:\CODIGOS\PassPlat\A1.8_test_fixtures.sql` | Fixtures A1.8 (hash Admin@123) |
