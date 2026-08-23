# S23 — CBP Dependency Discovery & Architecture Classification

> **Estado**: DISCOVERY / DOCUMENTATION
> **Fecha**: 2026-08-13
> **Tipo**: Discovery + Classification + Inventory + Architectural Validation — READ-ONLY (sin cambios de código)
> **Alcance**: `D:\CODIGOS\CBP` (framework CBP) — análisis de los 27 proyectos reales

---

## 1. Executive Summary

Se auditó el framework `CBP` en `D:\CODIGOS\CBP` bajo un enfoque estrictamente **READ-ONLY**:
solo lectura de `.csproj`, `.slnx`, contenido fuente y grafo Roslyn. No se modificó ningún
archivo de código, proyecto, solución, configuración ni documentación existente.

Resultados clave:

| Métrica | Resultado |
|---------|-----------|
| Proyectos inventariados | **27** (24 en `CBP.slnx` + 3 fuera: `CBP.Security.Password.Tests`, `LoginFlowDemo`, `ToastBenchmark`) |
| Soluciones | **10** (`CBP.slnx` principal + 9 slnx internas) |
| Ciclos de dependencia (nivel proyecto) | **0** — `hasCycles: false` |
| Dependencias `CBP → PassPlat` | **0** `using PassPlat` en todo CBP (PassPlat → CBP permitido/esperado) |
| Contaminación externa en Core | **0** (sin EF/SqlClient/ASP.NET/Serilog/Redis/Azure/WinForms en `CBP`, `CBP.Results`, `CBP.Events`) |
| Paquetes externos en Core | **1** (`Microsoft.Extensions.DependencyInjection` 10.0.10 en `CBP.Events`) — REQUIRED |
| Hallazgos | **8** (F1..F8) — todos documentados **sin acción** en S23 |
| Clasificación `CBP.Caching.Abstractions` | `CORE-CROSSCUTTING` (provisional) — **sin movimiento físico** |

**Conclusión principal**: el Core de CBP (`CBP`, `CBP.Results`, `CBP.Events`) es arquitectónicamente
sano: agnóstico, sin ciclos y sin contaminación. Los hallazgos relevantes se concentran en el
acoplamiento de ciertas abstracciones a tecnologías concretas (EF Core en `Data.Abstractions`,
ASP.NET Core en `Authentication.Abstractions`) y en pequeñas inconsistencias de versiones.

---

## 2. Metodología

S23 es **DISCOVERY / DOCUMENTATION** (WRITE-ONLY sobre este documento). Reglas de ejecución:

- ❌ No modificar `.cs` · `.csproj` · `.slnx` · `AGENTS.md`
- ❌ No mover proyectos · cambiar namespaces · cambiar referencias · instalar/eliminar paquetes
- ❌ No ejecutar build/tests como parte del cambio
- ❌ No implementar F1/F2 · no mover `CBP.Caching.Abstractions`
- ✅ Solo lectura y clasificación, con evidencia del contenido real

Fuentes de evidencia:

1. **`.csproj`** — lectura de los 27: `ProjectReference`, `PackageReference`, `FrameworkReference`, `TargetFramework`, `OutputType`, `Compile Remove`, `NoWarn`.
2. **`.slnx`** — lectura de las 10 soluciones (agrupación de proyectos).
3. **Grafo Roslyn** — `find_circular_dependencies(level: project)` sobre el workspace (incluye PassPlat): grafo directo + detección formal de ciclos.
4. **Grep de usings** — inventario de `using` por proyecto para detectar contaminación (EF, SqlClient, ASP.NET, Serilog, Redis, PassPlat, WinForms).
5. **Contenido fuente** — verificación de tipos públicos (carga de responsibility real, no ubicación física).

Criterios de clasificación (por responsabilidad y dirección de dependencias, no por carpeta):

| Categoría | Definición |
|-----------|------------|
| CORE | Capacidad fundamental, 0 dependencias o solo otras hojas Core |
| CORE-CONTRACT | Contrato/leaf sin dependencias, consumido por Core |
| CORE-CROSSCUTTING | Capacidad transversal independiente de proveedores, con contratos agnósticos |
| DATA | Acceso a datos (abstracción y implementaciones) |
| APPLICATION | Servicios de aplicación (orquestación sobre datos) |
| INFRASTRUCTURE | Implementaciones concretas de proveedores externos |
| UI / DEMO | Interfaz o aplicaciones demostrativas |
| TEST / EXE | Proyectos de pruebas o ejecutables de demostración |

Reglas arquitectónicas de referencia (CBP-CORE-001..006):

- CORE-001: El Core es agnóstico — sin dependencias a PassPlat/WebAPI/EF/SQL/SMTP/Azure/UI/infra específica.
- CORE-002: Core → App/Infra/UI/PassPlat prohibido.
- CORE-003: Sin ciclos de dependencia en ningún nivel.
- CORE-004: Dependencias Core→Core solo si representan capacidad fundamental.
- CORE-005: Minimizar dependencias NuGet del Core (cada una se justifica REQUIRED/OPTIONAL/QUESTIONABLE/UNNECESSARY).
- CORE-006: Clasificar por responsabilidad y dirección de dependencias, no por ubicación física.

---

## 3. Baseline

Sprint de referencia cerrado:

| Sprint | Estado | Contrato |
|--------|--------|----------|
| S21 | ✅ CLOSED / GATE PASS | Outbox → IEventPublisher → EventDispatcher → NewIpDetectedEventHandler → IEmailQueue → EmailBackgroundService |
| S22 | ✅ CLOSED / GATE PASS | Refactor CBP.Events: `ICBPEvent`, `EventDispatcher`, `IEventDispatcher`, `AddCBPEvents()`; `CBP.Events → CBP.Results` RETAINED |

**Nota S22 post-cierre**: el archivo `IDomainEvent.cs` → `CBPEvent.cs` → `ICBPEvent.cs`
(rename post-cierre a `ICBPEvent` por convención de interfaz). Estado actual verificado:
`CBP.Core/CBP.Events/ICBPEvent.cs` existe y es el archivo fuente de la interfaz.

Backup pre-refactor: `C:\Users\Developer\AppData\Local\Temp\opencode\s22-backup\` (histórico).

Este documento **NO reabre** S21/S22: se limita a inventariar y clasificar el estado actual de CBP.

---

## 4. Inventory — 27 proyectos, 10 soluciones

### 4.1 Soluciones

| Solución | Proyectos | Ubicación |
|----------|-----------|-----------|
| `CBP.slnx` | 24 (principal) | `D:\CODIGOS\CBP\CBP.slnx` |
| `CBP.Authentication.slnx` | 2 | `D:\CODIGOS\CBP\CBP.Authentication\` |
| `CBP.Caching.slnx` | 4 | `D:\CODIGOS\CBP\CBP.Caching\` |
| `CBP.Core.slnx` | 3 (CBP.Events, CBP.Results, CBP) | `D:\CODIGOS\CBP\CBP.Core\` |
| `CBP.Data.slnx` | 5 + `../CBP.Core/CBP.Results` | `D:\CODIGOS\CBP\CBP.Data\` |
| `CBP.Infraestructure.slnx` | Emails, Excel, Logging, MultiTenant, Security | `D:\CODIGOS\CBP\CBP.Infraestructure\` |
| `CBP.Service.slnx` | 3 | `D:\CODIGOS\CBP\CBP.Service\` |
| `CBP.WebAPI.slnx` | 1 | `D:\CODIGOS\CBP\CBP.WebAPI\` |
| `UI\WinUI\WinUI.slnx` | 2 | `D:\CODIGOS\CBP\UI\WinUI\` |
| `UI\WinUI\CBP.WinFrms\CBP.WinFrms.slnx` | 1 | `D:\CODIGOS\CBP\UI\WinUI\CBP.WinFrms\` |

### 4.2 Proyectos

Todos SDK-style `net10.0` (excepciones: `CBP.WinFrms`/`ToastBenchmark` = `net10.0-windows`).
Compile implícito (sin `Compile Include`), con excepciones de `Compile Remove` en
`CBP.Emails` (11) y `CBP.WinFrms`.

| # | Proyecto | TFM | Output | Deps directas (Project) | Paquetes | FrameworkRef | Notas |
|---|----------|-----|--------|--------------------------|----------|--------------|-------|
| 1 | `CBP.Core\CBP` | net10.0 | lib | — | — | — | Contratos de logging |
| 2 | `CBP.Core\CBP.Results` | net10.0 | lib | — | — | — | Leaf |
| 3 | `CBP.Core\CBP.Events` | net10.0 | lib | CBP.Results, CBP | MS.DependencyInjection 10.0.10 | — | 11 fuentes |
| 4 | `CBP.Data\CBP.Data.Abstractions` | net10.0 | lib | CBP.Results | MS.EntityFrameworkCore 10.0.10 | — | IUnitOfWork con DbContext |
| 5 | `CBP.Data\CBP.Data.Asynchronous` | net10.0 | lib | Data.Abstractions, Results | MS.Data.SqlClient 7.0.2, EF 10.0.10, EF.Relational 10.0.10 | — | |
| 6 | `CBP.Data\CBP.Data.Synchronous` | net10.0 | lib | Data.Abstractions, Results | SqlClient 7.0.2, EF.Relational **10.0.9** | — | Skew F3 |
| 7 | `CBP.Data\CBP.Data.Specifications` | net10.0 | lib | Data.Abstractions, Results | EF 10.0.10 | — | |
| 8 | `CBP.Data\CBP.Data.Utilities` | net10.0 | lib | Data.Abstractions, Results | SqlClient 7.0.2, EF.Relational 10.0.10 | — | Interceptores EF |
| 9 | `CBP.Service\CBP.Services.Abstractions` | net10.0 | lib | Data.Abstractions, Results | — | — | IServiceAsync |
| 10 | `CBP.Service\CBP.Services.Async` | net10.0 | lib | Services.Abstractions, Data.Asynchronous, Data.Abstractions, Results | AutoMapper 16.2.0, FluentValidation 12.1.1 | — | |
| 11 | `CBP.Service\CBP.Services.Sync` | net10.0 | lib | Services.Abstractions, Data.Synchronous | AutoMapper, FluentValidation, SqlClient 7.0.2, EF.Relational **10.0.9** | — | Skew F3 |
| 12 | `CBP.Infraestructure\CBP.MultiTenant` | net10.0 | lib | CBP.Results | MS.DI 10.0.10, MS.Hosting 10.0.10 | — | |
| 13 | `CBP.Infraestructure\CBP.Logging` | net10.0 | lib | CBP | Serilog 4.4.0 + 8 sinks/enrichers | **Microsoft.AspNetCore.App** | Infra Serilog |
| 14 | `CBP.Infraestructure\CBP.Emails` | net10.0 | lib | — | MailKit 4.17.0, MS.Configuration/DI/Options 10.0.10 | — | 11 Compile Remove |
| 15 | `CBP.Infraestructure\CBP.Excel` | net10.0 | lib | — | EPPlus 8.5.3 | — | |
| 16 | `CBP.Infraestructure\CBP.Security\CBP.Security.Cryptography` | net10.0 | lib | — | Isopoh.Cryptography.Argon2 2.0.0 | — | |
| 17 | `CBP.Infraestructure\CBP.Security\CBP.Security.Password.Tests` | net10.0 | lib(test) | CBP.Security.Cryptography | xunit, Moq, coverlet, MS.NET.Test.Sdk | — | fuera de CBP.slnx |
| 18 | `CBP.Infraestructure\CBP.Security\LoginFlowDemo` | net10.0 | **Exe** | CBP.Security.Cryptography | — | — | demo, fuera de CBP.slnx |
| 19 | `CBP.Caching\CBP.Caching.Abstractions` | net10.0 | lib | — | MS.Caching.Abstractions, DI.Abstractions, Logging.Abstractions 10.0.10 | — | Contratos cache |
| 20 | `CBP.Caching\CBP.Caching.Memory` | net10.0 | lib | Caching.Abstractions | MS.Caching.Memory | — | |
| 21 | `CBP.Caching\CBP.Caching.Redis` | net10.0 | lib | Caching.Abstractions | StackExchange.Redis 3.1.13 | — | |
| 22 | `CBP.Caching\CBP.Caching.NCache` | net10.0 | lib | Caching.Abstractions | Alachisoft.NCache.SDK 5.3.7.2 | — | copia ncconf |
| 23 | `CBP.Authentication\CBP.Authentication.Abstractions` | net10.0 | lib | CBP.Results | — | **Microsoft.AspNetCore.App** | F2 |
| 24 | `CBP.Authentication\CBP.Authentication.JwtBearer` | net10.0 | lib | Auth.Abstractions, CBP, Results | System.IdentityModel.Tokens.Jwt 8.22.0 | **Microsoft.AspNetCore.App** | |
| 25 | `CBP.WebAPI\CBP.WebApi` | net10.0 | lib | Results, Services.Abstractions, Data.Abstractions | MS.AspNetCore.OpenApi 10.0.10 | **Microsoft.AspNetCore.App** | NoWarn NU1903 (F5) |
| 26 | `UI\WinUI\CBP.WinFrms` | net10.0-windows | **WinExe** | — | Vanara ×5 (5.0.4), MS.DI.Abstractions 10.0.9 | UseWindowsForms | legacy UI, Compile Remove masivo |
| 27 | `UI\WinUI\ToastBenchmark` | net10.0-windows | **WinExe** | CBP.WinFrms | — | — | demo |

---

## 5. Clasificación por evidencia

La clasificación se basa en **responsabilidad y dirección de dependencias**, no en ubicación física
(regla CBP-CORE-006).

```
CORE
├── CBP                     ← hoja (0 deps; contratos de logging)
└── CBP.Results             ← leaf (0 deps; Result/Error/ErrorType/CommonErrors)

CORE-CROSSCUTTING
├── CBP.Events              ← → [CBP.Results, CBP]; única externa MS.DI 10.0.10
└── CBP.Caching.Abstractions ← clasificación provisional (ver F2)

DATA
├── CBP.Data.Abstractions   ← contrato acoplado a EF Core (F1)
├── CBP.Data.Asynchronous
├── CBP.Data.Synchronous
├── CBP.Data.Specifications
└── CBP.Data.Utilities

APPLICATION
├── CBP.Services.Abstractions
├── CBP.Services.Async
└── CBP.Services.Sync

INFRASTRUCTURE
├── CBP.Logging             ← Serilog + ASP.NET App
├── CBP.MultiTenant
├── CBP.Security.Cryptography
├── CBP.Caching.Memory
├── CBP.Caching.Redis
├── CBP.Caching.NCache
├── CBP.Authentication.Abstractions
├── CBP.Authentication.JwtBearer
├── CBP.WebApi
├── CBP.Emails
└── CBP.Excel

UI / DEMO
├── CBP.WinFrms             ← net10.0-windows, Vanara
└── ToastBenchmark          ← demo WinExe

TEST / EXE
├── CBP.Security.Password.Tests
└── LoginFlowDemo           ← demo Exe
```

**Nota de clasificación**: `CBP.Authentication.Abstractions` y `CBP.WebApi` se sitúan en
INFRASTRUCTURE pese a declarar `FrameworkReference Microsoft.AspNetCore.App`; la capa
infraestructura legítimamente implementa sobre ASP.NET Core. El hallazgo F2 se refiere
específicamente a que *abstracciones* ("Abstractions") dependan del framework web.

---

## 6. Grafo de dependencias

### 6.1 Grafo directo (confirmado por csproj + Roslyn)

```
CBP ──► []
CBP.Results ──► []
CBP.Events ──► [CBP.Results, CBP]

CBP.Data.Abstractions ──► [CBP.Results]
CBP.Data.Asynchronous ──► [CBP.Data.Abstractions, CBP.Results]
CBP.Data.Synchronous ──► [CBP.Data.Abstractions, CBP.Results]
CBP.Data.Specifications ──► [CBP.Data.Abstractions, CBP.Results]
CBP.Data.Utilities ──► [CBP.Data.Abstractions, CBP.Results]

CBP.Services.Abstractions ──► [CBP.Data.Abstractions, CBP.Results]
CBP.Services.Async ──► [Services.Abstractions, Data.Asynchronous, Data.Abstractions, Results]
CBP.Services.Sync ──► [Services.Abstractions, Data.Synchronous]

CBP.Logging ──► [CBP]
CBP.MultiTenant ──► [CBP.Results]
CBP.Caching.Memory ──► [CBP.Caching.Abstractions]
CBP.Caching.Redis ──► [CBP.Caching.Abstractions]
CBP.Caching.NCache ──► [CBP.Caching.Abstractions]
CBP.Authentication.Abstractions ──► [CBP.Results]
CBP.Authentication.JwtBearer ──► [Auth.Abstractions, CBP, CBP.Results]
CBP.WebApi ──► [CBP.Results, CBP.Services.Abstractions, CBP.Data.Abstractions]

CBP.Emails ──► []
CBP.Excel ──► []
CBP.Security.Cryptography ──► []
```

### 6.2 Ciclos

`find_circular_dependencies(level: project)` → **`hasCycles: false`**. Sin ciclos directos ni
transitivos a nivel proyecto. El Core (CBP, CBP.Results, CBP.Events) es acíclico y todas las
flechas fluyen desde las capas superiores hacia las hojas.

---

## 7. Límite del Core — evaluación de reglas

| Regla | Evaluación | Estado |
|-------|-----------|--------|
| CORE-001 (Core agnóstico) | `CBP`/`CBP.Results` sin deps; `CBP.Events` solo MS.DI (abstracciones de contenedor) | ✅ Cumple |
| CORE-002 (Core→App/Infra/UI/PassPlat prohibido) | 0 `using PassPlat` en CBP; 0 referencias a App/Infra/UI desde Core | ✅ Cumple |
| CORE-003 (Sin ciclos) | `hasCycles: false` | ✅ Cumple |
| CORE-004 (Core→Core solo capacidad fundamental) | `CBP.Events → CBP.Results` (contrato de retorno) y `→ CBP` (logging contract) — RETAINED desde S22 | ✅ Cumple |
| CORE-005 (Minimizar NuGet en Core) | 1 paquete en Core: MS.DI 10.0.10 (CBP.Events) = REQUIRED (AddCBPEvents/IServiceCollection) | ✅ Cumple |
| CORE-006 (Clasificar por responsabilidad) | Aplicada en §5 | ✅ Cumple |

**Perímetro del Core vigente**: `CBP`, `CBP.Results`, `CBP.Events` (físico/lógico actual) +
`CBP.Caching.Abstractions` (clasificación provisional S23, sin movimiento).

---

## 8. Análisis especializado

### 8.1 `CBP` (Core común real)

Hoja sin `ProjectReference` ni `PackageReference`. Contiene únicamente contratos de logging:
catálogos (`LoggingScopes`, `LoggingCategories`, `LoggingEvents`, `LoggingOperations`,
`LoggingPropertyNames`, `LoggingSources`, `LoggingCacheResults`), `Configuration/LoggingOptions`,
interfaces (`ILoggerService`, `IContextProvider`, `IExceptionLogger`) y modelos
(`LogEvent`, `LogLevel`). Usings: solo `CBP.Logging.Models` (interno). **Es el núcleo común legítimo.**

### 8.2 `CBP.Results`

Leaf sin dependencias. Contiene `Result<T>`/`Result`, `Error`, `ErrorType`, `CommonErrors`,
`ResultExtensions`. Consumido por Events, Data.*, MultiTenant, Auth.*, WebApi y PassPlat.
**Posición Core correcta** (contrato de retorno transversal).

### 8.3 `CBP.Events`

Dependencias: `CBP.Results` + `CBP`; única externa `Microsoft.Extensions.DependencyInjection`
10.0.10 (REQUIRED — `AddCBPEvents()` sobre `IServiceCollection`). Contenido: `ICBPEvent`,
`IEventHandler<T>`, `IEventDispatcher`, `EventDispatcher`, `EventBase`, `IEventPublisher`,
`EventPublisher`, `CommonEvents`, `DispatchStrategy`, `EventDispatcherConfiguration`,
`EntityCreated/Updated/DeletedEvent<TEntity>`, `ServiceCollectionExtensions`, `PipelineExample`.

Usings verificados (único):

```
CBP.Events.Configuration
CBP.Logging            (scope=domainEvents, S16.4 frozen)
CBP.Logging.Interfaces
CBP.Logging.Models
CBP.Results / CBP.Results.Errors
Microsoft.Extensions.DependencyInjection
System / System.Collections.Concurrent / System.Reflection
```

**Sin contaminación** (no EF, no SqlClient, no ASP.NET, no Serilog directo, no Redis, no PassPlat,
no WinForms). Su posición cross-cutting es correcta tras S22.

### 8.4 `CBP.Data`

- **Paridad Async/Sync verificada 1:1**: `RepositoryAsync` (GetByIdAsync/GetAllAsync/ExistsAsync/
  FirstOrDefaultAsync/WhereAsync/AnyAsync/CountAsync) ↔ `RepositorySync` (GetById/GetAll/Exists/
  FirstOrDefault/Where/Any/Count). Los repositorios síncronos y asíncronos exponen las mismas
  operaciones, diferenciadas solo por el patrón async.
- `CBP.Data.Asynchronous` usings: `Microsoft.Data.SqlClient`, EF (`Infrastructure`, `Storage`),
  MS.DI, `System.Data` — sin dependencia a `CBP.Data.Synchronous` (paridad por contrato de
  `CBP.Data.Abstractions`).
- `CBP.Data.Utilities` contiene interceptores EF (`AuditingInterceptor`, `SoftDeleteInterceptor`),
  `SqlBulkWriter`, `CachingRepositoryDecorator`.
- **Hallazgo F1**: `IUnitOfWork.cs` (en `CBP.Data.Abstractions`) declara
  `where TDbContext : DbContext` — la abstracción de datos está acoplada a EF Core.

---

## 9. Dependencias externas — clasificación por proyecto

| Proyecto | Paquete | Clasificación | Justificación |
|----------|---------|---------------|---------------|
| CBP.Events | MS.DependencyInjection 10.0.10 | **REQUIRED** | `AddCBPEvents()`/`IServiceCollection` (registro DI) |
| Data.Abstractions | MS.EntityFrameworkCore 10.0.10 | **QUESTIONABLE** | `DbContext` en `IUnitOfWork` (F1) — abstracción acoplada al ORM |
| Data.Asynchronous | SqlClient 7.0.2, EF 10.0.10, EF.Relational 10.0.10 | **REQUIRED** | Implementación EF/SQL |
| Data.Synchronous | SqlClient 7.0.2, EF.Relational 10.0.9 | **REQUIRED** (con F3) | Implementación EF/SQL; versión desalineada |
| Data.Specifications | EF 10.0.10 | **REQUIRED** | Expresiones sobre IQueryable EF |
| Data.Utilities | SqlClient 7.0.2, EF.Relational 10.0.10 | **REQUIRED** | Interceptores/bulk sobre EF/SQL |
| Services.Abstractions | — | — | Sin paquete directo; EF llega transitivo vía Data.Abstractions (F6) |
| Services.Async | AutoMapper 16.2.0, FluentValidation 12.1.1 | **REQUIRED** | Mapeo y validación |
| Services.Sync | AutoMapper, FluentValidation, SqlClient 7.0.2, EF.Relational 10.0.9 | **REQUIRED** (con F3) | Implementación sync |
| MultiTenant | MS.DI 10.0.10, MS.Hosting 10.0.10 | **REQUIRED** | Registro DI/worker de contexto tenant |
| Logging | Serilog 4.4.0 + 8 sinks/enrichers | **REQUIRED** | Implementación del contrato ILoggerService |
| Emails | MailKit 4.17.0, MS.Configuration/DI/Options | **REQUIRED** | Cliente SMTP |
| Excel | EPPlus 8.5.3 | **REQUIRED** | Generación de Excel |
| Security.Cryptography | Isopoh.Cryptography.Argon2 2.0.0 | **REQUIRED** | Argon2id |
| Caching.Abstractions | MS.Caching/DI/Logging.Abstractions 10.0.10 | **REQUIRED** | Contracts de abstracción del contenedor |
| Caching.Memory | MS.Caching.Memory | **REQUIRED** | Proveedor Memory |
| Caching.Redis | StackExchange.Redis 3.1.13 | **REQUIRED** | Proveedor Redis |
| Caching.NCache | Alachisoft.NCache.SDK 5.3.7.2 | **REQUIRED** | Proveedor NCache |
| Authentication.JwtBearer | System.IdentityModel.Tokens.Jwt 8.22.0 | **REQUIRED** | Validación JWT |
| WebApi | MS.AspNetCore.OpenApi 10.0.10 | **REQUIRED** | OpenAPI |
| WinFrms | Vanara ×5 (5.0.4), MS.DI.Abstractions 10.0.9 | **REQUIRED** | P/Invoke Win32 UI |

Ninguna dependencia externa del Core se clasifica UNNECESSARY. La única QUESTIONABLE es
EF Core en `CBP.Data.Abstractions` (F1).

---

## 10. Hallazgos F1–F8

Todos los hallazgos quedan **documentados sin acción** en S23 (Discovery).

### F1 — `CBP.Data.Abstractions` acoplada a EF Core

- **Prioridad**: P1
- **Evidencia**: `IUnitOfWork.cs` usa `where TDbContext : DbContext` (IUnitOfWorkSync/Async); el proyecto declara `Microsoft.EntityFrameworkCore` 10.0.10.
- **Riesgo**: la abstracción de datos (contrato consumido por Application y WebApi) arrastra EF Core a todas las capas; impediría usar otro proveedor de datos sin tocar el contrato.
- **Estado S23**: documentado. Propuesta de desacople (interfaces sin `DbContext`, abstracción del UoW) queda para S24+ como candidato, no implementada.

### F2 — `CBP.Caching.Abstractions` (clasificación CORE-CROSSCUTTING provisional)

**Clasificación propuesta:** `CORE-CROSSCUTTING`
**Estado:** Clasificación aprobada para el Discovery; movimiento físico NO autorizado en S23.

**Evidencia:**

`CBP.Caching.Abstractions` contiene:

- `ICacheService`
- `ICacheMetrics`
- `CacheEntry`
- `CacheKeyGenerator`
- `CacheServiceBuilder`
- `ServiceCollectionExtensions`
- `AddCbpCache`

Dependencias internas CBP:

- 0

Dependencias externas:

- `Microsoft.Extensions.Caching.Abstractions`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`

Consumidores:

- `CBP.Caching.Memory`
- `CBP.Caching.Redis`
- `CBP.Caching.NCache`
- PassPlat

**Evaluación arquitectónica**

El proyecto contiene contratos y componentes de abstracción para una capacidad transversal de caching.
Las implementaciones concretas de Memory, Redis y NCache permanecen fuera de este contrato.

Por responsabilidad, `CBP.Caching.Abstractions` no constituye una implementación de infraestructura; define
la abstracción que consumen tanto las implementaciones como las aplicaciones.

Se clasifica provisionalmente como:

`CORE-CROSSCUTTING`

**Trade-off**

*CORE-CROSSCUTTING*

Ventajas:

- Mantiene el contrato de caching separado de sus implementaciones.
- Permite que cualquier componente Core utilice caching sin depender de infraestructura concreta.
- Mantiene simetría conceptual con `CBP.Events`.
- Las implementaciones Memory/Redis/NCache permanecen como infraestructura.

Costo:

- Amplía el perímetro de Core.
- Introduce dependencias sobre `Microsoft.Extensions.*.Abstractions`.
- Un eventual movimiento físico requiere revisar `.slnx`, `.csproj`, CI y consumidores.

*INFRASTRUCTURE-CONTRACT*

Ventaja:

- Mantiene un Core físico/lógico mínimo.

Costo:

- Un componente Core que necesite caching tendría que depender de un proyecto clasificado fuera de Core.
- Genera una separación asimétrica respecto de otras capacidades transversales como Events.

**Decisión S23**

Se adopta `CORE-CROSSCUTTING` como clasificación arquitectónica.

**NO se realiza movimiento físico durante S23.**

El eventual movimiento/reorganización de `CBP.Caching.Abstractions`
queda como candidato para un sprint posterior y requiere un análisis estructural
independiente.

No se modifica código, `.csproj`, `.slnx` ni referencias como consecuencia de esta clasificación.

> **Nota de fundamentación**: la justificación se basa en evidencia presente (responsabilidad actual,
> naturaleza y dirección de dependencias, independencia de proveedores, simetría con otras
> capacidades cross-cutting, posibilidad de mantener el Core libre de infraestructura). El beneficio
> "un componente Core pueda cachear en el futuro" es un beneficio potencial, no un argumento de decisión.

### F3 — Skew de versión `Microsoft.EntityFrameworkCore.Relational`

- **Prioridad**: P2
- **Evidencia**: `CBP.Data.Synchronous` y `CBP.Services.Sync` declaran `EF.Relational` **10.0.9**; `CBP.Data.Asynchronous` y `CBP.Data.Utilities` declaran **10.0.10**. Riesgo de resolución dual de ensamblados en el mismo grafo de una app que use ambas familias.
- **Estado S23**: documentado sin acción.

### F4 — `CBP.Emails` con 11 `<Compile Remove>`

- **Prioridad**: P2
- **Evidencia**: `CBP.Emails.csproj` excluye de la compilación: `WinFormsEmailService`,
  `EmailConfigurationBuilder`, `SendGridSettings`, `SmtpSettings`, `IEmailQueueService`,
  `IEmailSenderProvider`, `DisabledSendGridProvider`, `FakeProvider`, `MailKitProvider`,
  `SendGridProvider`, `MultiAccountEmailService` — código fuente físico sin compilar (léxico muerto).
- **Estado S23**: documentado sin acción.

### F5 — `CBP.WebApi` con `NoWarn NU1903`

- **Prioridad**: P3
- **Evidencia**: el proyecto suprime el warning NU1903 (vulnerabilidad en dependencia) a nivel de proyecto. Debe revisarse qué paquete lo dispara antes de silenciarlo.
- **Estado S23**: documentado sin acción.

### F6 — `CBP.Services.Abstractions` → `CBP.Data.Abstractions` (EF transitivo en Application)

- **Prioridad**: P3
- **Evidencia**: `IServiceAsync<T,TDto>` expone predicados `Expression<Func<TEntity,...>>` sobre
  entidades EF; `CBP.Services.Abstractions` referencia `CBP.Data.Abstractions`, arrastrando EF Core
  transitivamente a la capa Application.
- **Estado S23**: documentado sin acción.

### F7 — Contrato de logging en `CBP` (Core) e implementación Serilog en `CBP.Logging` (Infra)

- **Prioridad**: INFO
- **Evidencia**: catálogos/interfaces (`ILoggerService`, `LogEvent`, `LoggingScopes`, etc.) viven en
  `CBP`; la implementación Serilog (sinks, enrichers, ASP.NET App) vive en `CBP.Infraestructure\CBP.Logging`.
- **Valoración**: diseño correcto (abstracción en Core, implementación en Infra). Se documenta como
  patrón de referencia, no como defecto.

### F8 — `CBP.Authentication.Abstractions` declara `FrameworkReference Microsoft.AspNetCore.App`

- **Prioridad**: P1 (relacionado con F1)
- **Evidencia**: un proyecto llamado "Abstractions" depende del framework web ASP.NET Core
  (contiene `AuthenticationMiddleware`, `IAuthenticationOperator`, `AuthenticationResult`,
  `CbpAuthenticationOptions`).
- **Estado S23**: documentado sin acción (se recoge bajo el mismo criterio de F1: no desacoplar en S23).

---

## 11. Matriz CBP.* — origen → destino

| Origen | Destino directo |
|--------|-----------------|
| CBP | — |
| CBP.Results | — |
| CBP.Events | CBP.Results, CBP |
| CBP.Data.Abstractions | CBP.Results |
| CBP.Data.Asynchronous | Data.Abstractions, Results |
| CBP.Data.Synchronous | Data.Abstractions, Results |
| CBP.Data.Specifications | Data.Abstractions, Results |
| CBP.Data.Utilities | Data.Abstractions, Results |
| CBP.Services.Abstractions | Data.Abstractions, Results |
| CBP.Services.Async | Services.Abstractions, Data.Asynchronous, Data.Abstractions, Results |
| CBP.Services.Sync | Services.Abstractions, Data.Synchronous |
| CBP.MultiTenant | Results |
| CBP.Logging | CBP |
| CBP.Emails | — |
| CBP.Excel | — |
| CBP.Security.Cryptography | — |
| CBP.Caching.Abstractions | — |
| CBP.Caching.Memory | Caching.Abstractions |
| CBP.Caching.Redis | Caching.Abstractions |
| CBP.Caching.NCache | Caching.Abstractions |
| CBP.Authentication.Abstractions | Results |
| CBP.Authentication.JwtBearer | Auth.Abstractions, CBP, Results |
| CBP.WebApi | Results, Services.Abstractions, Data.Abstractions |

**Física vs lógica**: solo `CBP.Caching.Abstractions` tiene clasificación lógica (CORE-CROSSCUTTING)
distinta de su ubicación física (CBP.Caching). Todos los demás proyectos coinciden físicamente con su
responsabilidad lógica.

---

## 12. Compatibilidad S21/S22

- El pipeline Outbox (S21): `Outbox → IEventPublisher → EventDispatcher → NewIpDetectedEventHandler → IEmailQueue → EmailBackgroundService` **no se modificó ni se reanalizó**; se da por cerrado.
- El refactor CBP.Events (S22): `ICBPEvent`, `EventDispatcher`, `AddCBPEvents()`, `CBP.Events → CBP.Results` RETAINED — verificado presente y sin cambios.
- Contrato de logging congelado S16.4 (`LoggingScopes.DomainEvents`, `scope=domainEvents`): intacto en `CBP` (Core).
- Este documento no propone ninguna modificación que pueda impactar los contratos certificados.

---

## 13. Recomendaciones y sprints candidatos

S23 es Discovery; las recomendaciones NO se ejecutan en este sprint.

### S24 — Core Boundary Enforcement (candidato prioritario post-S23)

Convertir las reglas descubiertas en **governance verificable automáticamente**:

1. **Matriz oficial de dependencias CBP** — documento normativo origen→destino (§11 como base).
2. **Reglas arquitectónicas ejecutables**:

   - `CBP.Core.*` NO puede depender de: `Microsoft.EntityFrameworkCore`, `Dapper`, `Redis`, `SqlClient`, `Serilog`, `PassPlat.*`.
   - `CBP.Data.*` SÍ puede depender de: EF Core, SqlClient.
   - `CBP.Caching.*` SÍ puede depender de: Redis, MemoryCache.

3. **Roslyn Architecture Tests** — tests que fallen automáticamente ante violación:

   ```csharp
   [Fact] public void CBP_Core_Must_Not_Depend_On_EntityFramework() { ... }
   [Fact] public void CBP_Events_Must_Not_Depend_On_PassPlat() { ... }
   [Fact] public void No_Cycles_Are_Allowed() { ... }
   ```

4. **Dependency Governance** — `Docs/Architecture/CBP-Dependency-Rules.md` como documento normativo.
5. **Gates**: S24.1 Dependency Rules PASS · S24.2 Roslyn Architecture Tests PASS · S24.3 No Cycles PASS · S24.4 Core Boundary PASS → S24 CLOSED / GATE PASS.

### Candidatos posteriores (S25+)

- Desacople de `CBP.Data.Abstractions` de EF Core (F1): interfaces de UoW/Repo sin `DbContext`.
- Revisión de `CBP.Authentication.Abstractions` y su `FrameworkReference` ASP.NET (F8).
- Normalización `EF.Relational` a 10.0.10 (F3).
- Limpieza de `CBP.Emails` (F4) y revisión de `NoWarn NU1903` (F5).
- Movimiento físico de `CBP.Caching.Abstractions` a `CBP.Core` (requiere análisis estructural
  independiente: `.slnx`, `.csproj`, CI, documentación, scripts).

---

## 14. Conclusiones

1. El framework CBP presenta una arquitectura **sana y consistente**: 0 ciclos, Core agnóstico,
   0 dependencias `CBP → PassPlat` (y PassPlat → CBP esperado), paridad Async/Sync verificada.
2. `CBP`, `CBP.Results` y `CBP.Events` constituyen un Core válido que cumple las reglas CBP-CORE-001..006.
3. `CBP.Caching.Abstractions` se clasifica **CORE-CROSSCUTTING** (provisional, evidencia presente),
   **sin movimiento físico** en S23.
4. Los hallazgos F1/F2/F3 son los de mayor interés arquitectónico; quedan documentados sin acción.
5. El siguiente paso natural es **S24 — Core Boundary Enforcement** para convertir estas reglas en
   tests ejecutables que prevengan regresiones futuras.

---

## Anexo — Verificación de "no contaminación" (grep)

| Búsqueda | Resultado |
|----------|-----------|
| `using PassPlat` en `D:\CODIGOS\CBP` | **0 coincidencias** |
| `using System.Data.SqlClient` / `Microsoft.Data.SqlClient` en Core (CBP, Results, Events) | **0** |
| `using Microsoft.EntityFrameworkCore` en Core | **0** |
| `using Microsoft.AspNetCore` en Core | **0** |
| `using Serilog` en Core | **0** |
| `using StackExchange.Redis` en Core | **0** |

Los únicos usings externos en Core corresponden a `Microsoft.Extensions.DependencyInjection`
(`CBP.Events`) y BCL. El proyecto `CBP` (hoja) solo usa `CBP.Logging.Models` (interno).

---

*Documento generado bajo S23 — DISCOVERY / DOCUMENTATION (WRITE-ONLY). Sin cambios de código.*
