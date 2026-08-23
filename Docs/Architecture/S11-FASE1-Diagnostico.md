# S11 — FASE 1: Inventario + Auditoría (Diagnóstico)

- **Fecha**: 2026-08-01
- **Sprint**: S11 — SEED/DDL Reproducible + Certificación End-to-End del Login
- **Estado**: Diagnóstico completo. Pendiente: FASE 5 (decisión de arquitectura) e implementación.

---

## 1. Árbol de directorios de BBDD

```
D:\CODIGOS\BBDD\
├── PASSWORDS.sql                    # DDL canónico (94915 bytes) — INCLUYE UsuarioTenant + Accesos.IdUsuarioTenant + FK compuesta
├── PASSWORDS SP.sql                 # Stored Procedures (71341 bytes) — SP_Usuario_Crear crea UsuarioTenant
├── docs\
│   ├── ANALISIS_SEED_DATA.md
│   └── INFORME_EMAIL_SUBSYSTEM.md
├── Migrations\A1\
│   ├── 001_Preflight.sql            # Pre-validación A1.1 (9/9 PASS en ejecución histórica)
│   ├── 002_Create_UsuarioTenant.sql # CREATE UsuarioTenant (idempotente)
│   ├── 003_Alter_Accesos.sql        # ADD Accesos.IdUsuarioTenant int NULL
│   ├── 004_Alter_UsuariosPermisos.sql
│   ├── 005_Indexes.sql              # 7 nuevos + 6 legacy preservados (U07)
│   ├── 006_ForeignKeys.sql          # FK_Accesos_UsuarioTenant compuesta
│   ├── 007_Triggers.sql             # 1 DROP + 3 REWRITE + 1 MODIFY (U08)
│   ├── 008_StoredProcedures.sql     # SPs A1.1 (sin cambios; documenta A1.4)
│   ├── 009_Postflight.sql           # Post-validación A1.1
│   ├── 010_A1.2_Preflight.sql       # One-shot guard + incoherencias pre-migración
│   ├── 011_A1.2_Migration.sql       # INSERT UsuarioTenant + UPDATE Accesos (ONE-SHOT)
│   ├── 012_A1.2_Postflight.sql      # Validaciones post-migración
│   ├── 013_A1.4_StoredProcedures.sql # SP_Usuario_Crear → INSERT UsuarioTenant; SP_Auth_LoginExterno → resolve IdUsuarioTenant
│   ├── A1.1_PASS.md                 # Bitácora de ejecución A1.1 (2026-07-28)
│   └── DOWN\                        # Rollbacks (Drop_UsuarioTenant, Restore_*)
├── Prototipos\Emails\  Permisos\
└── Seed\
    ├── 01_PRECHECK.sql              # Entorno: DB, versión, collation, 59 tablas requeridas
    ├── 02_VERIFY_SEED.sql           # Verificación de datos post-seed (SIN chequeo UsuarioTenant)
    ├── 02_FIXUP_SEED.sql            # Fixups entre VERIFY y VALIDATE
    ├── 03_VALIDATE.sql              # Certificación funcional (THROW 50001 si falla)
    ├── 04_RESET_Runtime.sql         # Reset operacional
    ├── SEED_Plataforma.sql          # Orquestador: 6 catálogos + 7 configuraciones
    ├── SEED_Tenant.sql              # Plantilla reutilizable (:setvar)
    ├── _run_PLATFORM.sql            # Copia de SEED_Tenant para PLATFORM
    ├── _run_ABARROTES.sql
    ├── _run_VESTUARIO.sql
    ├── _unicode_check.sql
    ├── Catalogo\   (01_Estados, 02_Tipos, 03_Resultados, 04_TiposModulo, 05_ProvIden, 06_Apps)
    ├── Configuracion\ (01_Modulos, 02_Permisos, 03_RolesGlobales, 04_Infraestructura, 05_OAuth, 06_EmailConfig, 07_Usuarios)
    ├── Tenant\      (01_DatosGenerales, 02_RolesTenant, 03_ConfProvIden, 04_EmailTenant, 05_AdminUsuario, 06_Accesos)
    ├── Validacion\  (security_matrix.sql, test_utf8.sql)
    └── logs\
```

---

## 2. Matriz: Modelo EF vs DDL vs Seed

| Capa | UsuarioTenant | Accesos.IdUsuarioTenant | FK compuesta | Seed UsuarioTenant | Seed Accesos.IdUsuarioTenant |
|------|---------------|------------------------|--------------|--------------------|------------------------------|
| **Modelo EF (C#)** | ✅ `UsuarioTenant` entidad | ✅ `IdUsuarioTenant` | n/a | — | — |
| **DDL canónico** | ✅ `PASSWORDS.sql:1188` | ✅ `PASSWORDS.sql:2728` | ✅ `:2810` | — | — |
| **Migraciones A1** | ✅ `002_Create` | ✅ `003_Alter` | ✅ `006_FK` | ✅ vía `011` (ONE-SHOT) | ✅ vía `011` |
| **SP** | ✅ `SP_Usuario_Crear` (PASSWORDS SP.sql:101, 013:125) | ✅ `SP_Auth_LoginExterno` (013:292) | — | — | — |
| **Seeds** | ❌ **NINGUNO** | ❌ **NINGUNO** | — | ❌ | ❌ |

**Conclusiones de la matriz:**
- El DDL **sí existe** de forma formal (PASSWORDS.sql + Migrations\A1). La afirmación de S10 ("no hay DDL formal") era **incorrecta/desactualizada** — se buscó solo en `Migrations\` del repo PassPlat, no en `D:\CODIGOS\BBDD\`.
- `SP_Usuario_Crear` **sí crea** UsuarioTenant automáticamente (A1.4).
- **PERO los seeds NO usan SP_Usuario_Crear**: insertan Usuarios/Accesos directamente.
- **Resultado**: una DB recién seedeada tiene Usuarios + Accesos pero **0 filas en UsuarioTenant**.

---

## 3. SEED GAP confirmado (causa raíz)

Los scripts de seed que crean usuarios/accesos:

| Script | Método | ¿Crea UsuarioTenant? |
|--------|--------|----------------------|
| `Seed\Configuracion\07_Usuarios.sql` | `INSERT INTO Usuarios` (sistema Id=1, platform_admin Id=2) + `INSERT Accesos` | ❌ NO |
| `Seed\Tenant\05_AdminUsuario.sql` | `INSERT INTO Usuarios` (admin tenant) | ❌ NO |
| `Seed\Tenant\06_Accesos.sql` | `INSERT INTO Accesos` (IdUsuarioTenant no se asigna) | ❌ NO |

**Consecuencia directa**: el `INSERT INTO UsuarioTenant(IdUsuario=1, IdTenant=1)` manual que motivó S11 es la prueba de que el seed no es reproducible: en una DB nueva el login falla (SinAccesoApp / sin membresía) hasta que se cree UsuarioTenant a mano.

**Nota**: la migración ONE-SHOT `011` poblaría UsuarioTenant desde Accesos+Roles, pero:
1. Es ONE-SHOT — si UsuarioTenant tiene datos, hace `THROW`.
2. No está encadenada al pipeline de seeds (SEED_Plataforma/SEED_Tenant no la invocan).
3. Requiere que primero existan Usuarios + Accesos + Roles con `IdTenant IS NOT NULL`.

---

## 4. Estado actual de la DB viva

| Tabla | Filas | Comentario |
|-------|-------|------------|
| UsuarioTenant | 61 | Poblada por A1.2 (011) + fixtures A1.8, NO por seeds |
| Apps | 3 | Id=1 PASSPLAT (seed) + 2 test apps Activa=0 |
| Tenants | 3 | Id=1 PLATFORM (EsSistema=1), 3 ABARROTES, 4 VESTUARIO |
| Usuarios | 93 | sistema(1), platform_admin(2), admin_platform(6), admin_abarrotes(4), admin_vestuario(5), + test_* |
| Accesos | 13 | 8 con IdUsuarioTenant asignado, 5 platform-scope (NULL) |
| Roles | 16 | 4 globales (IdTenant NULL) + 4×3 tenant |
| ProvIden | 7 | GOOGLE, MICROSOFT, GITHUB, APPLE, LINKEDIN, FACEBOOK, INSTAGRAM |
| ConfProvIden | 21 | 7 × 3 tenants |

**Hashes actuales en DB:**
- `sistema` (Id=1): `oP53RT...` ✅ (hash real del seed)
- `platform_admin` (Id=2): `g0mWVED...` = hash de `Admin@123` ✅ (CORREGIDO a mano — el seed original pone placeholder)
- `admin_abarrotes` (Id=4): `$argon2id$...$hashhashhash...` ❌ **PLACEHOLDER NO FUNCIONAL**
- `admin_vestuario` (Id=5): `$argon2id$...$hashhashhash...` ❌ **PLACEHOLDER NO FUNCIONAL**
- `admin_platform` (Id=6): `c/TEEqNCj...` = hash de `Admin@123` ✅
- test_* (fixtures A1.8): `g0mWVED...` = `Admin@123` ✅

---

## 5. Estado OAuth (ProvIden / ConfProvIden)

- **ProvIden**: 7 proveedores, IDs fijos 1–7 con `IDENTITY_INSERT` (`05_ProvIden.sql`). Catálogo global controlado (regla 24/25 AGENTS.md).
- **ConfProvIden**: MERGE por tenant (`03_ConfProvIden.sql`); **Callback base = `https://localhost:5001/api/auth/externo/{CODIGO}/callback`**.
- **⚠️ Discrepancia de puerto**: la API en Development corre en `http://localhost:5259` (HTTP) y `https://localhost:5001` (HTTPS). El Callback apunta a 5001 (HTTPS), que coincide con el perfil `https` de la WebAPI. **Consistente con FASE 17** (API HTTPS en 5001). No es bug, pero debe documentarse en S11.
- **Test fixture**: `A1.8_test_fixtures.sql` usa hash `CHIjGP9...` para `B7$k9mX!pW2@nR` — **DESACTUALIZADO**. La DB y los tests certificados usan `Admin@123` (`g0mWVED...`). En una máquina nueva, aplicar el fixtures tal cual crearía usuarios con password `B7$k9mX!pW2@nR` que **no coincidiría** con los tests (`PWD='Admin@123'`). **Bug de reproducibilidad confirmado.**

---

## 6. Dependencias manuales detectadas (no reproducibles)

| # | Dependencia | Evidencia |
|---|-------------|-----------|
| 1 | `INSERT INTO UsuarioTenant(IdUsuario=1, IdTenant=1)` manual | El gap motivador de S11 |
| 2 | Hash de `platform_admin` corregido a mano | Seed original inserta placeholder `hashhashhash...` |
| 3 | `A1.8_test_fixtures.sql` con hash antiguo (`B7$k9mX`) | DB real usa `Admin@123` |
| 4 | Hash placeholder en `05_AdminUsuario.sql` | `$argon2id$...$YWRTdHJpbmdTYWx0MjU2$hashhashhash...` — no es un Argon2id válido |
| 5 | `admin_abarrotes`/`admin_vestuario` sin password funcional en DB viva | No pueden hacer login con `Admin@123` |

---

## 7. Gaps identificados (clasificación)

| ID | Gap | Capa | Tipo | Repro |
|----|-----|------|------|-------|
| G1 | Seeds no crean `UsuarioTenant` | Seed | 🔴 PRODUCTION BUG | Sí |
| G2 | Seed inserta hash Argon2id placeholder no funcional para admins | Seed | 🔴 PRODUCTION BUG | Sí |
| G3 | `A1.8_test_fixtures.sql` hash desactualizado vs tests/DB | Test data | 🟡 TEST BUG | Sí |
| G4 | `02_VERIFY_SEED.sql`/`03_VALIDATE.sql` no validan UsuarioTenant | Seed | 🟡 TEST BUG (certificación) | Sí |
| G5 | Pipeline seeds no encadena migración A1.2 (011) | Docs/Seed | 🟡 CONTRACT | Parcial |
| G6 | Callback OAuth 5001 vs API Dev 5259 — requiere documentación | OAuth | 🟢 DOCUMENTACIÓN | No |
| G7 | `PASSWORDS SP.sql` (canónico) `SP_Auth_LoginExterno` NO asigna `IdUsuarioTenant` en INSERT Accesos; la migración A1.4 (013) sí | SP | 🟡 TEST/PROD (fuente desincronizada) | Sí (solo si se re-ejecuta el SP canónico) |

---

## 8. Riesgos

1. **Login falla en DB nueva**: sin UsuarioTenant, `LoginAsync` devuelve SinAccesoApp/sin membresía para usuarios de tenant.
2. **Admins sin password**: placeholder hash → imposible autenticarse como `admin_abarrotes`/`admin_vestuario` tras seed limpio.
3. **Fixtures stale**: reconstrucción desde cero + `A1.8_test_fixtures.sql` → tests A1.8/A1.9 fallarían (password mismatch).
4. **One-shot 011**: si un fix future de seed crea UsuarioTenant y luego se ejecuta 011 → THROW. El orden DDL→Seed→011 debe fijarse por contrato.
5. **Modificar seeds sin romper idempotencia**: los MERGE/IF NOT EXISTS actuales son la convención certificada en S7.9 (43 MERGE). Cualquier cambio debe mantener idempotencia.

---

## 9. Propuesta de arquitectura de seeds (para FASE 5)

### Opción A (recomendada): Seeds auto-suficientes con UsuarioTenant explícito
- En `Configuracion\07_Usuarios.sql` y `Tenant\05_AdminUsuario.sql`/`06_Accesos.sql`, tras crear el usuario y su Acceso:
  - `INSERT INTO UsuarioTenant (IdUsuario, IdTenant, IdEstado, Activo) SELECT ... WHERE NOT EXISTS (...)` — idempotente, lookup por `Codigo`/`NomUsuario`.
  - Asignar `Accesos.IdUsuarioTenant` con `UPDATE ... JOIN UsuarioTenant`.
- Reemplazar placeholder hash por hash Argon2id real de `Admin@123` (el mismo patrón `g0mWVED...` ya presente en DB).
- `SP_Usuario_Crear` ya crea UsuarioTenant — pero los seeds usan INSERT directo (por simplicidad y para sistema/IDENTITY_INSERT). Se documenta que el API SIEMPRE usa el SP.

### Opción B: Encadenar A1.2 (011) al pipeline
- Invocar `011_A1.2_Migration.sql` al final de `SEED_Plataforma`/`SEED_Tenant`.
- ❌ Riesgo: ONE-SHOT y diseñada para migración de datos legacy, no para seed. Requiere que existan Accesos tenant-scope con Roles.IdTenant — que los seeds sí crean. Válido como *backup*, no como camino principal.

### Opción C: Seeds usan SP_Usuario_Crear
- Sustituir INSERTs directos por `EXEC SP_Usuario_Crear`.
- ❌ Rompe `IDENTITY_INSERT` de sistema (Id=1) y la idempotencia basada en IF NOT EXISTS; requiere manejo de result-set.

**Recomendación FASE 5**: **Opción A** (explícita, idempotente, sin dependencia one-shot). Backfill opcional vía 011 para bases legacy.

---

## 10. Archivos a modificar (lista exacta)

| Archivo | Acción |
|---------|--------|
| `D:\CODIGOS\BBDD\Seed\Configuracion\07_Usuarios.sql` | + INSERT UsuarioTenant (sistema, platform_admin) + UPDATE Accesos.IdUsuarioTenant + hash Admin@123 |
| `D:\CODIGOS\BBDD\Seed\Tenant\05_AdminUsuario.sql` | + INSERT UsuarioTenant (admin tenant) + hash Argon2id real |
| `D:\CODIGOS\BBDD\Seed\Tenant\06_Accesos.sql` | + UPDATE Accesos.IdUsuarioTenant desde UsuarioTenant |
| `D:\CODIGOS\BBDD\Seed\02_VERIFY_SEED.sql` | + checks UsuarioTenant (cada usuario con Acceso tenant-scope debe tener membresía) |
| `D:\CODIGOS\BBDD\Seed\03_VALIDATE.sql` | + checks funcionales UsuarioTenant ↔ Accesos ↔ login |
| `D:\CODIGOS\PassPlat\A1.8_test_fixtures.sql` | Actualizar hash a `Admin@123` (`g0mWVED...`) + comentario |
| `D:\CODIGOS\PassPlat\Tests\s11-login-seed-certification.spec.ts` | **NUEVO** — tests S11 usando `tests/api-config.ts` |
| `Docs\Architecture\S11-Seed-Strategy.md` | **NUEVO** — estrategia de seeds (decisión FASE 5) |
| `Docs\Architecture\S11-Login-Seed-Certification.md` | **NUEVO** — certificación final |

**NO se tocan**: `PASSWORDS.sql`, `PASSWORDS SP.sql`, `Migrations\A1\*` (ya correctos), capa C#.

---

## Verificación de la hipótesis

- [x] **G1 confirmado**: seeds no crean UsuarioTenant (grep en Seed\* → 0 resultados).
- [x] **G2 confirmado**: placeholder `$argon2id$...$hashhashhash...` en 05_AdminUsuario.sql:28 y 07_Usuarios.sql:72.
- [x] **G3 confirmado**: fixtures `CHIjGP9...` (B7$k9mX) vs DB/tests `g0mWVED...` (Admin@123).
- [x] **DDL existe**: PASSWORDS.sql:1188 y Migrations\A1\002 — S10 estaba incompleto.
- [x] **SP correcto**: SP_Usuario_Crear crea UsuarioTenant (pero seeds lo evitan).
- [x] **DB viva**: 61 UsuarioTenant, 8/13 Accesos mapeados, 2 admins con placeholder roto.
- [x] **G7 confirmado (FASE 2)**: `PASSWORDS SP.sql` SP_Auth_LoginExterno INSERT Accesos sin `IdUsuarioTenant` (:1305/:1340/:1373) vs A1.4 013 con asignación (:292/:331/:370).

> **Nota FASE 2**: ver contrato completo del login (flujos local/platform/switch/OAuth, claims JWT, ramas de permisos, respuesta a las 15 preguntas del brief) en `Docs/Architecture/S11-Login-Access-Model.md`.
