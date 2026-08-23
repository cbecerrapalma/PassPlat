# S27 — Dependency Debt Discovery (Sprint DISCOVERY/WAIT)

**Estado**: DISCOVERY COMPLETO — inventario de deudas abiertas de PassPlat sobre baseline CBP.
**Alcance**: SOLO análisis. Prohibida cualquier implementación durante este sprint.
**Fecha**: 2026-08-14
**Solución**: `D:\CODIGOS\PassPlat\PassPlat.slnx` (32 proyectos, 848 documentos, 0 errores/warnings).
**Precedentes**: S24 CLOSED · S25.0/25.1 COMPLETE · S25.2 CLOSED · S26.0 COMPLETE · S26.1 APPROVED · S26.2 CLOSED · F9 RESUELTO.

---

## 0. Metodología

Este sprint es **descubrimiento read-only**. No se ejecutó build (sin cambios de código). Herramientas:
1. **sequential-thinking**: plan en 3 pasos + 5 hipótesis de trabajo (H1 placeholders UoW, H2 framework CBP, H3 S19-Fx, H4 concurrencia Acceso, H5 hallazgos nuevos).
2. **supermemory**: recuperación de deudas registradas (F9, F3/S27, GetRepository/GetCustomRepository, Detach).
3. **sharplens**: health check (32 proyectos, 848 docs, 0 errores), grafo de dependencias sin ciclos, diagnostics, grep dirigido.
4. **Context7**: NO requerido (información local).

Plantilla de cada deuda: `ID | Categoría | Capa | Evidencia | Dependencias | Impacto | Prioridad | Acción sugerida | Estado`.

---

## 1. Inventario de deudas abiertas

### DEUDA-001 — DATA-007: Concurrencia en `AccesoRepository.AsignarAccesoAsync` (P0)

| Campo | Detalle |
|---|---|
| Categoría | Concurrencia / Datos |
| Capa | Datos (`AccesoRepository`) |
| Evidencia | `AccesoRepository.cs:92-125` (FirstOrDefaultAsync + Update sin token de concurrencia); `AccesoConfiguration.cs:11` declara `HasTrigger("TR_Accesos_ValidarTenant")` **eliminado** en A1 (migración `A1.1_PASS.md:40`, `007_Triggers.sql:19`, verificado en BD viva: 0 triggers en `sys.triggers`); `FASE15_Certificacion_14Etapas.md:441-464`; índice único `(IdUsuario, IdApp, IdRol)` en `AccesoConfiguration.cs:49` |
| Impacto | Ocurrencia real: "The database operation was expected to affect 1 row(s), but actually affected 0 row(s)" (500). Desbloquea test 21-22 de FASE13 y validación E2E de asignación de rol. La carrera es read-then-write sin `RowVersion`; el índice único protege la BD pero no la excepción EF |
| Dependencias | A1 migración (trigger eliminado); decisión U08 (FK compuesta `(IdUsuarioTenant, IdUsuario)` reemplaza al trigger) |
| Prioridad | **P0** — bug funcional real, bloquea certificación |
| Acción sugerida | 1) Eliminar `HasTrigger` en `AccesoConfiguration.cs:11` (alinear EF con esquema vivo). 2) Hacer `AsignarAccesoAsync` atómico (SP dedicado o manejo de `DbUpdateException` con código único → `ACCESO_DUPLICADO`) |
| Estado | ✅ **RESUELTA EN S28** (2026-08-14) — ver `S28-AccesoConcurrencia-Cierre.md` |

### DEUDA-002 — F3: Skew de versiones EF Core (P1)

| Campo | Detalle |
|---|---|
| Categoría | Framework (CBP) |
| Capa | CBP.Data + CBP.Services |
| Evidencia | `CBP.Data.Asynchronous.csproj`, `CBP.Data.Specifications.csproj`, `CBP.Data.Utilities.csproj` → EF 10.0.11; `CBP.Data.Synchronous.csproj`, `CBP.Services.Sync.csproj` → EF.Relational 10.0.9 |
| Impacto | Inconsistencia en el grafo de dependencias; riesgo de comportamiento divergente entre UoW async (10.0.11) y sync (10.0.9) |
| Dependencias | F3 ↔ `Query()/IQueryable` (enlace con CBP.Data.Specifications) ↔ placeholders `GetRepository/GetCustomRepository` |
| Prioridad | **P1** — framework, fuera del alcance certificado de PassPlat, pero inequívoca |
| Acción sugerida | Unificar a 10.0.11 en los 5 csproj al tocar CBP (sprint framework dedicado) |
| Estado | REGISTRADA (aplazada — decisión previa: no tocar CBP sin autorización) |

### DEUDA-003 — CS8602 ×3 en ConfProvIden (P1)

| Campo | Detalle |
|---|---|
| Categoría | Higiene / Nullability |
| Capa | Aplicación |
| Evidencia | `CrearConfProvIdenValidator.cs:50,55` (desreferencia posiblemente NULL) y `ConfProvIdenService.cs:119` |
| Impacto | 3 warnings C# pre-existentes; rompen el objetivo "0 warnings"; riesgo de NRE a runtime |
| Prioridad | **P1** — bajo esfuerzo, alto valor de higiene |
| Acción sugerida | Null-check / `!` justificado según contexto del flujo |
| Estado | ✅ **RESUELTA EN S28** (2026-08-14) — ver `S28-AccesoConcurrencia-Cierre.md` |

### DEUDA-004 — F8: FrameworkReference Microsoft.AspNetCore.App en CBP (P2)

| Campo | Detalle |
|---|---|
| Categoría | Infraestructura (CBP) |
| Capa | Framework |
| Evidencia | `CBP.Authentication.Abstractions.csproj`, `CBP.Authentication.JwtBearer.csproj`, `CBP.Logging.csproj`, `CBP.WebApi.csproj` |
| Impacto | Acoplamiento de proyectos CBP framework con el SDK Web de ASP.NET Core |
| Dependencias | Futura instrumentación `Jwt_Validated` (deferido a CBP.Authentication.JwtBearer) |
| Prioridad | **P2** |
| Acción sugerida | Decisión RETAIN formalizada en S25 previa; documentar como limitación aceptada |
| Estado | REGISTRADA (aplazada — decisión S25) |

### DEUDA-005 — Placeholders `GetRepository` / `GetCustomRepository` (P2)

| Campo | Detalle |
|---|---|
| Categoría | Api surface temporal |
| Capa | Framework (CBP.Data) |
| Evidencia | `UnitOfWorkAsync.cs:57/67`, `UnitOfWorkSync.cs:59/69`; 0 consumidores en PassPlat (grep) |
| Impacto | Superficie de API no usada; puente temporal tras eliminar EF del contrato UoW |
| Dependencias | F3 (resolución de tipos genéricos) |
| Prioridad | **P2** |
| Acción sugerida | Eliminar al tocar CBP (o documentar como API de compatibilidad de terceros) |
| Estado | REGISTRADA (decisión S25.1 §3) |

### DEUDA-006 — `Query()/IQueryable` RETAIN provisional (P2)

| Campo | Detalle |
|---|---|
| Categoría | Api surface heredada |
| Capa | Framework (CBP.Data.Abstractions) |
| Evidencia | 1 consumidor: `CachingRepositoryDecorator.cs:45`; enlace con CBP.Data.Specifications |
| Impacto | 0 usos en dominio de PassPlat; se conserva por el decorator de caché |
| Dependencias | F3 |
| Prioridad | **P2** |
| Acción sugerida | RETAIN justificado (decisión S25.1 §10); revisar al diseñar caché definitiva |
| Estado | REGISTRADA (aplazada) |

### DEUDA-007 — `Detach`/`DetachAsync` sin uso en producción (P2)

| Campo | Detalle |
|---|---|
| Categoría | Api surface / parity async-sync |
| Capa | Framework (CBP.Data) |
| Evidencia | Solo `S25_2ContractPurityTests.cs:268` lo referencia (assert de no-existencia de DetachAsync); 0 usos funcionales |
| Impacto | Parity async/sync a definir; no usado |
| Prioridad | **P2** |
| Acción sugerida | Decidir si Detach (existe en Sync) debe existir en Async antes de eliminar |
| Estado | REGISTRADA (decisión S25.1 §4) |

### DEUDA-008 — Reflection en `BackgroundStatusService` (P2)

| Campo | Detalle |
|---|---|
| Categoría | Observabilidad / Diseño |
| Capa | Aplicación |
| Evidencia | `BackgroundStatusService.cs:73-79` usa `GetField("_running"/"_isRunning")` y `GetProperty("IsRunning")` sobre servicios en background |
| Impacto | Frágil; rompe si el campo privado cambia de nombre; alternativa: exponer estado por interfaz pública |
| Prioridad | **P2** |
| Acción sugerida | Definir `IBackgroundJobStatus` (Started/Finished/Failed + elapsedMs) en interfaz de contratos de jobs |
| Estado | NUEVA (confirmada este sprint) |

### DEUDA-009 — Catches silenciosos en `DispConfiableService` (P2)

| Campo | Detalle |
|---|---|
| Categoría | Higiene / Observabilidad |
| Capa | Aplicación |
| Evidencia | `DispConfiableService.cs:121,143` (`catch { }` alrededor de auditoría en `EliminarAsync`/`BloquearAsync`) |
| Impacto | Pérdida silenciosa de errores de auditoría; sin log ni propagación |
| Prioridad | **P2** |
| Acción sugerida | Registrar `$"Error al auditar..."` (Concurrently con código de auditoría opcional — no bloquear acción principal) |
| Estado | NUEVA (confirmada este sprint) |

### DEUDA-010 — `UnitTest1.cs` muerto (P3)

| Campo | Detalle |
|---|---|
| Categoría | Higiene / Tests |
| Capa | Test |
| Evidencia | `PassPlat.Aplicacion.Test\UnitTest1.cs` — clase de test vacía (dead code) |
| Impacto | Documentación falsa; ruido en descubrimiento de tests |
| Prioridad | **P3** |
| Acción sugerida | Eliminar archivo; limpiar clases de test S17/S22/Unused encontradas por `find_unused_code` |
| Estado | NUEVA (confirmada este sprint) |

### DEUDA-011 — `Sql_SlowQuery` en backlog sin emisor (P3)

| Campo | Detalle |
|---|---|
| Categoría | Observabilidad |
| Capa | Infraestructura / Datos |
| Evidencia | Evento en catálogo `Logging.EventCatalog.md`; 0 referencias de código en PassPlat; alcance P6 cerrado (sin interceptor EF) |
| Impacto | No se detectan queries lentas en runtime |
| Prioridad | **P3** |
| Acción sugerida | Diseño previo (interceptor EF vs ADO clock) antes de implementar; sprint de instrumentación CBP |
| Estado | REGISTRADA (backlog) |

### DEUDA-012 — Templates de email sin certificar (P3)

| Campo | Detalle |
|---|---|
| Categoría | Funcional |
| Capa | Aplicación (Email) |
| Evidencia | Templates 3 (mfa-code), 11 (password-expired), 12 (first-login), 15 (new-device) sin fixture de certificación; `new-ip` (16) certificado vía S21 en vivo |
| Impacto | Cobertura de negocio incompleta (lista de verificación email 22 eventos) |
| Prioridad | **P3** |
| Acción sugerida | Sprint funcional dedicado con mailbox real |
| Estado | REGISTRADA (requiere ambiente de certificación) |

---

## 2. Hallazgos nuevos este sprint (S27)

| Hallazgo | Tipo | Severidad |
|---|---|---|
| `AccesoConfiguration.cs:11` declara `HasTrigger("TR_Accesos_ValidarTenant")` pero el trigger fue DROPPED en A1 y verificado ausente en BD viva → **discrepancia EF vs esquema real**; probable causa raíz de DATA-007 (INSERT con `SCOPE_IDENTITY()`/OUTPUT incorrecto frente a trigger inexistente) | CORRECCIÓN DE DIAGNÓSTICO de DEUDA-001 | P0 |
| `BackgroundStatusService.cs:73-79` reflection sobre campos privados de servicios en background | NUEVA (DEUDA-008) | P2 |
| `DispConfiableService.cs:121,143` catches silenciosos de auditoría | NUEVA (DEUDA-009) | P2 |
| `UnitTest1.cs` vacío + clases de test S17/S22 sin uso detectable | NUEVA (DEUDA-010) | P3 |
| 3 CS8602 confirmados en ConfProvIden (validators + service) | NUEVA (DEUDA-003) | P1 |
| `SaveEntitiesAsync`/`HasActiveTransaction` — 0 consumidores en PassPlat | OBSERVACIÓN (superficie retenida en UoW) | P3 |

---

## 3. Verificaciones que DESACTIVAN deudas previas

| Deuda previa | Resultado | Evidencia |
|---|---|---|
| S19-Fx IP-DETECTION-DETERMINISTIC | ✅ **RESUELTA** — `IPRepository.cs` decide `EsNueva` por existencia real (no heurística de timestamps); tests T1-T3 en `IPServiceDetectionTests.cs` | `Docs/Sprints/S19/S19-Sprint-Registry.md` + código |
| F9 (PasPlatDbContext/Aplicacion) | ✅ **RESUELTA** — 0 referencias a `PassPlatDbContext` o `Microsoft.EntityFrameworkCore` en `PassPlat.Aplicacion` | cerró con S26.2 |
| MFA `IdEstado` en SP_Auth_Login | ✅ **RESUELTA** — SP_Auth_Login filtra `IdEstado = ACTIVO AND EsPrincipal = 1` (verificado en fuente ~L1664) | `PASSWORDS SP.sql` |
| Q-AUD-002 (GetPagedAsync sin uso) | ✅ **RESUELTA** — 10 controllers usan `GetPagedAsync` (Apps, Usuarios, Tenants, ConfProvIden, ProvIden, ConfigApp, IntentosAcceso, Notificaciones, AuditoriaPwd, HistorialPwd); `IntentoAccesoService` lo overrída con tenant-scope | grep 10 controllers + 1 service |
| DI-013 (Service locator IEmailQueue) | ✅ **RESUELTA** — `IEmailQueue` inyectado por constructor en 16 servicios/handlers (AppService, AccesoService, TenantService, UsuarioService, PasswordService, ConfProvIdenService, IdenExtService, AuthService, PasswordExpirationBackgroundService, BloqueoService, 4 event handlers, ExternalAuthService, MfaService, TokenRestService, IntentoAccesoService) | grep 18 matches inyección ctor |
| `GetRequiredService` en HostedServices | ⚠️ **NO es deuda** — patrón legítimo de resolución scoped dentro de `CreateAsyncScope()` en 5 background services (IdenExtTokensRotacionJob, OutboxProcessor, EmailBackgroundService, PasswordExpirationBackgroundService, EmailTemplateStoreService); no es anti-patrón de service locator porque es el ciclo de vida correcto de un HostedService | código + S25.2 |

**Regla**: No reintroducir estas tres como deuda pendiente en el índice.

---

## 4. Matriz de prioridad objetiva

| Prioridad | Deuda | Justificación |
|---|---|---|
| **P0** | DEUDA-001 (concurrencia Acceso) | Bug funcional real; bloquea FASE13 tests 21-22 y certificación de asignación de rol |
| **P1** | DEUDA-002 (F3 skew EF) · DEUDA-003 (CS8602 ×3) | Framework inequívoco + higiene 0-warnings de bajo esfuerzo |
| **P2** | DEUDA-004 (F8) · DEUDA-005 (GetRepository) · DEUDA-006 (Query) · DEUDA-007 (Detach) · DEUDA-008 (Reflection) · DEUDA-009 (catches) | Limpieza de superficie + decisiones de diseño diferidas |
| **P3** | DEUDA-010 (UnitTest1) · DEUDA-011 (Sql_SlowQuery) · DEUDA-012 (templates) | Higiene y funcionalidad no arquitectónica |

---

## 5. Sprint recomendado

**S28 (alto valor, independiente, desbloqueante)**:
1. **DEUDA-001** — Fix concurrencia `AccesoRepository`:
   - Eliminar `HasTrigger("TR_Accesos_ValidarTenant")` de `AccesoConfiguration.cs:11`.
   - Convertir `AsignarAccesoAsync` en operación atómica: manejar `DbUpdateException` con código único (`2601/2627`) → `Result.Failure("ACCESO_DUPLICADO", ...)`, o delegar a SP dedicado. Eliminar read-then-write.
   - Verificar A0/A1: la FK compuesta `(IdUsuarioTenant, IdUsuario)` (ya en config) garantiza integridad; verificar que el servicio `AccesoService.AsignarAccesoAsync` maneje el resultado correctamente.
   - Desbloquea: test 21-22 `fase13-usuario-sin-email.spec.ts`, validación E2E asignación de rol.
2. **DEUDA-003** — Higiene CS8602 ×3.

Post-S28: decidir si se ataca DEUDA-002/F3 (sprint framework) o se avanza a deudas de diseño (P2).

> **Status 2026-08-14**: Los pasos de este sprint recomendado (DEUDA-001 + DEUDA-003) fueron **EJECUTADOS y CERRADOS** en S28 — ver `S28-AccesoConcurrencia-Cierre.md`.

---

## 6. Deudas que permanecen APLAZADAS (sin acción en S28)

| Deuda | Razón del aplazamiento |
|---|---|
| DEUDA-002 (F3 skew EF) | Requiere tocar CBP — decisión previa: no tocar framework sin autorización explícita |
| DEUDA-004 (F8 FrameworkReference) | Decisión RETAIN ya formalizada en S25 |
| DEUDA-005/006/007 (placeholders UoW, Query, Detach) | Decisiones contractuales S25.1 difieren; dependen de F3 |
| DEUDA-011 (Sql_SlowQuery) | Requiere diseño previo de interceptor (P6 alcance cerrado) |
| DEUDA-012 (templates email) | Requiere ambiente de certificación real (mailbox) |

---

## 7. Deudas del índice S15 verificadas como RESUELTAS o NO-DEUDA (confirmado S27)

| Deuda S15 | Resolución verificada en S27 |
|---|---|
| CFG-001 (fuga ciphertext ConfigUserService:83) | 🔸 `ConfigAppService.cs:129` usa `_encryption.Decrypt(valorOriginal, key)` — sin fuga; NRE mitigado |
| CFG-002 (password SQL en appsettings) | ✅ `appsettings.json` sin password literal (ConnectionString vacío → User Secrets) |
| DI-013 (service locator IEmailQueue) | ✅ Constructor injection en 16 servicios (ver §3) |
| Q-AUD-002 (GetPagedAsync sin uso) | ✅ 10 controllers + override tenant-scope (ver §3) |
| WEB-004 (6 controllers ControllerBase) | ⚠️ Confirmado aún presente: EmailLog, Grupos, GruposUsuarios, RolesHerencia, TipAsigPermiso, UsuariosPermisos (64 controllers totales). Deuda menor P3 — BasApiController migración |
| DATA-004 (IUnitOfWork en Synchronous) | ⚠️ Parte del contrato S25.2 — IUnitOfWorkAsync se declara en Synchronous (regla AGENTS). No es deuda, es ubicación contratada |
| SEC-047 (Modelo PoliticaPwd duplicado) | ⚠️ PoliticaPwd es clase única compartida entre Dominio y CBP.Security.Cryptography (decisión documentada). No es duplicación activa |

---

## 8. Reglas respetadas

- S27 es SOLO DISCOVERY: **0 cambios de código realizados**, **0 builds ejecutados** (sin cambios).
- No se generó otro prompt de S26 (cerrado).
- Ninguna deuda fue convertida en implementación durante este sprint.
- Dependencias entre deudas explícitas: F3↔DEUDA-005/006/007; A1↔DEUDA-001; DEUDA-004↔instrumentación JWT futura.

---

## 9. Estado de cierre

- **S27 DISCOVERY COMPLETO** — inventario final: **12 deudas activas** (1×P0, 2×P1, 6×P2, 3×P3).
- Verificaciones que redujeron el backlog heredado S15: 4 deudas resueltas/no-deuda adicionales (Q-AUD-002, DI-013, CFG-001, CFG-002) + GetRequiredService en HostedServices confirmado como patrón legítimo.
- **Siguiente sprint recomendado: S28** — DEUDA-001 (concurrencia AccesoRepository, desbloqueante) + DEUDA-003 (higiene CS8602).
- **Coordenadas en BD viva**: verificado 0 triggers `TR_Accesos%` en `sys.triggers` (A1.1 aplicado) — la config EF sigue declarando el trigger (DEUDA-001).
- **S28 CERRADO (2026-08-14)**: DEUDA-001 y DEUDA-003 resueltas (build 0 errores, 153/153 tests). Backlog restante: 10 deudas activas (DEUDA-002/004/005/006/007/008/009/010/011/012).

---

## 10. Trazabilidad de cierre — DEUDA-002/F3 resuelta por S29 (2026-08-14)

### Estado consolidado del sprint F3

| Sprint | Estado | Resultado |
|---|---|---|
| S27 | ✅ DISCOVERY COMPLETE | Inventario 12 deudas |
| S28 | ✅ CLOSED / GATE PASS | DEUDA-001 (concurrencia Acceso), DEUDA-003 (CS8602) |
| S29.0 | ✅ DISCOVERY COMPLETE | F3 confirmado, 11/11 gate |
| S29.1 | ✅ DESIGN APPROVED | Opción A (10.0.11), CPM diferido |
| S29.2 | ✅ CLOSED / GATE PASS | Unificación EF 10.0.11, 15/15 gate |

**DEUDA-002 (F3 skew EF) — RESUELTA por S29** — antes: CBP sync en 10.0.9 vs Async/PassPlat 10.0.11; después: **CBP.Data.Synchronous y CBP.Services.Sync en EF.Relational 10.0.11**, stack CBP y PassPlat 100% unificados. Verificación por `project.assets.json` (más sólida que solo .csproj).

### Backlog consolidado post-S29 (base de S30.0)

```
P2
├── DEUDA-004  F8 Authentication.Abstractions (RETAIN formalizado S25)
├── DEUDA-005  GetRepository / GetCustomRepository (dependía de F3)
├── DEUDA-006  Query / IQueryable (RETAIN provisional, dependía de F3)
├── DEUDA-007  Detach (sin uso producción, dependía de F3)
├── DEUDA-008  Reflection BackgroundStatusService
└── DEUDA-009  Catches silenciosos DispConfiableService

P3
├── DEUDA-010  UnitTest1 muerto
├── DEUDA-011  Sql_SlowQuery (requiere interceptor, P6 alcance cerrado)
└── DEUDA-012  Templates email sin certificar

Hardening futuro
└── CPM (Central Package Management — evita recurrencia del skew F3)
```

**Comentario clave**: Con F1, F6, F9 y F3 resueltos, las relaciones entre DEUDA-005/006/007 y F8 (que dependían de F3) ya no aplican. Recomendación de sprintología: no asumir F8 por antigüedad — realizar **S30.0 Discovery / Priorización del backlog P2** (misma metodología que S21–S29: discovery primero con deudas heterogéneas).
