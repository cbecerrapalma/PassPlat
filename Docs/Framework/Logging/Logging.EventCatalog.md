# CBP.Logging — Catalog of Events

**Tipo**: Catálogo oficial de eventos del framework
**Versión del contrato**: 1.0 (CONGELADO)
**Especificación de referencia**: [CBP.Logging.Specification.md](../CBP.Logging.Specification.md)
**Campaña**: S16.4 / S16 Release Candidate (RC1)

Este documento es el **catálogo autorizado de eventos** de CBP.Logging. Complementa
la especificación: mientras la especificación define *el contrato y las reglas*, este
catálogo enumera *cada `EventName` con su `Scope` y descripción*. Es la referencia para
dashboards, alertas, auditorías, integración con OpenTelemetry y mantenimiento del
framework.

Regla de gobernanza: cualquier `EventName` usado en el código DEBE estar registrado aquí
bajo una de las secciones siguientes. Un evento que aparezca en el código y no en este
catálogo es una violación de contrato.

---

## Scopes (LoggingScopes)

Flujo funcional transversal que agrupa eventos de varias categorías técnicas.

| Scope | Valor |
|-------|-------|
| Authentication | `authentication` |
| Authorization | `authorization` |
| PasswordPolicy | `passwordPolicy` |
| Cache | `cache` |
| Email | `email` |
| DomainEvents | `domainEvents` |
| Persistence | `persistence` |
| Sql | `sql` |
| BackgroundJobs | `backgroundJobs` |
| WebApi | `webApi` |
| Api | `api` |

---

## Catálogo de eventos

### Cache (Scope=`cache`)

| EventName | Scope | Descripción |
|-----------|-------|-------------|
| `Cache_Hit` | cache | Lectura desde caché (acierto) |
| `Cache_Miss` | cache | Lectura desde origen (fallo de caché) |
| `Cache_Set` | cache | Valor escrito/actualizado en caché |
| `Cache_Invalidation` | cache | Clave invalidada tras escritura subyacente |

### Authentication (Scope=`authentication`)

| EventName | Scope | Descripción |
|-----------|-------|-------------|
| `Login_Succeeded` | authentication | Usuario autenticado correctamente |
| `Login_Failed` | authentication | Intento de autenticación con credenciales inválidas |
| `Jwt_Generated` | authentication | Token JWT emitido |
| `Jwt_Validated` | authentication | Token JWT validado correctamente |
| `Jwt_Expired` | authentication | Token JWT expirado |
| `RefreshToken_Issued` | authentication | Refresh token emitido |
| `Logout` | authentication | Sesión revocada (logout) |

### Security (Scope=`authorization` / `passwordPolicy`)

| EventName | Scope | Descripción |
|-----------|-------|-------------|
| `Password_Changed` | passwordPolicy | Contraseña cambiada correctamente |
| `Password_Reset` | passwordPolicy | Contraseña restablecida por flujo de recuperación |
| `Password_PolicyViolation` | passwordPolicy | Contraseña rechazada por política |
| `Account_Locked` | authorization | Cuenta bloqueada |
| `Account_Unlocked` | authorization | Cuenta desbloqueada |
| `Mfa_Succeeded` | authentication | Verificación MFA correcta |
| `Mfa_Failed` | authentication | Verificación MFA fallida |

### Email (Scope=`email`)

| EventName | Scope | Descripción |
|-----------|-------|-------------|
| `Email_Queued` | email | Correo encolado para envío |
| `Email_Sent` | email | Correo enviado correctamente |
| `Email_Failed` | email | Correo no enviado (error de proveedor o reintentos agotados) |

### Domain Events (Scope=`domainEvents`)

| EventName | Scope | Descripción |
|-----------|-------|-------------|
| `Event_Published` | domainEvents | Evento de dominio publicado |
| `Event_Handled` | domainEvents | Evento de dominio manejado por un handler |
| `Event_Failed` | domainEvents | Handler de evento fallido |
| `Event_Queued` | domainEvents | Evento de dominio encolado (Outbox) pendiente de publicación |

### Background (Scope=`backgroundJobs`)

| EventName | Scope | Descripción |
|-----------|-------|-------------|
| `Background_JobStarted` | backgroundJobs | Job en segundo plano iniciado |
| `Background_JobFinished` | backgroundJobs | Job en segundo plano completado |
| `Background_JobFailed` | backgroundJobs | Job en segundo plano falló |

### Persistence / Sql (Scope=`sql`)

| EventName | Scope | Descripción |
|-----------|-------|-------------|
| `Sql_SlowQuery` | sql | Consulta SQL que supera el umbral de latencia |

---

## Estado de implementación por EventName

| EventName | Emisor (S16.4) |
|-----------|----------------|
| `Cache_Hit` | repositorios (S16.3: PoliticaPwd/ConfigTenant/App) |
| `Cache_Miss` | repositorios (S16.3) |
| `Cache_Set` | repositorios (S16.3) |
| `Cache_Invalidation` | repositorios (S16.3) |
| `Login_Succeeded` / `Login_Failed` | `AuthService` (P1) |
| `Mfa_Succeeded` / `Mfa_Failed` | `AuthService` (P1) |
| `RefreshToken_Issued` | `AuthService` (P1) |
| `Logout` | `AuthService` (P1) |
| `Password_Changed` / `Password_PolicyViolation` | `PasswordService` (P2) |
| `Password_Reset` | `PasswordService` (P2) — punto de emisión implementado; cobertura E2E pendiente |
| `Account_Locked` / `Account_Unlocked` | `BloqueoService` (P2) |
| `Email_Queued` | `EmailQueue` (P4) |
| `Email_Sent` / `Email_Failed` | `PassPlatEmailService` + `EmailBackgroundService` (P4/P5) |
| `Jwt_Generated` | `AuthenticationTokenIssuer` (S17 F3) — emitido en vivo |
| `Jwt_Validated` / `Jwt_Expired` | `JwtTokenService` (S17 F3) — `Jwt_Validated` emitido en vivo |
| `Event_Published` / `Event_Handled` / `Event_Failed` | `EventDispatcher` (S17 F3, renombrado en S22) — código + tests 6/6; `Event_Published`/`Event_Handled` **certificados en vivo (S18, vía `DeviceRevokedEvent` 17:03:29)**; `Event_Failed` cubierto por tests/contrato T5 (no declarado E2E certificado) |
| `Event_Queued` | `IPService.DetectarNuevaIPConOutboxAsync` (S33.2, V-02 resuelto) — emisor migrado a `LoggingEvents.EventQueued` |
| `Background_JobStarted` / `Background_JobFinished` / `Background_JobFailed` | 4 jobs background (P5) — `Background_JobStarted`/`Finished` emitidos en vivo |
| `Sql_SlowQuery` | `SqlSlowQueryInterceptor` (canal EF: LINQ/SaveChanges/ExecuteSqlRaw/FromSqlQuery) + `RawQueryRepository{Async,Sync}.Measure*` (canal ADO SP) — S37.2, **emitido en vivo** (smoke G-6: 5 commandTypes, 42 eventos, evidence `Docs/Evidence/s37-smoke-20260817.log`) |

**Pendientes (reservados en el catálogo, sin emisor aún)**: _(ninguno — S37.2 cierra el único pendiente; DEUDA-012 emails sigue en sprint funcional separado)_

---

## Cambios

| Cambio | Descripción | Versión |
|--------|-------------|---------|
| Inicial | Catálogo oficial RC1 (S16.4) | 1.0 |
| S17 F3+F7 | Emisores añadidos: Jwt_Generated (AuthenticationTokenIssuer), Jwt_Validated/Jwt_Expired (JwtTokenService), Event_Published/Handled/Failed (DomainEventDispatcher) + fix binder dispatch. Evidencia en vivo: Jwt_Validated ✓, Background_* ✓; Event_* pendiente (hallazgo S17-F6) | 1.1 |
| S18 | Event_Published / Event_Handled certificados en vivo (flujo incondicional `DeviceRevokedEvent`, correlationId W3C propagado); Event_Failed cubierto por tests/contrato T5 | 1.1 |
| S22 | Renombrado del emisor `DomainEventDispatcher` → `EventDispatcher` (refactor CBP.Events). Emisión, scopes (`domainEvents`), categorías y CorrelationId **sin cambios** — contrato de logging congelado S16.4 intacto | 1.1 |
| S33.2 | V-02 resuelto: `Event_Queued` registrado (aditivo). Fuente de verdad: `LoggingEvents.EventQueued` en `LoggingEvents.cs`; emisor `IPService.cs` migrado de literal a constante (0 breaking, mismo valor runtime, cadena S21/S22/S25.2 intacta). Añadido guard Roslyn de gobernanza (`IEventNameCatalogGuard` + T1–T6): `LoggingEvents.cs` = enforcement ejecutable; este catálogo = documentación sincronizada (T5B) | 1.2 |
| S37.2 | DEUDA-011 resuelta: `Sql_SlowQuery` pasa de "reservado" a **emitido en vivo**. Emisor doble (canales disjuntos, 1 comando = 1 evento): `SqlSlowQueryInterceptor` (EF: `linqQuery`/`saveChanges`/`executeSqlRaw`/`fromSqlQuery`) + `RawQueryRepository{Async,Sync}.Measure*` (ADO SP: `storedProcedure`/`text`). Umbral `Cbp:Logging:SqlSlowQueryThresholdMs=250` (LoggingOptions). Metadata segura: `commandType/procedureName/commandName/elapsedMs` — sin parámetros ni valores. Certificado smoke G-6 (5 commandTypes, 42 eventos). Contract tests 19/19. Parity Async≈Sync 6/6 | 1.3 |