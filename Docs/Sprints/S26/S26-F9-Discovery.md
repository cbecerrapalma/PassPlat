# S26 — F9 Discovery: Desacoplamiento `PassPlat.Aplicacion → PassPlatDbContext`

| Campo | Valor |
|-------|-------|
| **Tipo** | Discovery (read-only) — sin implementación |
| **Sprint** | S26 |
| **Objetivo** | Determinar cómo eliminar el acceso EF directo de los 2 servicios de `PassPlat.Aplicacion` que inyectan `PassPlatDbContext` |
| **Fuente** | `S25-F1F6-Discovery.md`, `S25.1-Design.md`, `S25.2-Implementation-Certification.md` |
| **Depende de** | S25.2 (cerrado) |
| **Influye en** | F3, Detach, GetRepository/GetCustomRepository, F8, Query/IQueryable (deudas separadas) |
| **Estado** | ✅ Discovery completado — criterios de éxito definidos |

---

## 1. Contexto

Tras S25.2 (contrato agnóstico `IUnitOfWorkAsync`/`IUnitOfWorkSync` sin EF), el grep exhaustivo confirmó que `PassPlat.Aplicacion` queda con **solo 2 servicios** inyectando `PassPlatDbContext` directamente:

| Servicio | Archivo | Líneas EF |
|----------|---------|-----------|
| `FederacionService` (IFederacionService) | `PassPlat.Aplicacion\Services\BBDD\FederacionService.cs` | 3 consultas |
| `OAuthCatalogValidationService` (IOAuthCatalogValidationService) | `PassPlat.Aplicacion\Services\OAuth\OAuthCatalogValidationService.cs` | 2 consultas |

**Total: 5 consultas EF directas**, todas read-only (`AsNoTracking`).

---

## 2. Análisis consulta por consulta

### 2.1 `FederacionService.ObtenerEstadisticasAsync` (3 consultas)

| # | Consulta actual | Entidad | Repo existente | Cobertura |
|---|-----------------|---------|----------------|-----------|
| F-1 | `Set<IdenExt>().IgnoreQueryFilters().Where(IdTenant && !Eliminado).GroupBy(IdProvIden,Codigo,Nombre,Icono).Select(ProveedorEstadisticasDto).AsNoTracking()` | `IdenExt` | `IIdenExtRepository` | ❌ **No cubierta** — no existe método de agregación GroupBy + proyección. `ObtenerPorTenantAsync` devuelve entidades con Includes, sin agrupar. |
| F-2 | `Set<ProvIden>().CountAsync(p => p.Activo)` | `ProvIden` | `IProvIdenRepository` | ⚠️ Parcial — `ObtenerActivosAsync` existe (filtro `Activo` + `OrderBy Orden` + AsNoTracking) pero devuelve lista, no Count. |
| F-3 | `Set<AudIdenExt>().Where(IdTenant).OrderByDescending(FecEvento).Take(10).Select(UltimaActividadFederacionDto).AsNoTracking()` | `AudIdenExt` | `IAudIdenExtRepository` | ✅ **Casi exacta** — `ObtenerPorTenantAsync(idTenant, limite=50)` filtra por `IdTenant`, ordena por `FecEvento` desc, `Take(limite)`, `AsNoTracking`, `Include(ProvIden)`. Solo falta proyección (mapear en servicio) y pasar `limite=10`. |

### 2.2 `OAuthCatalogValidationService.ValidateAsync` (2 consultas)

| # | Consulta actual | Entidad | Repo existente | Cobertura |
|---|-----------------|---------|----------------|-----------|
| O-1 | `Set<ProvIden>().AsNoTracking().OrderBy(Orden)` | `ProvIden` | `IProvIdenRepository` | ✅ **Exacta** — `ObtenerTodosOrdenadosAsync()` (OrderBy Orden + AsNoTracking). Reusable directo. |
| O-2 | `Set<ConfProvIden>().AsNoTracking()` | `ConfProvIden` | `IConfProvIdenRepository` | ❌ **No cubierta** — no existe método "todos sin filtro". `ObtenerPorTenantAsync(idTenant)` exige tenant + hace Includes; `ObtenerConfiguracionAsync` exige tenant+provIden. |

---

## 3. Respuestas a las preguntas de diseño

### ¿EF solo se usa como mecanismo de consulta?
**Sí, 100%.** Las 5 consultas son lecturas con `AsNoTracking()` y sin `Include` en OAuth; FederacionService usa proyección directa a DTO. No hay escrituras, updates ni transacciones en ninguno de los 2 servicios. No se requiere `IUnitOfWork` — solo repos de lectura.

### ¿Reutilizar repos existentes o crear repos nuevos?
**Reutilizar.** Las 4 entidades ya tienen repos con interfaz registrados en DI:
- `IProvIdenRepository` → `DatosDependencyInjection.cs:77` (O-1 reusada tal cual; falta F-2 Count)
- `IConfProvIdenRepository` → `DatosDependencyInjection.cs:78` (falta O-2 "todos")
- `IIdenExtRepository` → `DatosDependencyInjection.cs:79` (falta F-1 GroupBy)
- `IAudIdenExtRepository` → `DatosDependencyInjection.cs:80` (F-3 reusada con `limite=10`)

**No se crean repos nuevos.**

### ¿Consultas especializadas necesarias?
**Sí, 3 métodos nuevos en repos existentes** (todos read-only, `AsNoTracking`, retornando `Result<T>`):

| Método nuevo | Repo | Firma sugerida | Origen |
|--------------|------|----------------|--------|
| `ObtenerDesglosePorProveedorAsync` | `IIdenExtRepository` | `Task<Result<IReadOnlyList<...>>>` con GroupBy por `IdProvIden/Codigo/Nombre/Icono` + `TotalVinculadas` | F-1 |
| `ContarActivosAsync` | `IProvIdenRepository` | `Task<Result<int>>` | F-2 |
| `ObtenerTodosAsync` | `IConfProvIdenRepository` | `Task<Result<IReadOnlyList<ConfProvIden>>>` (sin tenant, sin Includes) | O-2 |

> **Nota F-1**: la proyección `ProveedorEstadisticasDto` está en `PassPlat.Aplicacion.Dtos\Core\FederacionEstadisticasDto.cs`. La capa Datos NO puede referenciar DTOs de Aplicación (grafo: Aplicación → Datos → Dominio). Opciones: (a) repo devuelve entidades agrupadas crudas y el servicio proyecta; (b) `SPResults`-style DTO en Datos. **Decisión deferida a diseño S27.**

### ¿Comportamiento compartido entre los servicios?
**Sí, `ProvIden` se consulta en ambos** (F-2 Count activos; O-1 todos ordenados). Ambos pueden vivir en `IProvIdenRepository` (pueden reusar/consultar el mismo repo). `IdenExt` y `AudIdenExt` solo en FederacionService; `ConfProvIden` solo en OAuth. No requieren servicio compartido nuevo.

> **Observación**: O-1 (`ObtenerTodosOrdenadosAsync`) y F-2 (`ContarActivosAsync`) comparten el filtro `Activo`/Orden — ambos en `IProvIdenRepository`, contiguos, sin duplicación funcional.

### ¿caching / specifications?
- **Caching**: los datos de catálogo (`ProvIden`, `ConfProvIden`) son candidatos naturales a `ICacheService` (CBP.Caching), como ya se hizo en S16.3 para `PoliticaPwd`/`ConfigTenant`/`Apps`. **Opcional en S27 de diseño** — no bloquea el desacoplamiento (el contrato de repo no cambia con cache).
- **Specifications**: no aplican — son 3 consultas puntuales sin reuso motiva `ISpecification`. Mantener métodos directos en repos.

### ¿Impacto de `AsNoTracking`?
**Ninguno.** Todas las consultas ya usan `AsNoTracking()`. Los métodos nuevos deben preservarlo.

### ¿Dependencias que solo existen por inyectar `PassPlatDbContext`?
**Sí, ambas.** Solución actual:
- `FederacionService(PassPlatDbContext)` → debe inyectar `IIdenExtRepository` + `IProvIdenRepository` + `IAudIdenExtRepository`
- `OAuthCatalogValidationService(PassPlatDbContext)` → debe inyectar `IProvIdenRepository` + `IConfProvIdenRepository`

Con esto, `PassPlat.Aplicacion` queda **100% desacoplada de EF** (sin `using Microsoft.EntityFrameworkCore` en servicios). El `using PassPlat.Datos` ya existe en ambos (solo para el namespace de repos/entity) — NO se elimina el `using PassPlat.Datos` (los repos viven ahí), pero sí **desaparece el `using PassPlat.Datos` para `PassPlatDbContext`** (el tipo migra a `using PassPlat.Datos` sigue necesario por entities). Verificación post-refactor: solo se eliminan los `using Microsoft.EntityFrameworkCore`.

### Observación adicional: `IgnoreQueryFilters()` es no-op
El grep confirmó que **no existe ningún `HasQueryFilter`** en `PassPlat.Datos\Configurations`. Por tanto `FederacionService` L28 `.IgnoreQueryFilters()` es hoy un **no-op redundante** (no hay filters globales que ignorar). Al migrar a repo, el método puede omitirlo sin cambio de comportamiento — documentado como hallazgo de limpieza.

---

## 4. DI involucrado

| Servicio | Registro actual |
|----------|-----------------|
| `IFederacionService → FederacionService` | `AplicacionDependencyInjection.cs:141` (`AddScoped`) |
| `IOAuthCatalogValidationService → OAuthCatalogValidationService` | `AplicacionDependencyInjection.cs:148` (`AddScoped`) |

Los 4 repos ya están registrados como `AddScopedWithInterface` en `DatosDependencyInjection.cs:77-80` — **no se toca DI de repos**, solo los constructores de los servicios.

---

## 5. Criterios de éxito (para diseño S27)

1. **0 referencias a `PassPlatDbContext` en `PassPlat.Aplicacion`** (grep `PassPlatDbContext` → solo `Program.cs` y Datos).
2. **0 `using Microsoft.EntityFrameworkCore` en `PassPlat.Aplicacion\Services`**.
3. **Comportamiento SQL idéntico**: mismas consultas generadas (GroupBy/Count/OrderBy/Take/AsNoTracking), mismos DTOs de respuesta (`FederacionEstadisticasDto`, `CatalogValidationIssue`).
4. **Contrato `Result<T>` intacto** en repos y servicios.
5. **Build 0 errores, 0 warnings nuevas** · xUnit 87/87 · Architecture 56/56 sin romper.
6. F9 resuelto cuando el grep de F9 exhaustivo dé **0 matches** de `PassPlatDbContext` en Aplicación.

---

## 6. Límites y separación (deudas fuera de S26)

| Item | Estatus |
|------|---------|
| F3 — EF.Relational version skew (Async 10.0.11 vs Sync 10.0.9) | Fuera de alcance S26 |
| Detach — análisis de parity | Fuera de alcance S26 |
| GetRepository / GetCustomRepository — cleanup post-S25.2 | Fuera de alcance S26 |
| F8 — Authentication.Abstractions | Fuera de alcance S26 |
| Query/IQueryable — análisis posterior | Fuera de alcance S26 |

---

## 7. Evidencia

- `Docs\Architecture\S25.2-Implementation-Certification.md` — cierre de contrato (precede a F9)
- `PassPlat.Aplicacion\AplicacionDependencyInjection.cs:141,148` — registros de servicios F9
- `PassPlat.Datos\DatosDependencyInjection.cs:77-80` — registros de los 4 repos
- Greps F9 exhaustivos (esta sesión): solo 2 servicios, 5 consultas, 0 HasQueryFilter

---

**Hallazgos clave**: 5 consultas → 3 métodos nuevos en repos existentes (2 reusos directos); sin repos nuevos; sin DI nuevo para repos; `IgnoreQueryFilters` no-op; 2 dependencias `PassPlatDbContext` eliminables de Aplicación.

**Recomendación**: Diseño S27 (autorizado por usuario): F9 como objetivo único, separando F3/Detach/GetRepository/F8/Query.