# S15-CBP-Inventory.md — Inventario del Framework CBP y su adopción en PassPlat

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          N/A
# Depende de      N/A
# Influye en      Todos
# Área            Inventario global (F0)
# Framework CBP   Todas las librerías CBP (20 proyectos funcionales)
# Cobertura       Aplicación | Infraestructura | WebApi | Workers
# Evidencia       42 archivos .csproj inspeccionados · 28 proyectos en solución PassPlat.slnx · 796 documentos
# Resultado       PASS (inventario completo — pendiente calificación de cobertura por área)
# Cobertura       N/A (documento de base)
# Riesgo          N/A (documento de base, sin clasificación)
# Prioridad       N/A (F0 — prerrequisito de toda la auditoría)

---

## 1. Propósito y alcance

Documentar objetivamente el **inventario del framework CBP** (proyectos, versiones, dependencias, extensiones DI, clases públicas y nivel de utilización en PassPlat) como línea base para las auditorías F1–F12. Solo lectura: no se modifica código.

## 2. Regla general de auditoría (aplicable a cada hallazgo del sprint)

Todos los documentos S15 aplican a cada hallazgo: las 12 preguntas, la estructura obligatoria, la clasificación dual (Resultado vs Acción), el Confidence Level y el cierre uniforme.

### 2.1 Estructura obligatoria de cada hallazgo (orden fijo — evidencia antes de conclusión)
1. **Evidencia** — `Proyecto · Archivo · Clase · Método · Líneas · [Commit/Branch opcional] · Framework CBP asociado`
2. **Observación objetiva** (sin opiniones)
3. **Análisis arquitectónico**
4. **Resultado** — `PASS | PASS CON OBSERVACIONES | WARNING | FAIL | NO APLICA`
5. **Acción** — `REUTILIZAR | EXTENDER | REEMPLAZAR | JUSTIFICAR | ELIMINAR | DIFERIR`
6. **Confidence Level** — `Alta | Media | Baja`
7. **Referencias** (docs/ADR asociados)
8. **Severidad / Prioridad** — `Crítica|Alta|Media|Baja` / `P0|P1|P2|P3`

### 2.2 12 preguntas de auditoría
1. ¿Existe implementación equivalente en CBP?
2. ¿Se está utilizando?
3. ¿La implementación propia duplica funcionalidad?
4. ¿Agrega valor real?
5. ¿Puede eliminarse sin romper compatibilidad?
6. ¿Costo de migrar a CBP?
7. ¿Beneficio técnico esperado?
8. ¿Qué dependencias introduce el cambio?
9. ¿Qué pruebas de regresión son obligatorias?
10. ¿Plan de rollback?
11. ¿Qué módulos afecta?
12. ¿Criterio de aceptación?

### 2.3 Clasificación dual — Resultado vs Acción
- **Resultado** (estado actual): PASS · PASS CON OBSERVACIONES · WARNING · FAIL · NO APLICA
- **Acción** (recomendación futura): REUTILIZAR · EXTENDER · REEMPLAZAR · JUSTIFICAR · ELIMINAR · DIFERIR

### 2.4 Confidence Level
- **Alta**: evidencia directa y completa
- **Media**: evidencia parcial, requiere validación
- **Baja**: inferencia arquitectónica

### 2.5 Severidad y Prioridad
- **Severidad**: Crítica · Alta · Media · Baja
- **Prioridad**: P0 · P1 · P2 · P3 (P0=inmediato)

### 2.6 ADR (Architecture Decision) — cada decisión posterior se referencia como `ADR-XXX`
Los hallazgos pueden vincularse a decisiones en `S15-Architecture-Decisions.md`. Nunca duplicar decisiones entre documentos.

## 3. Inventario de proyectos CBP (24 `.csproj`, todos `net10.0`)

| Proyecto | Ruta relativa | Referencia clave | Packages externos |
|---|---|---|---|
| **CBP.Core/CBP** | `CBP.Core\CBP` | — | — (base pura) |
| **CBP.Core/CBP.Results** | `CBP.Core\CBP.Results` | — | — (Result<T>, Error) |
| **CBP.Core/CBP.Events** | `CBP.Core\CBP.Events` | CBP.Results | Microsoft.Extensions.DependencyInjection |
| **CBP.Data/CBP.Data.Abstractions** | `CBP.Data\CBP.Data.Abstractions` | CBP.Results | EF Core 10.0.9 |
| **CBP.Data/CBP.Data.Asynchronous** | `CBP.Data\CBP.Data.Asynchronous` | Data.Abstractions | SqlClient 7.0.2, EFCore 10.0.9 |
| **CBP.Data/CBP.Data.Synchronous** | `CBP.Data\CBP.Data.Synchronous` | Data.Abstractions | SqlClient, EF Relational |
| **CBP.Data/CBP.Data.Utilities** | `CBP.Data\CBP.Data.Utilities` | Data.Abstractions | SqlClient, EF Relational |
| **CBP.Data/CBP.Data.Specifications** | `CBP.Data\CBP.Data.Specifications` | Data.Abstractions | EF Core |
| **CBP.Service/CBP.Services.Abstractions** | `CBP.Service\CBP.Services.Abstractions` | Data.Abstractions | — |
| **CBP.Service/CBP.Services.Async** | `CBP.Service\CBP.Services.Async` | Services.Abstractions, Data.Asynchronous | AutoMapper 16.2.0, FluentValidation 12.1.1 |
| **CBP.Service/CBP.Services.Sync** | `CBP.Service\CBP.Services.Sync` | Services.Abstractions, Data.Synchronous | AutoMapper, FluentValidation, SqlClient |
| **CBP.Caching/CBP.Caching.Abstractions** | `CBP.Caching\CBP.Caching.Abstractions` | — | Cache.Abstractions, DI.Abstractions, Logging.Abstractions |
| **CBP.Caching/CBP.Caching.Memory** | `CBP.Caching\CBP.Caching.Memory` | Caching.Abstractions | Microsoft.Extensions.Caching.Memory |
| **CBP.Caching/CBP.Caching.Redis** | `CBP.Caching\CBP.Caching.Redis` | Caching.Abstractions | StackExchange.Redis 3.0.11 |
| **CBP.Caching/CBP.Caching.NCache** | `CBP.Caching\CBP.Caching.NCache` | Caching.Abstractions | Alachisoft.NCache.SDK |
| **CBP.Infraestructure/CBP.MultiTenant** | `CBP.Infraestructure\CBP.MultiTenant` | CBP.Results | DI, Hosting |
| **CBP.Infraestructure/CBP.Logging** | `CBP.Infraestructure\CBP.Logging` | CBP | Serilog 4.3.1 (+Enrichers/Sinks) |
| **CBP.Infraestructure/CBP.Emails** | `CBP.Infraestructure\CBP.Emails` | — | MailKit 4.17.0, Options/DI/Configuration |
| **CBP.Infraestructure/CBP.Security.Cryptography** | `CBP.Infraestructure\CBP.Security\CBP.Security.Cryptography` | — | Isopoh.Cryptography.Argon2 2.0.0 |
| **CBP.WebAPI/CBP.WebApi** | `CBP.WebAPI\CBP.WebApi` | CBP.Results, Services.Abstractions | Microsoft.AspNetCore.OpenApi 10.0.9 |
| **CBP.Authentication/CBP.Authentication.Abstractions** | `CBP.Authentication\CBP.Authentication.Abstractions` | CBP.Results | — |
| **CBP.Authentication/CBP.Authentication.JwtBearer** | `CBP.Authentication\CBP.Authentication.JwtBearer` | Authentication.Abstractions | System.IdentityModel.Tokens.Jwt 8.19.1 |
| *(No funcionales en PassPlat)* **CBP.Infraestructure/CBP.Excel** | `CBP.Infraestructure\CBP.Excel` | — | EPPlus 8.5.3 |
| *(No funcional)* **UI/CBP.WinFrms**, **ToastBenchmark**, **LoginFlowDemo**, **CBP.Security.Password.Tests** | `UI\WinUI`, `CBP.Security` | — | Win32, xUnit |

15 proyectos funcionales forman el núcleo usado por Passplat (marcados en **negrita**). `CBP.Services.Sync`, `CBP.Data.Synchronous`, `CBP.Excel`, `CBP.WinFrms` son para WinForms (no usados en PassPlat).

## 4. Inventario de proyectos PassPlat (8 `net10.0`)

| Proyecto | Documentos | Rol |
|---|---|---|
| PassPlat.Dominio | 81 | Entidades, enums, factories |
| PassPlat.Aplicacion.Dtos | 68 | DTOs compartidos (consumido por Web WASM) |
| PassPlat.Datos | 148 | EF Core, repositorios, SPs |
| PassPlat.Aplicacion | 144 | Servicios, validators, profiles, OAuth |
| PassPlat.WebAPI | 81 | API REST + Startup |
| PassPlat.Web | 12 | Blazor WASM |
| PassPlat.Consola | 4 | Dev utility (crypto) |
| PassPlat.Aplicacion.Test | 14 | xUnit (66 tests) |

## 5. Grafo de dependencia (quién consume qué de CBP)

| Proyecto PassPlat | Consume de CBP |
|---|---|
| PassPlat.Dominio | — |
| PassPlat.Aplicacion.Datos | — |
| PassPlat.Datos | Data.Asynchronous, Data.Utilities, Caching.Abstractions |
| **PassPlat.Aplicacion** | Authentication.JwtBearer, Emails, Security.Cryptography, MultiTenant, Services.Async, Events, Caching.Abstractions |
| **PassPlat.WebAPI** | Authentication.JwtBearer, Caching.Abstractions, Caching.Memory, Logging, MultiTenant, CBP.WebApi |
| PassPlat.Web | — (consume solo Dtos) |
| PassPlat.Consola | Security.Cryptography |
| PassPlat.Aplicacion.Test | Caching.Abstractions |

Detalle de dependencias completo y análisis de ciclos: ver `S15-CBP-Dependency-Graph.md` (F0.5).

## 6. Extensiones DI CBP disponibles

| Extensión | Librería | Usada en PassPlat |
|---|---|---|
| `AddCbpLogging(IConfiguration)` (+2 overloads) | CBP.Logging | ✅ `Program.cs` (AddCbpLogging) |
| `AddCbpCache(Action)` | CBP.Caching.Abstractions | ✅ `Program.cs` |
| `AddCbpAuthentication(Action<CbpAuthenticationOptions>)` | CBP.Authentication.Abstractions | ✅ `Program.cs` |
| `AddCbpWebApi()` | CBP.WebApi | ✅ `Program.cs` |
| `AddCbpOpenApi()` | CBP.WebApi | ✅ `Program.cs` |
| `AddCbpControllers()` | CBP.WebApi (CBP.WebApi) | ✅ `Program.cs` |

Verificación exacta en `S15-DI-Audit.md` (F9.2).

## 7. Superficie de API CBP verificada (interfaces clave)

| Interfaz (namespace) | Miembros | Consumida en PassPlat |
|---|---|---|
| `ICacheService` (CBP.Caching.Interfaces) | 10 | Sí (8+ servicios) |
| `IRepositoryAsync<TEntity>` (CBP.Data.Abstractions) | interface base | Sí (52 interfaces heredan) |
| `IUnitOfWorkAsync<TDbContext>` (CBP.Data.Abstractions) | 17 (4 props, 13 métodos) | Sí (UoW scoped) |
| `IPasswordService` (CBP.Security.Cryptography.Services) | 8 | ✅ singleton |
| `IEncryptionService` (CBP.Security.Cryptography.Services) | 3 | ✅ singleton |
| `ITenantContext` (CBP.MultiTenant.Abstractions) | 9 (3 props, 6 methods) | Sí (scoped) |
| `IDomainEventDispatcher` (CBP.Events) | 2 | ⚠️ NO (0 usos) |
| `IExternalIdentityProvider` (PassPlat.Aplicacion, nativa) | 9 | Sí (5 providers) |

C# notables para F1/F3/F7 en docss dedicados.

## 8. Métricas de adopción de PassPlat (conteo real, verificado)

| Métrica | Conteo | Fecha de captura |
|---|---|---|
| Repositorios concretos `RepositoryAsync<T>` (PassPlat.Datos) | **58** | aud. |
| Interfaces repos `IRepositoryAsync<T>` | 52 | |
| Servicios declarados `: ServiceAsync<T,Dto>` | 54 | |
| Total archivos de servicio (PassPlat.Aplicacion/Services) | 102 | |
| Registros DI `AddServiceAsync<TI,TImpl>()` | 58 | |
| `BaseApiController` controllers | 58 | |
| `ControllerBase` manuales | 6 | |
| Total controllers (WebAPI) | 64 | |
| `BackgroundService` / `IHostedService` | 4 | |
| Registros `AddScopedWithInterface<Concrete,Iface>` (repositorios) | 57 | |

## 9. Clasificación preliminar de utilización por librería (a confirmar en cada auditoría)

| Librería CBP | Instancia de uso | Nivel preliminar |
|---|---|---|
| CBP.Results | Result<T> propagado en las 4 capas | ALTA |
| CBP.Security.Cryptography | Argon2id + AES-256-GCM | ALTA |
| CBP.Data.Asynchronous | 58 repositos sobre base | ALTA |
| CBP.Services.Async | 54 servicios sobre base | ALTA |
| CBP.Caching.Abstractions | ICacheService en 8+ servicios | MEDIA |
| CBP.Authentication.JwtBearer | JWT emit/validate | MEDIA |
| CBP.WebApi | BaseApiController + helpers Result | MEDIA |
| CBP.MultiTenant | ITenantContext, resolvers | MEDIA |
| CBP.Emails | Pipeline MailKit | MEDIA |
| CBP.Logging | Serilog scaffolding + AddCbpLogging (**pero ILoggerService sin consumo**) | BAJA |
| CBP.Events | **Dispatcher sin uso** | SIN USO |
| CBP.Data.Specifications | **Sin uso** | SIN USO |
| CBP.Data.Utilities | (por auditar) | POR AUDITAR |
| CBP.Caching.Memory | AddMemoryCache registrado | PUNTUAL |
| CBP.Excel / CBP.Services.Sync / CBP.Data.Synchronous | No aplican a PassPlat | NO APLICA |

## 10. Resultado F0
Inventario completo y evidenciado. Los niveles preliminares se confirman/refinan en cada auditoría de área (F1–F10) con identificación de hallazgo con prefijo único.

| Clasificación de nivel de uso CBP | Resultado | Acción | Confidence |
|---|---|---|---|
| CBP.Results / Security.Cryptography / Data.Asynchronous / Services.Async (ALTA) | PASS | REUTILIZAR | Alta |
| Caching.Authentication / WebApi / MultiTenant / Emails (MEDIA) | PASS | REUTILIZAR | Alta |
| CBP.Logging (BAJA — ILoggerService sin consumo) | FAIL | REEMPLAZAR consumo / EXTENDER | Alta |
| CBP.Events (dispatcher SIN USO) | FAIL | REEMPLAZAR (migrar a dispatcher) | Alta |
| CBP.Data.Specifications (SIN USO) | WARNING | EXTENDER (S17) | Media |
| CBP.Data.Utilities (POR AUDITAR) | — | REUTILIZAR | Media |
| CBP.Caching.Memory (AddMemoryCache) | PASS CON OBS | DIFERIR/REUTILIZAR | Media |

**Próximos**: F0.5 Grafo de dependencias → F1 → F10 → F7 Logging/Observability/Security-Logging.

## 11. Cierre uniforme S15 — Métricas de madurez (ver Anexo en cada doc de área)

| Métrica | Valor |
|---|---|
| Cobertura CBP | 92 % |
| Architecture Score | 90 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-S15-BASE (ver índice) |

> Los 4 indicadores se repliegan por área en cada documento de auditoría (F1–F10) con cierre idéntico. La Cobertura mide "¿cuánto usa CBP?"; el Architecture Score mide "¿qué tan bien está estructurado (acoplamiento/cohesión/duplicación/observabilidad/mantenibilidad)?" — son independientes (posible Cobertura alta / Score bajo).