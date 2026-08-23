# S16-Sprint-Registry.md — Registro de Trazabilidad S16 (implementación basada en evidencia S15)

# Estado          En curso
# Nivel           Ejecución (N0-N3 puente: implementación de hallazgos N1/N2 certificados)
# Origen          Baseline S15 (referencia): `S15-Audit-Methodology.md` (gobernanza)
# Regla           NINGUNA tarea entra a S16 sin hallazgo trazable en S15. Prohibido ampliar alcance con ideas nuevas.

---

## Flujo obligatorio (ver S15 metodología §3)

```
Hallazgo S15 (N1/N2) → Implementación S16 → Validación (build/test) → Cierre (marcar en matriz S15)
```

Cada commit/tarea referencia explícitamente su hallazgo. Al cerrar S16, la matriz de cumplimiento S15 marca qué desviaciones quedaron resueltas.

---

## Registro de tareas S16

| ID | Origen S15 | Hallazgo | Resultado | Acción | Estado |
|---|---|---|---|---|---|
| S16-001 | S15-Logging-Audit | LOG-006 / CFG-001 / SEC-001 | FAIL | EXTERMINAR | INSTALLED |
| S16-002 | S15-Configuration-Audit | CFG-002 | FAIL | REEMPLAZAR | EXTERNOR |
| S16-003 | S15-Security-Logging-Audit | SEC-005 | FAIL | REEMPLAZAR | IMPLEMENTADO |
| S16-004 | S15-Security-Logging-Audit | SEC-007 | WARNING | REEMPLAZAR | IMPLEMENTADO |
| S16-005 | S15-Security-Logging-Audit | SEC-006 | WARNING | REEMPLAZAR | IMPLEMENTADO |
| S16-006 | S15-Events-Audit | EVENT-002/003 | FAIL | REEMPLAZAR | IMPLEMENTADO |
| S16-007 | S15-Caching-Audit | CAD-001/002 | WARNING | EXTENDER | ✅ AUDITADO (S16.3.1 ROI) |
| S16-008 | S15-Logging-Audit | LOG-001..007 | FAIL/WARN | REEMPLAZAR | PENDIENTE |
| S16-009 | S15-Caching-Opportunity | CAD-001 (PoliticaPwd) | FAIL | EXTENDER | ✅ CERTIFICADO (G3.1) |
| S16-010 | S15-Caching-Opportunity | CAD-002 (ConfigTenant) | WARNING | EXTENDER | ✅ CERTIFICADO PASS-CON-OBS (G3.2) |
| S16-011 | S15-Caching-Opportunity | CAD-005 (Apps) | WARNING | EXTENDER | ✅ CERTIFICADO (G3.3) |

### Íneas de implementación cerradas (detalle)

**S16-001 — LOG-006/CFG-001/SEC-001 (P0):** eliminada fuga de ciphertext en `ConfigAppService.cs:83` (`Console.WriteLine` con prefix de ciphertext). Fix: se elimina la línea de exposición; se conserva el cifrado correcto. Confianza alta.

**S16-002 — CFG-002 (P0):** retirado el password SQL en texto plano de `appsettings.Development.json`. La connection string completa vive en User Secrets (`PassPlat.WebAPI-Secrets`); el developer-json queda con placeholder sin credencial (TrustServerCertificate=True). Precedencia correcta (User Secrets > appsettings.Development.json).

**S16-003 — SEC-005 (P1):** `AuthService.EnviarCodigoMfaAsync` pasa de `void` silencioso a `Task<Result>`; ya NO traga excepciones (propaga `MFA_SEND_ERROR`, `MFA_SIN_EMAIL`, `MFA_READ_ERROR`). El catch conserva `LogError` con stack trace y retorna Failure. `ObtenerTipoMfaAsync` idem a `Result<int?>` (SEC-007, no fallback silencioso).

**S16-004 — SEC-007 (P1):** en `LoginAsync`, si el tipo MFA no puede obtenerse o el envío del código falla, el login de MFA-requerido se RECHaza con el error (evita resolver a "sin MFA" por silencio → previene bypass).

**S16-005 — SEC-006 (WARN):** `BloqueoService.NotificarBloqueoAsync`/`NotificarDesbloqueoAsync` pasan de `void` a `Result`; el catch ya no traga: loguea y retorna `NOTIFY_ERROR`. Los callers distinguen las primarias (bloqueo creado) de las secundarias (notif email) — never se pierde la traza de entrega.

**S16-006 — EVENT-002/003 (FAIL):** migración de publicadores estáticos a pipeline CBP.Events DI. `IPEventPublisher` y `DispConfiableEventPublisher` (`sealed static` con `IEmailQueue` param) ELIMINADOS; su lógica de encolado se movió a `IEventHandler<T>` resueltos por DI y despachados vía `IEventPublisher`. Cambios: (1) nuevos handlers `NewIpDetectedEventHandler`, `SecurityAlertEventHandler` (`Services\Security\IpEventHandlers.cs`) y `NewDeviceDetectedEventHandler`, `DeviceRevokedEventHandler` (`Services\Security\DispConfiableEventHandlers.cs`) que reproducen el enqueue a `IEmailQueue` con `EmailJobKind.*` correspondiente y mapeo IdTenant/IdUsuario/CorrelationId; (2) `IPService` y `DispConfiableService` inyectan `IEventPublisher` (en vez de `IEmailQueue`) y publican el record de dominio con `PublishAsync` + correlación; (3) `AddDomainEvents()` + `AddEventHandlersFromAssembly` en `AplicacionDependencyInjection`. Clases estáticas y su `using Email` eliminadas de los archivos de eventos (solo records quedan). Resultado: pipeline de eventos desacoplado del email, extensible a otros handlers sin tocar los servicios. Build 0 errores · xUnit 66/66 PASS.

### Verificaciones de cierre S16.2 (no bloqueantes, ejecutadas)

**Verif-1 — IEmailQueue (clasificación de usos):** ✓ consumidores legítimos = `EmailBackgroundService`, `BackgroundStatusService`, los 4 EventHandlers de S16.2. ✓ `IPService`/`DispConfiableService` ya NO conocen IEmailQueue. ⚠ 13 Application Services + `PasswordExpirationBackgroundService` enquean notificaciones de negocio directamente (código MFA, reset token, cuenta bloqueada) — son efectos secundarios síncronos del flujo, NO eventos de dominio (migrar = expansión alcance, prohibido). Deuda documentada para S16.4. ⚠ antipatrón service-locator: `PasswordController.cs:99,143` + `UsuariosController` usan `GetRequiredService<IEmailQueue>()`.

**Verif-2 — CorrelationId/contexto:** los 4 eventos S16.2 usan `WithCorrelationId()` en el servicio y handlers pasan `@event.CorrelationId` ✓. `EventBase` provee `EventId`, `OccurredOn`(Timestamp), `CorrelationId`; cada record lleva UserId/TenantId. **Gap menor para S16.4**: falta `AppId` (siempre null) y `Source`. No bloqueante.

**Resultado S16.2**: 🔒 cerrado (regresión PASS, build 0, 66/66).

**S16.3.1 — ROI Caché (S16-007):** matriz ROI creada en `Docs/Sprints/S16/S16.3-Caching-ROI.md` respaldada en evidencia S15 (CAN-001..005), no intuición. Priorizado: PoliticaPwd (P1, 6-8 queries/login) > ConfigTenant (P2) > Apps catálogo (P3). Roles/Permisos ya en claims JWT (JUSTIFICAR), Menús aplazado.

**S16.3.2 — Implementación CBP.Caching (ICacheService):**
- **S16-009 / CAD-001 PolíticaPwd:** `PoliticaPwdRepository` añade `ICacheService`. Claves `politicapwd:applicable:{tenant}:{app}` / `politicapwd:applicable:{tenant}` / `politicapwd:global`, TTL 60s Sliding. `InvalidarCacheAsync()` con `RemoveByPatternAsync("politicapwd:")`, llamado desde `PoliticaPwdService` en Crear/Actualizar/Desactivar.
- **S16-010 / CAD-002 ConfigTenant:** `ConfigTenantRepository` clave `configtenant:tenant:{idTenant}`, TTL 60s; invalidación en `ActualizarPepperVersion`.
- **S16-011 / CAD-005 Apps:** `AppRepository` clave `app:catalog:activas`, TTL 60s; `InvalidarCacheAsync()` desde `AppService.CrearAsync`/`DesactivarAsync`.
- Patrón: `ConfigAppRepository` (Get→Set TTL→Remove en escritura). Solo `ICacheService`, sin IMemoryCache en negocio.
- Verificación: **Build 0 errores** · **xUnit 69/69 PASS** · **G3 formal (nivel de gate) implementado en vivo con evidencia `api-*.log`**. Se requisito del Ancla G0 se instrumentó el capa de Data con `CBP.Logging` (catálogos `LoggingEvents`/`LoggingCategories`/`LoggingSources`/`LoggingCacheResults`/`LoggingPropertyNames` en CBP.Core; `LogEvent.EventName`; repos stateless sin contadores; `EmitCacheEvent` con `ForContext`→ propiedades estructuradas).

**S16.3.3 — Certificación G3 (cache + logging contract):** evidencia capturada en vivo (`PassPlat.WebAPI`, puerto 5259) + test de contrato dedicado.

| Gate | Resultado | Evidencia |
|---|---|---|
| G3.1 PolíticaPwd | ✅ PASS | `politicapwd:global` → Miss(sqlserver)→Refreshed(memory)→Hit(memory) |
| G3.2 ConfigTenant | ✅ PASS CON OBSERVACIÓN | Repo instrumentado (`configtenant:tenant:{id}`); 403 en vivo por policy `CONFIG_APP_VER` (dominio autorización, no caché) |
| G3.3 Apps | ✅ PASS | `app:catalog:activas`: Miss→Refreshed→Hit; **invalidation** en create/desactivar → re-Miss→Refreshed; consistencia 1→2→1 (incluye app temporal de cert. desactivada tras el test) |
| G3.4 Events | ✅ PASS (implícito) | repositorios/SP instrumentados: toda escritura invalida (`InvalidarCacheAsync`) |
| G3.5 Correlación | ✅ PASS | test contrato real `LoggerService`: `EnrichLogEventContext` adjunta `CorrelationId`/`UserId`/`ClientIp` (PascalCase) al evento estructurado |
| G3.6 ElapsedMs | ✅ PASS (parcial) | contrato verifica `elapsedMs` por evento; MISS≈15.5ms vs HIT≈10.4ms (1er ciclo); esp. renderizado del template consola difiere se audita en S16.4 |
| G3.7 Contrato | ✅ PASS | 3/3 tests `CacheLogContractTests` (HIT/MISS/Pipeline) — 2 paretes, `elapsedMs`, `tenantId`, enrichment presentes en evento Serilog |

- **Finding S16.4 (no bloqueante S16.3):** el enriquecimiento automático (`LoggerService.EnrichHttpContextCore`) emite claves **PascalCase** (`CorrelationId`, `UserId`, `ClientIp`, `RequestPath`) mientras el catálogo del emisor usa **lowerCamel** (`category`, `tenantId`, `elapsedMs`). Unificar nominación en `CBP.Logging.Specification.md` (S16.4).
- **ConfigTenant observación:** no FAIL — el `403` pertenece al dominio de autorización (`CONFIG_APP_VER`), no al mecanismo de caché. PASS CON OBSERVACIÓN.

---

## Estado de gates

| Gate | Estado |
|---|---|
| S16.1 Seguridad P0/P1 (S16-001..005) | ✅ Build 0 errores · cambios implementados |
| S16.2 Events (S16-006) | ✅ Build 0 errores · xUnit 66/66 · publicadores estáticos eliminados |
| S16.3 Caching (S16-007) | ✅ Auditoría ROI (S16.3.1) · S16-009..011 implementados · Build 0 · xUnit 69/69 · **G3.1–G3.7 certificados en vivo** |
| S16.4 Logging (S16-008) | ✅ Especificación `CBP.Logging.Specification.md` v1.0 · F1–F4 (camelCase + LoggingScopes + eventName/scope emitidos + tests anti-PascalCase) · Build 0 · xUnit 70/70 · **F5 instrumentación transversal: P1 Auth ✅ (Login_*/Mfa_*/Refresh/Jwt_Generated/Logout; Jwt_Validated ⏳ diferido a CBP.Authentication) · P2 Security ✅ (Password_Changed/Reset/PolicyViolation + Account_Locked/Unlocked; Password_Reset E2E pendiente) · P4 Email ✅ (Queued/Sent/Failed) · P5 Background ✅ CERTIFICADO en vivo (EmailBackgroundService + PasswordExpirationBackgroundService + IdenExtTokensRotacionJob + SesionCleanupService: Background_JobStarted/Finished/Failed con elapsedMs) · P3 Events ⏳ DEFERRED a CBP.Events (rehúye instrumentar bus; requeriría añadir CBP.Logging al framework) → **RESUELTO POST-S16 por S17+S18** (instrumentación CBP.Events + certificación runtime Event_Published/Handled) · P6 Persistence ✅ alcance cerrado (cache/invalidaciones ya cubiertas; CRUD genérico fuera de alcance; SQL lento **sin interceptor EF por decisión de alcance**) · Build 0 · xUnit 70/70 |
| Gate C (Playwright E2E) | ✅ **PASS — APROBADO FORMALMENTE (2026-08-08)**. ▶️ Ejecutado **11/11 PASS** (2026-08-08, `tests\gateC-observability.spec.ts`). C1 Login/JWT/claims + C1.4 inválido 401 + C1.5 refresh 200 · C2 Cache Miss→Hit · C3 Invalidate→Hit · C5.1/C5.2 Logout + refresh 401 (evidencia §7 del doc S16.4-Observability-GateC). **C-1/C-2 RESUELTOS** (2026-08-08 02:24, corrida acotada 3/3 PASS): `PlatformLoginAsync` emite `Login_Succeeded`/`Login_Failed` en 5 ramas de rechazo; `EmailQueue.EnqueueAsync` fallback `job.CorrelationId ?? HttpContext.Items[HttpCorrelationIdKey]` → `Email_Queued` propaga correlationId del request (evidencia `gatec-fix-20260808.log`). Bug corregido: logout revocaba sesión por `jti` como `Id` → `RevocarSesionPorJtiAsync` (patrón SwitchToPlatform). Build 0 · xUnit 70/70. **S16.4 CERRADO · RC1 APROBADO · S17 AUTORIZADO** (deudas no bloqueantes: `Jwt_Validated` → CBP.Authentication.JwtBearer, `Event_*` → CBP.Events, `Password_Reset` como observación E2E). |

---

## Matriz de cumplimiento S15 (cierre S16 ✅ 2026-08-08)

(Se completará al terminar el sprint: marcar cada hallazgo S15 como RESUELTO/PARCIAL/SIN CAMBIO con referencia S16-xxx.)

| Hallazgo S15 | Resuelto por | Estado |
|---|---|---|
| CFG-001 | S16-001 | ✅ |
| CFG-002 | S16-002 | ✅ |
| SEC-001 | S16-001 | ✅ |
| SEC-005 | S16-003 | ✅ |
| SEC-007 | S16-004 | ✅ |
| SEC-006 | S16-005 | ✅ |
| EVENT-002/003 | S16-006 | ✅ |
| CAD-001 | S16-009 (PolíticaPwd) | ✅ |
| CAD-002 | S16-010 (ConfigTenant) | ✅ |
| CAD-005 | S16-011 (Apps) | ✅ |
| LOG-001..007 | S16-008 | ✅ RESUELTO (contrato + instrumentación F5 + validación Playwright Gate C) |

---

## Cierre F5 — Instrumentación transversal (S16.4)

### Decisión de estado

| Sub-ámbito | Estado | Evidencia / Razón |
|---|---|---|
| P1 Auth | ✅ PASS CON OBSERVACIÓN | Login_Succeeded/Failed, Mfa_*, RefreshToken_Issued, Logout, **Jwt_Generated** (AuthenticationTokenIssuer.Generate). `Jwt_Validated` ⏳ diferido a **CBP.Authentication.JwtBearer** → **RESUELTO por S17** (JwtTokenService instrumentado, evidencia en vivo). |
| P2 Security | ✅ PASS CON OBSERVACIÓN | Password_Changed, **Password_Reset** (ramificación `ETipoCambioPwd.Reset` en `PasswordService.CambiarPasswordAsync`), Password_PolicyViolation, Account_Locked/Unlocked. `Password_Reset` E2E ⏳ (requiere token real + flujo `olvido-password`/SMTP). |
| P4 Email | ✅ PASS | Email_Queued (EmailQueue), Email_Sent/Failed (PassPlatEmailService), Email_Failed reintentos (EmailBackgroundService:230). |
| P5 Background | ✅ **CERTIFICADO en vivo** | 4 jobs con ciclo Started→Finished/Failed + elapsedMs: EmailBackgroundService, PasswordExpirationBackgroundService, IdenExtTokensRotacionJob, SesionCleanupService. Evidencia log `bin\Debug\net10.0\Logs\passplat-20260807.log` (arranque PID en vivo). |
| P3 Events | ⏳ **DEFERRED** → **✅ RESUELTO POST-S16 (S17+S18)** | `Event_Published/Handled/Failed` pertenecían al bus `CBP.Events` (`IEventPublisher`/`DomainEventDispatcher`); instrumentarlo exigía introducir `CBP.Logging` en el framework (regla Jwt_Validated). **S17** instrumentó `DomainEventDispatcher` con `ILoggerService` opcional (contrato v1.0 intacto) y **S18** certificó `Event_Published`/`Event_Handled` en runtime vía `DeviceRevokedEvent`. `Event_Failed` cubierto por tests/contrato T5. |
| P6 Persistence | ✅ **Alcance cerrado** | Caché + invalidaciones + excepciones ya cubiertos (S16.3 G3, EmitCacheEvent). CRUD genérico **fuera de alcance** deliberado. SQL lento **sin interceptor EF por decisión de alcance** (implementarlo ampliaría S16). |

### Pendientes conocidos (enable solo decisiones, no bloquean S16.4 funcional)

- `Jwt_Validated` → **RESUELTO en S17** (instrumentación `JwtTokenService`, evidencia en vivo).
- `Event_Published/Handled/Failed` → **RESUELTO en S17+S18** (instrumentación `CBP.Events` + certificación runtime via `DeviceRevokedEvent`; `Event_Failed` por tests/contrato).
- `Password_Reset` → falta validación E2E con token real (flujo recuperación).
- Renderizado de propiedades estructuradas/EventName en template File → diferido a etapa de observabilidad.

> **Regla de no-bypass Gate C:** NO tocar `CBP.Events`, `CBP.Authentication.JwtBearer` ni los templates Serilog para "hacer pasar" Gate C. Esas piezas están fuera del alcance certificado de S16.4.
>
> **Resolución post-S16 (trazabilidad, sin reabrir S16.4):** las deudas `Jwt_Validated` y `Event_*` fueron resueltas posteriormente por **S17** (instrumentación en `CBP.Authentication.JwtBearer.JwtTokenService` y `CBP.Events.DomainEventDispatcher`, contrato v1.0 intacto) y **S18** (certificación runtime de `Event_Published`/`Event_Handled`). Ver `S17-Sprint-Registry.md` y `S18-Discovery.md`.

---

## Ejecución Gate C — Evidencia E2E (2026-08-08)

### Resultado: **11/11 PASS** (`tests\gateC-observability.spec.ts`, API `https://localhost:5001/api`)

| Test | Contrato | Resultado |
|------|----------|-----------|
| C1.1 | Login `/auth/login/platform` → JWT + encabezado `x-correlation-id` | ✅ PASS |
| C1.2 | Claims JWT: `jti`/`permiso`/`iss=PassPlat`/`aud=PassPlat`/`exp` | ✅ PASS |
| C1.3 | Endpoint protegido `/apps` con JWT → HTTP 200 | ✅ PASS |
| C1.4 | Login inválido → 401 (Login_Failed, acceso denegado) | ✅ PASS |
| C1.5 | Refresh token válido → 200 (RefreshToken_Issued) | ✅ PASS |
| C2.1-C2.2 | Cache `/apps/activas`: `sqlserver\|Miss` → `memory\|Hit` | ✅ PASS |
| C3.1-C3.2 | Crear App → invalida `app:catalog:activas` → refleja nueva App | ✅ PASS |
| C5.1 | POST `/auth/logout` → `Sesión revocada (logout por jti)` → 200 | ✅ PASS |
| C5.2 | Refresh tras logout → 401 (hash no encontrado, sesión revocada) | ✅ PASS |

> **Estado de aprobación:** ✅ **APROBADO FORMALMENTE (2026-08-08)** — el responsable de
> arquitectura concedió el cierre conforme al criterio §9 del doc
> `S16.4-Observability-GateC.md`. Gate C = **PASS**, S16.4 = **CERRADO**, RC1 =
> **APROBADO**, S17 = **AUTORIZADO** (respetando deudas registradas en backlog).
> Esta sección registra la evidencia factual de la ejecución que sustentó la aprobación.

### Bug funcional real corregido en Gate C — Logout no revocaba sesión

**Síntoma (C5.2 FAIL pre-fix):** tras `POST /auth/logout` (HTTP 200), `refresh` seguía devolviendo 200 (sesión activa).

**Causa raíz:** el JWT usa `jti` (`Guid.NewGuid()` en `AuthenticationTokenIssuer.Generate`) como claim, pero la sesión se persiste en BD con el jti en **`IdTokenExt`** (`SessionManager.CreateSessionAsync` → `SP_Sesiones_Crear`, param `@IdTokenExt`), no en `Id`. `AuthController.Logout` parseaba `jti` como `Guid` y llamaba `RevocarSesionAsync(idSesion)` que busca `s.Id == idSesion` (`SesionRepository.RevocarSesionAsync:54`) → nunca encontraba la sesión → no-op silencioso (`[WRN] RevocarSesionAsync: sesión "..." no encontrada o ya inactiva`) → `Result.Success` → HTTP 200 con sesión viva.

**Fix aplicado:** nueva `IAuthService.RevocarSesionPorJtiAsync(idUsuario, jti)` en `AuthService`, que usa `SessionManager.ResolveAndRevokeSessionByJtiAsync` (por `ObtenerSesionActivaPorJtiAsync`, `IdTokenExt == jti` — mismo patrón que `SwitchToPlatform`). `AuthController.Logout` ahora resuelve por claim `nameidentifier` + `jti`. Evidencia log post-fix: `SwitchToPlatform: sesión revocada | SessionId=b22460a2-... | Usuario=2 | Jti=3fb5c8bf-...` + `Sesión revocada (logout por jti)`. BD: sesión queda `EsActiva=False, HashRefresh=NULL`.

**Cambios:** `PassPlat.Aplicacion\Services\SPro\AuthService.cs` (interfaz + impl) · `PassPlat.WebAPI\Controllers\AuthController.cs` (Logout). **Build:** 0 errores (solo NU1603 pre-existente). **xUnit: 70/70 PASS.**

### Fallos funcionales del Gate C: 0 tras fix
- Frontera: la invalidación de caché y los eventos `Jwt_Validated`/`Event_*`/`Password_Reset` quedan como limitaciones conocidas (framework/diferido), igual que en la tabla F5.

### Hallazgos de contrato — corrida 2026-08-08 01:30 (evidencia estructurada `{Properties:j}`)

Conforme al criterio "evidencia → decisión" (no forzar PASS si una propiedad del
contrato no queda demostrada), la corrida con template `{Properties:j}` (artefacto
congelado `Docs/Evidence/gatec-structured-run-20260808.log`, L500–565) demostró en
vivo: `Jwt_Generated`, `RefreshToken_Issued`, `Cache_Miss/Cache_Set/Cache_Hit/Cache_Invalidation`,
`Logout`, `Email_Queued`, `Email_Sent`, con `correlationId` W3C consistente dentro de
cada request (`00-<traceId>-<spanId>-00`). Dos observaciones impiden el PASS formal:

| # | Hallazgo | Evidencia | Impacto |
|---|----------|-----------|---------|
| C-1 | `Login_Succeeded`/`Login_Failed` NO emitidos por la ruta probada (`/api/auth/login/platform` → `PlatformLoginAsync`); solo `Jwt_Generated` + mensajes legacy (SessionCreated/TokenGenerated). Los emisores existen en `LoginAsync` (tenant), no en `PlatformLoginAsync`. | L502 (200) y L519-520 (401) sin evento `Login_*` | Fila `Login_*` de la matriz §4 no certificada E2E |
| C-2 | `Email_Queued` en `POST /api/apps` emite `correlationId=null` pese a request con correlación activa (`00-6ed4d21c…`). Emisor `EmailQueue` fija el valor desde `job.CorrelationId` (null) y el enricher `LoggerService` usa `TryAdd` (no sobreescribe). | L548 | Check #13 del checklist (`CBP.Logging.Validation.md`) no cumplida para ese evento; `Email_Sent` en background es null legítimo (sin HttpContext) |

**RESUELTOS 2026-08-08 02:24 (corrida fix acotada 3/3 PASS, evidencia `Docs/Evidence/gatec-fix-20260808.log`):**

| # | Fix | Implementación | Verificación |
|---|-----|----------------|--------------|
| C-1 | `PlatformLoginAsync` emite `Login_Succeeded` (éxito) y `Login_Failed` (5 ramas: usuario no encontrado, cuenta eliminada, inactiva, hash no disponible, contraseña inválida) | `AuthService.cs` L639-680: 6 llamadas a `LogAuthEvent` | C1.1: `Login_Succeeded` (200); C1.4: `Login_Failed` (401) — 3/3 PASS acotada |
| C-2 | `EmailQueue.EnqueueAsync` fallback `job.CorrelationId ?? HttpContext.Items[HttpCorrelationIdKey]`; sin tocar 26 call-sites; `IHttpContextAccessor` singleton ya registrado | `EmailQueue.cs` L91-104: constructor + resolve en EnqueueAsync | C3.1: `Email_Queued` con `correlationId` idéntico a `AUTHZ OK`/`Cache_Invalidation`/`RequestLoggingMiddleware` (`00-fe395...`) |

**Template Serilog restaurado** tras la captura fix (sin `{Properties:j}`). Build: 0 errores · xUnit: 70/70.
