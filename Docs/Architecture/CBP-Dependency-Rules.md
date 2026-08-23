# CBP — Dependency Rules (Matriz normativa)

> **Estado**: VALIDADO (S24.0) — base para gates S24.1..S24.4
> **Fecha**: 2026-08-13
> **Tipo**: Especificación normativa de fronteras de dependencia del framework CBP
> **Fuente**: `Docs/Sprints/S23/S23-CBP-Dependency-Discovery.md` (S23, cerrado) + validación de realidad S24.0
> **Alcance**: los 27 proyectos de `D:\CODIGOS\CBP`

---

## 1. Executive Summary

S24.0 validó contra el código real (no contra documentación) cada `using` externo,
cada `ProjectReference` y cada uso de símbolos EF antes de codificar ninguna regla.
El resultado es esta matriz normativa: **reglas por Proyecto + Clasificación +
DependencyGraph + AllowedDependencies** — explícitamente NO es una blacklist de
paquetes ni de namespaces sin evidencia.

### Hallazgos de validación (S24.0)

| # | Hallazgo | Evidencia | Decisión para reglas |
|---|----------|-----------|----------------------|
| V1 | `CBP.Services.Async`: `using Microsoft.EntityFrameworkCore` en `AsynchronousServiceCollectionExtensions.cs:2` es **código muerto** (archivo solo usa `MS.DI` + `System.Reflection.Assembly`, leído íntegro) | archivo | Regla sobre **símbolos usados**, no sobre `using` declarados |
| V2 | `CBP.Services.Sync`: ídem en `SynchronousServiceCollectionExtensions.cs:2` | archivo leído (82 líneas) | ídem |
| V3 | `CBP.Data.Specifications\Core\SpecificationEvaluator.cs:1`: `using EF` **real** — usa extensión `Include` de EF Core (`current.Include(...)` sobre `IQueryable<T>`, líneas 19/22) | archivo leído (39 líneas) | EF es ALLOWED en DATA |
| V4 | `CBP.Data.Abstractions\IUnitOfWork.cs:1`: `using EF` real (F1, contrato `DbContext`) | archivo + S23 | ALLOWED en DATA |
| V5 | Datos sobresalientes: todos los demás `using EF` (33) están en capa DATA (Synchronous/Utilities/Asynchronous) | grep 35 apariciones | ALLOWED en DATA |
| V6 | Grafo directo de `ProjectReference` sin referencias a `PassPlat.*`; sin ciclos | grep csproj + Roslyn | Dirección CBP→PassPlat prohibida |

### Consecuencia para S24.2 (Architecture Tests)

Los tests Roslyn deben operar sobre **símbolos de tipos usados/expuestos**, no sobre
directivas `using`. Codificar `"no usando Microsoft.EntityFrameworkCore < CBP.Service"` sería
un **falso positivo** (V1/V2): la regla correcta es `"ningún tipo EF usado ni expuesto fuera de DATA"`,
que hoy cumple → verde real.

---

## 2. Clasificación de referencia (S23, cerrada)

| Categoría | Proyectos |
|-----------|-----------|
| **CORE** | `CBP`, `CBP.Results` |
| **CORE-CROSSCUTTING** | `CBP.Events`, `CBP.Caching.Abstractions` (provisional, sin movimiento físico) |
| **DATA** | `CBP.Data.Abstractions`, `CBP.Data.Asynchronous`, `CBP.Data.Synchronous`, `CBP.Data.Specifications`, `CBP.Data.Utilities` |
| **APPLICATION** | `CBP.Services.Abstractions`, `CBP.Services.Async`, `CBP.Services.Sync` |
| **INFRASTRUCTURE** | `CBP.Caching.{Memory,Redis,NCache}`, `CBP.MultiTenant`, `CBP.Security.Cryptography`, `CBP.Logging`, `CBP.Emails`, `CBP.Excel`, `CBP.Authentication.Abstractions`, `CBP.Authentication.JwtBearer`, `CBP.WebApi` |
| **UI / DEMO** | `CBP.WinFrms`, `LoginFlowDemo` |
| **TEST / EXE** | `CBP.Security.Password.Tests`, `ToastBenchmark` |

---

## 3. Matriz de dependencias VALIDADA (grafo directo real)

Extraído de los `.csproj` (grep `ProjectReference`) — directo, no transitivo:

| Proyecto | Dependencias directas (ProjectReference) |
|----------|-------------------------------------------|
| `CBP` | — (hoja) |
| `CBP.Results` | — (hoja) |
| `CBP.Events` | `CBP.Results`, `CBP` |
| `CBP.Caching.Abstractions` | — (hoja) |
| `CBP.Data.Abstractions` | `CBP.Results` |
| `CBP.Data.Asynchronous` | `CBP.Data.Abstractions` |
| `CBP.Data.Synchronous` | `CBP.Data.Abstractions` |
| `CBP.Data.Specifications` | `CBP.Data.Abstractions` |
| `CBP.Data.Utilities` | `CBP.Data.Abstractions` |
| `CBP.Services.Abstractions` | `CBP.Data.Abstractions` |
| `CBP.Services.Async` | `CBP.Services.Abstractions`, `CBP.Data.Asynchronous` |
| `CBP.Services.Sync` | `CBP.Services.Abstractions`, `CBP.Data.Synchronous` |
| `CBP.Caching.Memory` | `CBP.Caching.Abstractions` |
| `CBP.Caching.Redis` | `CBP.Caching.Abstractions` |
| `CBP.Caching.NCache` | `CBP.Caching.Abstractions` |
| `CBP.Authentication.Abstractions` | `CBP.Results` |
| `CBP.Authentication.JwtBearer` | `CBP.Authentication.Abstractions`, `CBP` |
| `CBP.MultiTenant` | `CBP.Results` |
| `CBP.Logging` | `CBP` |
| `CBP.WebApi` | `CBP.Results`, `CBP.Services.Abstractions` |
| `CBP.Security.Cryptography` | — (hoja) |
| `CBP.Emails` / `CBP.Excel` | — (hojas) |
| `CBP.WinFrms` | — (hoja) |
| `CBP.Security.Password.Tests` | `CBP.Security.Cryptography` |
| `LoginFlowDemo` | `CBP.Security.Cryptography` |
| `ToastBenchmark` | `CBP.WinFrms` |

---

## 4. Dependencias externas REALES por proyecto (S24.0, grep de usings)

| Proyecto | Usings externos (excluyendo `System.*` y `CBP.*`) | Clasificación |
|----------|--------------------------------------------------|---------------|
| `CBP.Events` | `Microsoft.Extensions.DependencyInjection` | CORE-CROSSCUTTING |
| `CBP.Caching.Abstractions` | `MS.DependencyInjection{,.Extensions}`, `MS.Logging{,.Abstractions}` | CORE-CROSSCUTTING |
| `CBP.Data.Abstractions` | `Microsoft.EntityFrameworkCore` | DATA |
| `CBP.Data.Synchronous` | `Microsoft.Data.SqlClient`, `MS.EFCore{,.Infrastructure,.Storage}`, `MS.DependencyInjection` | DATA |
| `CBP.Data.Asynchronous` | `Microsoft.Data.SqlClient`, `MS.EFCore{,.Infrastructure,.Storage}`, `MS.DependencyInjection` | DATA |
| `CBP.Data.Specifications` | `Microsoft.EntityFrameworkCore` (usado: `Include`) | DATA |
| `CBP.Data.Utilities` | `Microsoft.Data.SqlClient`, `MS.EFCore{,.Diagnostics,.Metadata.Builders,.Storage}`, `MS.Caching.Memory`, `MS.DependencyInjection`, `MS.Primitives` | DATA |
| `CBP.Services.Async` | `AutoMapper`, `FluentValidation{,.Results}`, `MS.DI`, `MS.EFCore` (**muerto**, V1) | APPLICATION |
| `CBP.Services.Sync` | `AutoMapper`, `FluentValidation{,.Results}`, `MS.DI`, `MS.EFCore` (**muerto**, V2) | APPLICATION |
| `CBP.Caching.Memory` | `MS.Caching.Memory`, `MS.DI` | INFRASTRUCTURE |
| `CBP.Caching.Redis` | `StackExchange.Redis`, `MS.DI` | INFRASTRUCTURE |
| `CBP.Caching.NCache` | `Alachisoft.NCache.Client`, `Alachisoft.NCache.Runtime.Caching`, `MS.DI` | INFRASTRUCTURE |
| `CBP.Authentication.Abstractions` | `MS.AspNetCore.Http`, `MS.DI`, `MS.Logging` | INFRASTRUCTURE (F8: FrameworkReference ASP.NET) |
| `CBP.Authentication.JwtBearer` | `MS.AspNetCore.Http`, `MS.Logging`, `MS.IdentityModel.Tokens` | INFRASTRUCTURE |
| `CBP.MultiTenant` | `MS.DI`, `MS.Hosting` | INFRASTRUCTURE |
| `CBP.Logging` | `MS.AspNetCore.Http`, `MS.Configuration`, `MS.DI`, `MS.Options`, `Serilog`, `Serilog.Events` | INFRASTRUCTURE |
| `CBP.Emails` | `MailKit.{Net.Smtp,Security}`, `MimeKit`, `MS.Configuration`, `MS.DI`, `MS.Logging`, `MS.Options` | INFRASTRUCTURE |
| `CBP.Security.Cryptography` | `Isopoh.Cryptography.Argon2` | INFRASTRUCTURE |
| `CBP.WebApi` | `MS.AspNetCore.{Builder,Http,Mvc...}`, `MS.AspNetCore.OpenApi`, `Microsoft.OpenApi`, `MS.DI`, `MS.Logging` | INFRASTRUCTURE |

---

## 5. REGLAS NORMATIVAS (objeto de los gates S24.1..S24.4)

### Regla D-01 — Dirección global
> Ningún proyecto CBP puede depender de `PassPlat.*`.

- **Validación**: grafo de `ProjectReference` + símbolos `PassPlat.*` en árboles de tipos usados/expuestos.
- **Estado real**: CUMPLE (0 dependencias CBP→PassPlat).

### Regla D-02 — Sin ciclos
> El grafo de dependencias (directas y transitivas) de CBP no contiene ciclos.

- **Validación**: `find_circular_dependencies(level: project)`.
- **Estado real**: CUMPLE (`hasCycles: false`).

### Regla D-03 — Acoplamiento por clasificación (núcleo de S24)

| Clasificación | Tecnologías ALLOWED (PackageReference) | Prohibido (por símbolo usado/expuesto) |
|---------------|-----------------------------------------|-----------------------------------------|
| **CORE** (`CBP`, `CBP.Results`) | — (hojas, 0 paquetes) | Todo lo demás (EF, SqlClient, ASP.NET, Serilog, Redis, AutoMapper, FluentValidation, PassPlat, UI) |
| **CORE-CROSSCUTTING** (`CBP.Events`, `CBP.Caching.Abstractions`) | Solo abstracciones `MS.Extensions.*` (`DependencyInjection`, `Logging.Abstractions`) | Implementaciones e infraestructura: EF, SqlClient, Serilog, Redis/NCache/Memory cache, MailKit, ASP.NET, AutoMapper, FluentValidation |
| **DATA** (5 proyectos) | `Microsoft.EntityFrameworkCore` (+ Relational), `Microsoft.Data.SqlClient`, `MS.Caching.Memory` (Utilities, interceptor/diagnostics), `MS.Extensions.*` | ASP.NET Core, Serilog, Redis, MailKit, AutoMapper, FluentValidation, PassPlat, UI |
| **APPLICATION** (3 proyectos) | `AutoMapper`, `FluentValidation`, `MS.Extensions.DependencyInjection` | **EF Core y SqlClient** (símbolos EF/SqlClient usados o expuestos), ASP.NET, Serilog, Redis, PassPlat, UI |
| **INFRASTRUCTURE** (11 proyectos) | Solo el proveedor de su responsabilidad (p. ej. Logging→Serilog, Caching.Redis→StackExchange.Redis, Emails→MailKit/MimeKit, Security→Isopoh.Argon2, Auth→IdentityModel+ASP.NET) + `MS.Extensions.*` | Proveedores ajenos a su responsabilidad (p. ej. Logging no usa Redis; Redis no usa MailKit) |
| **UI / DEMO / TEST** | Lo necesario para su función | Sin regla especial de paquete; respetan D-01/D-02 |

Nota V1/V2: `EF` en `CBP.Services.*` está como `using` muerto. La regla D-03 para APPLICATION se
evalúa por **símbolos de tipo usados o expuestos**, no por directivas `using`. Sin embargo, la
limpieza de esos 2 usings se registra como deuda de higiene (eliminar en sprint de mantenimiento,
no bloqueante para S24).

### Regla D-04 — API pública expuesta (strong typing de frontera)
> Un tipo público de un proyecto no puede exponer en su firma pública tipos de tecnologías
> prohibidas para su clasificación.

- **applies**: clases públicas, interfaces, delegados, métodos públicos, propiedades, genéricos.
- **Ejemplo S24**: `CBP.Data.Abstractions.IUnitOfWork` expone `DbContext` (F1) — permitido porque es DATA.
  `CBP.Services.*` NO puede exponer `IQueryable<T>` con tipos EF en firmas.
- **Validación**: recorrido de símbolos públicos → tipos de los parámetros/retornos/generics.

### Regla D-05 — FrameworkReference
> `FrameworkReference` de un proyecto solo puede ser el compatible con su clasificación.

- **Real hoy**: `Microsoft.AspNetCore.App` en `CBP.Authentication.Abstractions` (F8, hallazgo P1 documentado en S23) y en `CBP.WebApi`.
- **Decisión**: F8 se mantiene como hallazgo documentado (no se cambia en S24); la regla se codifica como
  `FrameworkReference ASP.NET permitido solo en INFRASTRUCTURE` (Auth.*, WebApi) — **prohibido en CORE/CORE-CROSSCUTTING/DATA/APPLICATION**.

### Regla D-06 — Dependencia transitiva no crea permiso tácito
> Que un proyecto referencie a otro que usa tecnología X (p. ej. APPLICATION → DATA → EF) **no** autoriza
> a APPLICATION a usar X. La tecnología requerida debe declararse como `PackageReference` del proyecto que la usa.

- **Validación**: análisis de símbolos transitivos — los tipos EF usados en una clase de APPLICATION reportan
  violación aunque el `PackageReference` directo no exista.

---

## 6. Pseudo-reglas descartadas por validación (anti-reglas)

| Anti-regla | Motivo de descarte |
|------------|--------------------|
| `CBP.Application no puede tener using EF` | Falso positivo V1/V2: el `using` existe pero ningún tipo es usado |
| `CBP.Core no puede tener package` | Deliberadamente NO: la regla es `0 paquetes en CBP/CBP.Results`, pero CORE-CROSSCUTTING admite abstracciones MS.* (necesario, p. ej. DI) |
| Blacklist por nombre de paquete global | V3/V4/V5: EF es permitido en DATA y prohibido en APPLICATION; una lista global no distingue contexto |
| `CBP.Data.Specifications` sin EF | Incorrecto: usa `Include` de EF Core (extensión), por lo tanto EF es ALLOWED en DATA |

---

## 7. Mapa de enforcement → gates

| Gate | Verifica | Herramienta |
|------|----------|-------------|
| **S24.1** | D-01, D-02 (grafo directo + ciclos) | Roslyn dependency graph |
| **S24.2** | D-03, D-04, D-05, D-06 (por símbolos usados/expuestos + PackageReference + FrameworkReference) | Roslyn Architecture Tests (NetArchTest o análisis personalizado) |
| **S24.3** | D-02 reiterado sobre el árbol completo (después de cualquier cambio futuro) | Roslyn |
| **S24.4** | Core Boundary: ningún símbolo fuera de CORE/CORE-CROSSCUTTING en Core; Core no referencia OUTSIDE | Roslyn + revisión de `ProjectReference` de `CBP`/`CBP.Results`/`CBP.Events` |

---

## 8. Anexo — Deudas de higiene no bloqueantes (S24)

| Deuda | Detalle |
|-------|---------|
| `using Microsoft.EntityFrameworkCore` muerto en `CBP.Services.Async\AsynchronousServiceCollectionExtensions.cs:2` | Eliminar en sprint de mantenimiento |
| `using Microsoft.EntityFrameworkCore` muerto en `CBP.Services.Sync\SynchronousServiceCollectionExtensions.cs:2` | Eliminar en sprint de mantenimiento |
| F1 — `Data.Abstractions.IUnitOfWork` expone `DbContext` | Documentado S23; revisión S25 (alternativas `IUnitOfWork<TContext>` sin EF explícito) |
| F8 — FrameworkReference ASP.NET en `Authentication.Abstractions` | Documentado S23 P1; decisión de arquitectura S25 |
| F3 — skew EF.Relational 10.0.9 vs 10.0.10 | Normalizar versiones (sprint resultado) |

---

## 9. Trazabilidad

| Doc | Rol |
|-----|-----|
| `S23-CBP-Dependency-Discovery.md` | Descubrimiento y clasificación (cerrado) |
| `CBP-Dependency-Rules.md` | **Este documento** — matriz normativa validada |
| AGENTS.md | Prohibido modificar en S23/S24 |
