# S15-DI-Audit.md — Dependency Injection Audit (F9.2)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          CBP-Inventory
# Depende de      Inventory
# Influye en      Services, WebApi, Certification
# Area            Inyeccion de dependencias (F9.2)
# Framework CBP   CBP.Services.Async, CBP.Data.Asynchronous, CBP.Caching, CBP.Logging, CBP.Authentication, CBP.MultiTenant, CBP.WebApi
# Cobertura       Aplicacion | Infraestructura | WebApi | Workers
# Evidencia       Program.cs (WebAPI) · AplicacionDependencyInjection.cs · DatosDependencyInjection.cs · 58 AddServiceAsync · 57 AddScopedWithInterface · 0 ciclos DI
# Resultado       PASS CON OBSERVACIONES
# Cobertura       92 % (ver F11)
# Riesgo          Medio
# Prioridad       Alta

---

## 1. Proposito

Auditar el registro, resolucion y ciclo de vida de todos los servicios en DI: Scoped/Singleton/Transient, factories, decorators, open generics, keyed services, validacion de dependencias, ciclos, servicios no registrados, servicios registrados sin uso y servicios instanciados manualmente con `new`. Especial atencion a BackgroundServices, HostedServices, Builders, Factories, Authentication, Email, Cache, MultiTenant.

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Puntos de registro DI en PassPlat

| Archivo | Rol |
|---|---|
| `PassPlat.WebAPI/Program.cs` | Host: CBP extensions, DbContext, UoW, Cache, HTTP clients, Password/Encryption, Tenant, HostedServices |
| `PassPlat.Aplicacion/AplicacionDependencyInjection.cs` | Todos los servicios (58 AddServiceAsync) + Auth pipeline + Email + OAuth |
| `PassPlat.Datos/DatosDependencyInjection.cs` | Todos los repositorios (57 AddScopedWithInterface + 2 AddScoped) |

## 4. Inventario de registros (conteo verificado)

| Tipo de registro | Conteo | Lifetime |
|---|---|---|
| `AddServiceAsync<TI,TImpl>()` (CBP.Services.Async) | **58** | Scoped (default) |
| `AddScopedWithInterface<Concrete,Iface>()` (repos) | **57** | Scoped (concrete + interface same instance) |
| `AddScoped` directos (WebAPI + Aplicacion) | ~30 | Scoped |
| `AddSingleton` | 12 | Singleton |
| `AddHostedService` | 4 | Singleton (hosted) |
| `AddTransient` | 0 | — |
| `AddKeyed` / `AddDecorator` / `Decorate` / `TryAdd*` | **0** | — |
| `AddHttpClient` named | 4 (OAuth.*) + 1 default | Transient (factory) |
| CBP extensiones (`AddCbp*`) | 6 | Según CBP |

## 5. Hallazgos

### 5.1 Lifecycles y posibles capturas de Scoped en Singleton

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **DI-001** | `AddSingleton(Log.Logger)` registra Serilog global como singleton (correcto) pero coexiste con `AddCbpLogging` que crea su propia pipeline. Dos rutas de logging activas. | `Program.cs:32` (AddCbpLogging) · `Program.cs:38` (AddSingleton Log.Logger) | WARNING |
| **DI-002** | `AddMemoryCache()` (Program.cs:149) + `AddCbpCache(UseLocal MemoryCacheProvider)` (Program.cs:150). **Dos caches de memoria coexistiendo**: la de Microsoft y la de CBP. Servicios que usan `IMemoryCache` directamente (MfaCodeStore, OAuthSessionStore legacy) no pasan por ICacheService. | `Program.cs:149-150` | REEMPLAZAR |
| **DI-003** | `IDistributedLockService` instanciado via factory con `new SqlDistributedLockService(connStr!)`. Correcto como factory (no usa IMemoryCache), pero ADO.NET manual fuera de CBP.Data (ver F4). | `Program.cs:142-145` | JUSTIFICAR (factory legitima) |
| **DI-004** | `IEncryptionService` y `IPasswordService` como Singletons con clave 32+ chars verificada en startup. Sin problema de lifecycle (stateless). | `Program.cs:187-198` | PASS |

### 5.2 Servicios registrados sin consumo o scaffolding muerto

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **DI-005** | `AddCbpLogging` registra `ILoggerService` de CBP.Logging pero **ningun consumidor lo usa** (todo el logging via Serilog/ILogger directamente). Scaffolding muerto. | `Program.cs:32`; grep `ILoggerService` en PassPlat: 0 usos | REEMPLAZAR |
| **DI-006** | `RequestDelegate` singleton con `static ctx => Task.CompletedTask` parece placeholder/incompleto. | `Program.cs:43` | JUSTIFICAR (revisar) |
| **DI-007** | `AddCbpAuthentication` registra pipeline completo de CBP (IAuthenticationOperator, middleware) — usado por middleware, pero la emision de JWT se hace con token issuers propios (ver F1). | `Program.cs:49` | JUSTIFICAR |

### 5.3 Duplicados / doble registro del mismo servicio

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **DI-008** | `IPassPlatPasswordSecurity` (PassPlat wrapper) y `CBP...IPasswordService` (CBP directa) cohabitan: **dos capas de password** — PassPlatPasswordSecurity compone CBP internamente (HashingService, ValidationService). Duplicacion de fachada sobre CBP. | `AplicacionDependencyInjection.cs` (AddSingleton IPassPlatPasswordSecurity) · `Program.cs:187` (IPasswordService) · `SPro/PassPlatPasswordSecurity.cs:35-53` | REEMPLAZAR |
| **DI-009** | `AddSingleton<IJwksStore, JwksStore>()` (AplicacionDI) usa `ICacheService` internamente; el comment legacy habla de `IDistributedCache`/`AddDistributedMemoryCache` que ya NO se registra. Comentario muerto. | `AplicacionDependencyInjection.cs` (comment FASE 17.1+17.6) | WARNING |

### 5.4 Servicios instanciados manualmente con `new` (bypass DI)

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **DI-010** | `PassPlatPasswordSecurity` construye manualmente `HashingService`, `ValidationService`, `GenerationService` de CBP.Security (composicion interna). Bypass de DI para componentes CBP. | `SPro/PassPlatPasswordSecurity.cs:35-53` | REEMPLAZAR |
| **DI-011** | `PassPlatEmailService.cs:296` instancia `new EmailService(emailSettings)` (CBP.Emails) manualmente en vez de resolverla de DI. Bypass de DI. | `Services/Email/PassPlatEmailService.cs:296` | REEMPLAZAR |
| **DI-012** | `EmailTemplateStoreService.cs:148` `new TemplateContext(_options)` — instancia de modelo, no servicio; aceptable. | `Services/Email/EmailTemplateStoreService.cs:148` | PASS |
| **DI-013** | Controllers resuelven `IEmailQueue` via `HttpContext.RequestServices.GetRequiredService<IEmailQueue>()` en vez de constructor (service locator). Anti-patron. | `WebAPI/Controllers/PasswordController.cs:99,143` | REEMPLAZAR |
| **DI-014** | `IdenExtTokensRotacionJob.cs:55-56` usa `scope.ServiceProvider.GetRequiredService<>()` dentro de BackgroundService — aceptable en hosted service con scope manual, pero debe documentarse. | `Services/IdenExtTokensRotacionJob.cs:55-56` | JUSTIFICAR (scope manual en hosted) |

### 5.5 BackgroundServices / HostedServices (4)

| Servicio | Registrado en | Lifetime scope |
|---|---|---|
| `EmailBackgroundService` | AplicacionDI (AddHostedService) | singleton, consume `IEmailQueue` (singleton) + `IServiceScopeFactory` |
| `IdenExtTokensRotacionJob` | AplicacionDI (AddHostedService) | singleton + scope manual |
| `SesionCleanupService` | Program.cs:223 | singleton |
| `PasswordExpirationBackgroundService` | Program.cs:224 | singleton |

Correcto: todos los BackgroundService son singleton por convencion de .NET y resuelven dependencias via scope factory. Verificacion en F7 (logging) de que no capturen Scoped.

### 5.6 Keyed services / Open generics / Decorators
- **0** keyed services, **0** decorators, **0** TryAdd. Los open generics (`AddGenericServiceAsync<TEntity,TDto>`) de CBP NO se usan — todos los servicios se registran explicitamente (58). Esto es una decision de explicitud valida, pero `AddServicesAsync(assembly)` (auto-scan) de CBP tampoco se usa. Insumo para F9/F12.

### 5.7 Servicios no registrados
Se busca uso de `GetRequiredService<X>` o inyeccion de interfaces sin registro. Hallazgos puntuales:
- DI-013: `IEmailQueue` SI registrada (singleton) — resuelta correctamente, solo anti-patron de service locator.
- `IPassPlatEmailService`, `IEmailAccountResolverService`, `IDashboardEnterpriseService`, `IBackgroundStatusService`, `IFederacionService`, `IOAuthCatalogValidationService`, `IExternalProviderValidator`, `IExternalLoginProviderService`, `IPermissionClaimBuilder`, `IAuthenticationTokenService`, `SessionManager`, `AuthenticationTokenIssuer` — todos registrados en AplicacionDI. **Sin hallazgos de no registrado.**

## 6. Uso de CBP en DI (adopcion)

| Capacidad CBP | Uso | Estado |
|---|---|---|
| `AddServiceAsync` (CBP.Services.Async) | 58 registros | ADOPTADO |
| `AddUnitOfWorkAsync` (CBP.Data) | 1 (WebAPI) | ADOPTADO |
| `AddCbpCache` + MemoryCacheProvider | 1 | ADOPTADO |
| `AddCbpAuthentication` | 1 | ADOPTADO |
| `AddCbpWebApi` / `AddCbpControllers` / `AddCbpOpenApi` | 1 c/u | ADOPTADO |
| `AddCbpLogging` | 1 | ADOPTADO pero sin consumo (DI-005) |
| `AddGenericServiceAsync` / `AddServicesAsync` (auto-scan) | 0 | NO USADO |
| `AddRepositoryAsync` (CBP.Data) | 0 (se usa AddScopedWithInterface propio) | REEMPLAZAR: el helper local duplica funcionalidad de CBP |

## 7. Duplicacion CBP vs local en DI

| Capacidad | En CBP | En PassPlat | Decision |
|---|---|---|---|
| Registro repositorios | `AddRepositoryAsync` (probable) | `AddScopedWithInterface<TConcrete,TInterface>` local | **REUTILIZAR** — el helper local es equivalente; migrar a CBP en S16 |
| Auto-registro servicios | `AddServicesAsync(assembly)` | manual explicito (58) | JUSTIFICAR (explicitud valida, aunque pierde descubrimiento automatico) |
| Cache | `AddCbpCache` | + `AddMemoryCache` redundante (DI-002) | REEMPLAZAR — eliminar AddMemoryCache |

## 8. Resultado F9.2
- **Adopcion alta**: 58 AddServiceAsync (CBP), 57 repos via helper local, 6 extensiones CBP activas.
- **Observaciones**: DI-002 (doble cache), DI-005 (ILoggerService muerto), DI-008/010/011 (fachadas que componen CBP manualmente), DI-013 (service locator en controllers).
- **No usados de CBP**: auto-scan de servicios, open generics, AddRepositoryAsync.
- Sin ciclos, sin keyed/decorators, sin servicios no registrados.

Insumo para F12: iniciativa de "limpiar doble cache", "migrar AddScopedWithInterface a CBP", "eliminar ILoggerService scaffolding", "inyectar EmailService/PassPlatEmailService por DI".

### 8.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| DI-001 | PASS CON OBS | JUSTIFICAR | Baja | P2 | Alta |
| DI-002 | FAIL | REEMPLAZAR (eliminar AddMemoryCache) | Media | P2 | Alta |
| DI-003 | PASS | JUSTIFICAR | Baja | P3 | Alta |
| DI-004 | PASS | REUTILIZAR | Baja | P3 | Alta |
| DI-005 | FAIL | EXTENDER/REEMPLAZAR consumo | Media | P1 | Alta |
| DI-008 | FAIL | REEMPLAZAR (adoptar CBP) | Media | P2 | Alta |
| DI-010 | WARNING | REEMPLAZAR (usar DI de CBP) | Baja | P2 | Alta |
| DI-011 | FAIL | REEMPLAZAR (inyectar EmailService) | Media | P1 | Media |
| DI-013 | WARNING | REEMPLAZAR (inyectar IEmailQueue en ctor) | Media | P1 | Alta |
| DI-014 | PASS | JUSTIFICAR (scope manual) | Baja | P3 | Media |

## 9. Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 92 % (AddServiceAsync/AddCbp*) |
| Architecture Score | 76 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-DI-001..014 |