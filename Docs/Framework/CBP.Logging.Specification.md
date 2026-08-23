# CBP.Logging — Specification

**Estado**: Activo (CONGELADO) · **Versión**: 1.0 · **Campaña**: S16.4 (Framework Observability Contract)
**Alcance**: `CBP.Core/CBP/Logging` (catálogos) + `CBP.Logging` (enriquecimiento/sinks).
**Fuente de verdad**: este documento. Toda instrumentación nueva DEBE seguir estas convenciones.

---

## 1. Objetivos

1. **Logging estructurado** — eventos con propiedades tipadas (`category`, `tenantId`, `elapsedMs`), consultables en Seq, Grafana, Kibana, OpenTelemetry.
2. **Independencia del proveedor** — el contrato es agnóstico de Serilog, Seq, OpenTelemetry, etc. Los sinks se cambian sin tocar la instrumentación.
3. **Observabilidad transversal** — correlación (`correlationId`), usuario (`userId`), tenant (`tenantId`), capa (`category`), flujo funcional (`scope`).
4. **Compatibilidad futura** — catálogos centralizados en `CBP.Core` para que Data, Services, WebAPI, Background y UI consuman un único vocabulario.
5. **Nunca literales libres** — todo nombre de evento, categoría, fuente, scope y propiedad proviene de un catálogo `CBP.Logging.*`.

## 2. Modelo LogEvent

Tipo: `CBP.Logging.Models.LogEvent` (`CBP.Core/CBP/Logging/Models/LogEvent.cs`).

| Propiedad | Tipo | Emitida en evento | Propósito | Obligatoriedad | Restricción |
|-----------|------|:---:|-----------|:---:|-------------|
| `EventName` | `string` | Sí → `eventName` | Qué ocurrió | ✅ | Debe venir de `LoggingEvents`, nunca libre. |
| `Scope` | `string` | Sí → `scope` | Flujo funcional transversal | según capa | Debe venir de `LoggingScopes`. |
| `Message` | `string` | Sí (Message) | Descripción legible | ✅ | `{ }` se escapan como plantilla de Serilog. |
| `Args` | `object[]` | Sí (placeholder) | Argumentos del template | según uso | — |
| `Exception` | `Exception?` | Sí (`@Exception`) | Error capturado | según evento | — |
| `Properties` | `Dictionary<string,object?>` | Sí (uno por clave) | Dimensiones estructuradas | contextual | Claves deben venir de `LoggingPropertyNames` cuando existan. |

> **Nota de unificación (S16.4)**: `EventName` y `Scope` se emiten también como propiedades estructuradas (`eventName`, `scope`) para consultas uniformes. Antes de S16.4 únicamente quedaban en el campo modelo.

## 3. LoggingEvents — convención `Dominio_Acción`

Plantilla de `CBP.Core/CBP/Logging/LoggingEvents.cs`.

### Regla de naming
- Formato: `{Dominio}_{Accion}` en **PascalCase**, separado por **un guión bajo**.
- `Dominio`: `Cache`, `Login`, `Jwt`, `Password`, `Email`, `Event`, `Sql`, `Background`.
- `Accion`: `Hit`, `Miss`, `Set`, `Invalidation`, `Succeeded`, `Failed`, `Generated`, `Expired`, `Reset`, `Queued`, `Sent`, `Failed`, `Published`, `Handled`, `SlowQuery`, `JobStarted`, `JobFinished`.
- **Prohibido**: `CacheHit` (sin `_`), `cache.hit` (punto/minúscula), `CACHE_HIT` (mayúsculas), `Cache-hit` (guion).

### Catálogo actual
```csharp
Cache_Hit, Cache_Miss, Cache_Set, Cache_Invalidation
Login_Succeeded, Login_Failed, Jwt_Generated, Jwt_Expired, Password_Reset
Email_Queued, Email_Sent, Email_Failed
Event_Published, Event_Handled
Sql_SlowQuery
Background_JobStarted, Background_JobFinished
```

## 4. Categories — jerarquía oficial

Plantilla de `LoggingCategories.cs`.

| Categoría | Uso |
|-----------|-----|
| `data` | Padre raíz capa datos |
| `data.cache` | Operaciones de caché |
| `data.repository` | Acceso a repositorios |
| `data.sql` | Queries SQL |
| `application` | Padre raíz app |
| `application.auth` | Autenticación |
| `application.security` | Seguridad / MFA |
| `domain` | Padre raíz dominio |
| `domain.events` | Eventos de dominio |
| `infrastructure` | Padre raíz infraestructura |
| `infrastructure.email` | Emails |
| `webapi` | Capa WebAPI / HTTP |
| `background` | Background services |

**Regla de crecimiento**: máximo 2 segmentos (`a.b`); primer segmento es un dominio raíz estable; segundo segmento lowercase single-token. Consultas soportadas: `category startswith "data"`.

## 5. Sources

Plantilla de `LoggingSources`. Origen real de los datos (proveedor). NO confundir con `LoggingCacheResults`.

`memory, redis, sqlserver, api, queue, filesystem`

Extensible sin romper, pero **nunca valores inventados por el emisor** — agregar constante nueva al catálogo.

## 6. PropertyNames — estándar único (camelCase)

Plantilla de `LoggingPropertyNames`. Resuelve el hallazgo de S16.3 (mezcla de PascalCase/lowerCamel).

**Decisión**: **camelCase** para TODAS las propiedades. Compatible con JSON, OpenTelemetry, Elastic, Grafana, Seq y Azure Monitor.

| Constante | Valor camelCase |
|-----------|-----------------|
| `Category` | `category` |
| `Repository` | `repository` |
| `Operation` | `operation` |
| `Method` | `method` |
| `Source` | `source` |
| `CacheResult` | `cacheResult` |
| `Key` | `key` |
| `EventName` | `eventName` |
| `Scope` | `scope` |
| `Event` | `event` |
| `TenantId` | `tenantId` |
| `UserId` | `userId` |
| `CorrelationId` | `correlationId` |
| `RequestPath` | `requestPath` |
| `HttpMethod` | `httpMethod` |
| `ClientIp` | `clientIp` |
| `ElapsedMs` | `elapsedMs` |

> **Migración invertida conscientemente**: los enrichers HTTP de `LoggerService`/`LoggerServiceBase` emitían `"CorrelationId"/"UserId"/"ClientIp"/"RequestPath"/"HttpMethod"` (PascalCase). Unificado a las constantes camelCase (**Fase 2 S16.4**).
>
> `HttpCorrelationIdKey = "CorrelationId"` permanece como clave de transporte interno de `HttpContext.Items` (NO es una propiedad estructurada): el middleware guarda/lee bajo "CorrelationId"; el nombre estructurado emitido del evento es `correlationId`.

## 7. Propiedades obligatorias

| Propiedad | Obligatoria | Contexto |
|-----------|-------------|----------|
| `eventName` | ✅ siempre | Emitida automáticamente desde `LogEvent.EventName` |
| `scope` | ✅ siempre | Emitido automáticamente desde `LogEvent.Scope` |
| `category` | ✅ según capa | Auth→`application.auth`, repo→`data.repository`, caché→`data.cache`… |
| `repository` | según capa | Capa datos |
| `operation` | según capa | Capa datos — operación funcional estable de `LoggingOperations` |
| `method` | diagnóstico | Capa datos — nombre técnico real del método (`nameof(...)`) |
| `elapsedMs` | cuando aplica | Operaciones de cache/sql/IO medibles |
| `correlationId` | HTTP | Middleware + enricher de contexto HTTP |
| `tenantId` | multi-tenant | Si el evento pertenece a un tenant |
| `userId` | autenticación | Si hay usuario autenticado |

## 8. LoggingScopes — flujo funcional (NUEVO S16.4)

Plantilla de `LoggingScopes`. NO reemplaza Category: describe el **flujo de negocio transversal** que agrupa eventos de varias categorías técnicas.

`authentication`, `authorization`, `passwordPolicy`, `cache`, `email`, `domainEvents`, `persistence`, `sql`, `backgroundJobs`, `webApi`, `api`

Ejemplo de uso: consulta `Scope = authentication` reúne eventos de `application.auth`, `webapi` y `data.repository` en un mismo flujo de login, sin perder la organización técnica de `category`.

## 9. LoggingOperations — operación funcional (NUEVO S16.4)

Plantilla de `LoggingOperations`. Valores PascalCase (tokens de vocabulario, como `LoggingCacheResults`). Representan la **operación funcional estable** de la propiedad `operation`, independiente del nombre técnico del método (que va en `method`).

`Create, Update, Delete, Get, Authenticate, Authorize, Publish, Handle, Queue, Send, Execute, Invalidate, Refresh`

Ejemplo de separación operation/method:
```csharp
eventName = Cache_Hit
operation = LoggingOperations.Get          // estable → métricas/dashboards
method    = nameof(ObtenerActivasAsync)   // técnico → diagnóstico
repository = PoliticaPwdRepository
category  = data.cache
```
Los dashboards siguen consistentes aunque se refactorice el nombre interno del método.

## 10. Ejemplos

### Caché (repo)
```csharp
var ev = new LogEvent {
    EventName = LoggingEvents.CacheHit,
    Scope = LoggingScopes.Cache,
    Message = "Cache hit",
    Properties = {
        [LoggingPropertyNames.Category] = LoggingCategories.DataCache,
        [LoggingPropertyNames.Repository] = nameof(PoliticaPwdRepository),
        [LoggingPropertyNames.Operation] = LoggingOperations.Get,
        [LoggingPropertyNames.Method] = nameof(ObtenerActivaPorTenantAsync),
        [LoggingPropertyNames.Source] = LoggingSources.Memory,
        [LoggingPropertyNames.CacheResult] = LoggingCacheResults.Hit,
        [LoggingPropertyNames.Key] = "app:catalog:activas",
        [LoggingPropertyNames.TenantId] = tenantId,
        [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
    }
};
_logger.LogInformation(ev);
```

### JWT
```csharp
EventName = LoggingEvents.JwtGenerated, Scope = LoggingScopes.Authentication,
Category = application.auth, Operation = LoggingOperations.Authenticate, UserId = userId, TenantId = tenantId
```

### Login
```csharp
EventName = LoggingEvents.LoginSucceeded (o ...Failed), Scope = LoggingScopes.Authentication,
Category = infrastructure, Operation = LoggingOperations.Authenticate, CorrelationId, UserId, TenantId
```

### Background
```csharp
EventName = LoggingEvents.BackgroundJobFinished, Scope = LoggingScopes.BackgroundJobs,
Category = background, Key = jobName, Operation = LoggingOperations.Execute, ElapsedMs = elapsed
```

### Email
```csharp
EventName = LoggingEvents.EmailSent (o Queued/Failed), Scope = LoggingScopes.Email,
Category = infrastructure.email, Operation = LoggingOperations.Send, UserId = recipientId
```

### SQL
```csharp
EventName = LoggingEvents.SqlSlowQuery, Scope = LoggingScopes.Sql,
Category = data.sql, Operation = LoggingOperations.Execute, Method = commandName, ElapsedMs = ms
ElapsedMs > 5000
```

## 11. Directrices de implementación (Fase 3)

1. El enriquecimiento (HTTP, correlación, claims, headers) DEBE usar `LoggingPropertyNames` — nunca literales.
   ```csharp
   logEvent.Properties.TryAdd(LoggingPropertyNames.CorrelationId, GetCorrelationId(httpContext));
   logEvent.Properties.TryAdd(LoggingPropertyNames.UserId, GetUserId(httpContext));
   ```
2. `LogEvent.EventName` y `LogEvent.Scope` se persisten como propiedades estructuradas `eventName` y `scope` automáticamente (ver `WriteLog`).
3. `HttpContext.Items["CorrelationId"]` se mantiene como clave de transporte (constante `HttpCorrelationIdKey`). El evento estructurado lo expone como `correlationId`.
4. `operation` SIEMPRE proviene de `LoggingOperations`; el nombre técnico del método se expone por separado en `method`.

## 12. Validación automática (Fase 5)

En `CacheLogContractTests`:
- `Assert.True(properties.ContainsKey(LoggingPropertyNames.CorrelationId))`
- `Assert.False(properties.ContainsKey("CorrelationId"))` ← evita regresión PascalCase.
- Verificar `eventName`/`scope` emitidos como propiedades estructuradas.
- Verificar `operation` mediante `LoggingOperations` y `method` como `nameof(...)`.
- `Assert.True(properties.ContainsKey(LoggingPropertyNames.CorrelationId))`
- `Assert.False(properties.ContainsKey("CorrelationId"))` ← evita regresión PascalCase.
- Verificar `eventName`/`scope` emitidos como propiedades estructuradas.

## 12. Estado de la campaña (S16.4)

| Fase | Descripción | Estado |
|------|-------------|--------|
| F1 | Especificación (este doc) + catálogos `LoggingPropertyNames`/`LoggingScopes` | ✅ |
| F2 | Unificación a camelCase (enrichers) | ✅ |
| F3 | Enrichers usan `LoggingPropertyNames` (sin literales) | ✅ |
| F4 | Tests de contrato ampliados | ✅ |
| F5 | Instrumentación transversal (Auth/JWT/Password/Email/EventBus/SQL/Background/WebAPI) | ✅ cerrado (P1 Auth, P2 Security, P4 Email, P5 Background; P3 Events diferido; P6 cache-only) |

## 13. Congelación del contrato (v1.0)

A partir de S16.4 el contrato de logging de CBP **queda CONGELADO en v1.0** y
`CBP.Logging.Specification.md` pasa a ser la **especificación oficial del framework**.

### Reglas de cambio (contrato congelado)

1. **No añadir propiedades ad hoc** a `LogEvent` ni a los catálogos
   (`LoggingEvents`, `LoggingPropertyNames`, `LoggingScopes`,
   `LoggingCategories`, `LoggingOperations`, `LoggingSources`,
   `LoggingCacheResults`) sin pasar por este proceso.
2. Cualquier modificación futura requiere:
   - **nueva versión** de la especificación (v1.1, v2.0...) reflejada en el encabezado;
   - **compatibilidad hacia atrás** explícita (los consumidores con v1.0 no pueden romperse);
   - **registro de cambios** en la sección `## Cambios por versión` (nueva fila por versión),
     indicando qué propiedad/catálogo cambió y por qué.
3. La adición de un nuevo *valor* dentro de un catálogo existente (p.ej. un nuevo
   `Scope` o un nuevo `EventName`) **no** exige nueva versión de propiedad del
    contrato, pero **sí** actualización del catálogo y de la especificación
   ("cambios aditivos").
4. Romper el contrato (renombrar/eliminar una propiedad o valor existente) es
   un **cambio de breaking** y solo procede con versión mayor + aprobación
   arquitectónica.

### Cambios por versión

| Versión | Cambios | Fecha |
|---------|---------|-------|
| 1.0 | Contrato inicial S16.4 (camelCase, LoggingScopes, LoggingOperations, EventName/Scope de primer nivel) | S16.4 |

### Criterio de cierre S16

El sprint S16 (S16.1–S16.4) se declara **CERRADO** solo cuando los tres gates se cumplan:

| Gate | Criterio | Estado |
|------|----------|--------|
| A | `dotnet clean` + `dotnet build` sin errores | ✅ |
| B | `dotnet test` 70/70 | ✅ |
| C | Playwright E2E validando flujos reales (Login → MFA → JWT → Cache → Background Email → Logout) | ⏳ pendiente |