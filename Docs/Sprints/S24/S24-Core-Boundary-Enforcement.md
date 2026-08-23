# S24 — Core Boundary Enforcement (GATE PASS)

> **Estado**: CLOSED / GATE PASS
> **Fecha**: 2026-08-13
> **Tipo**: Enforcement de reglas de dependencia del framework CBP + Architecture Tests
> **Base**: `S23-CBP-Dependency-Discovery.md` (cerrado) → `CBP-Dependency-Rules.md` (normativo, S24.0)
> **Alcance**: los 27 proyectos de `D:\CODIGOS\CBP`; tests en `PassPlat.CBP.Architecture.Test`

---

## 1. Executive Summary

S24 convierte el descubrimiento S23 (read-only) en **reglas ejecutables** con tests de
arquitectura automatizados. La clave del sprint fue la fase de **validación contra la realidad**
(S24.0) que evitó codificar reglas equivocadas: detectó que la "contaminación EF de Application"
reportada como F6 era en realidad **código muerto** (`using Microsoft.EntityFrameworkCore` en
2 archivos de DI sin uso de tipos EF), y confirmó que `CBP.Data.Specifications` SÍ usa EF
(extensión `Include`) de forma legítima.

| Gate | Resultado |
|------|-----------|
| S24.0 — Validación de reglas | ✅ Matriz normativa `CBP-Dependency-Rules.md` (reglas D-01..D-06) |
| S24.1 — Dependency Rules PASS | ✅ D-01 (0 PassPlat), D-02 (0 ciclos), D-03 (por clasificación) sobre grafo real |
| S24.2 — Architecture Tests | ✅ **47/47 PASS** (Reflection, 0 paquetes nuevos) |
| S24.3 — No Cycles gate | ✅ Roslyn `hasCycles:false` + test D-02 (DFS) |
| S24.4 — Core Boundary gate | ✅ Core aislado: 0 símbolos de fuera de CORE/CORE-CROSSCUTTING |
| Build | ✅ 0 errores (warnings NU1903 pre-existentes) |
| xUnit total | ✅ **134/134** (87 baseline S22 + 47 nuevos S24) |

---

## 2. Hallazgos de validación S24.0 (evidencia real)

| # | Hallazgo | Archivo | Impacto en reglas |
|---|----------|---------|-------------------|
| V1 | `using Microsoft.EntityFrameworkCore` = **muerto** (archivo solo usa MS.DI + System.Reflection) | `CBP.Service\CBP.Services.Async\AsynchronousServiceCollectionExtensions.cs:2` | Las reglas se evalúan por **símbolos usados/expuestos**, NO por directivas `using` |
| V2 | Ídem mirror | `CBP.Service\CBP.Services.Sync\SynchronousServiceCollectionExtensions.cs:2` | Ídem |
| V3 | `using EF` **real** — extensión `Include` de EF Core sobre `IQueryable<T>` (líneas 19/22) | `CBP.Data\CBP.Data.Specifications\Core\SpecificationEvaluator.cs:1` | EF es ALLOWED en DATA |
| V4 | `using EF` real — contrato `DbContext` (F1 S23) | `CBP.Data\CBP.Data.Abstractions\IUnitOfWork.cs:1` | EF es ALLOWED en DATA |
| V5 | 33 usings EF restantes todos en capa DATA (de 35 apariciones totales) | grep | EF es ALLOWED solo en DATA |
| V6 | `Error.cs:141` matchea `Microsoft.Data.SqlClient.SqlException` por **nombre de tipo en string** (sin referencia de tipo real) | `CBP.Core\CBP.Results\Errors\Error.cs:141` | No viola D-03: es mapeo de errores sin dependencia (diseño intencional) |

**Consecuencia metodológica**: un test "Application no puede tener `using EF`" habría producido
falsos positivos (V1/V2). Los tests S24.2 verifican **tipos usados y expuestos** vía reflection,
no directivas.

---

## 3. Entregables

| Entregable | Ruta |
|------------|------|
| Matriz normativa (reglas D-01..D-06) | `Docs/Architecture/CBP-Dependency-Rules.md` |
| Proyecto de tests de arquitectura | `PassPlat.CBP.Architecture.Test/` |
| Catálogo de ensamblados + clasificación | `PassPlat.CBP.Architecture.Test/CbpCatalog.cs` |
| Tests de reglas (47) | `PassPlat.CBP.Architecture.Test/DependencyBoundaryTests.cs` |
| Registro del proyecto en solución | `PassPlat.slnx` (+1 proyecto) |

---

## 4. Tests S24.2 — cobertura

| Regla | Tests | Resultado |
|-------|-------|-----------|
| D-01 — 0 dependencias CBP→PassPlat | `D01_...` (1) | ✅ |
| D-02 — grafo sin ciclos (DFS) | `D02_...` (1) | ✅ |
| D-03 CORE — sin EF/SqlClient/Serilog/Redis/ASP.NET/AutoMapper/FluentValidation/EPPlus | 2 | ✅ |
| D-03 CORE-CROSSCUTTING — sin tecnologías de infraestructura | 2 | ✅ |
| D-03 APPLICATION — sin EF/SqlClient/ASP.NET/Serilog/Redis | 3 | ✅ |
| D-03 DATA — sin Serilog/Redis/ASP.NET/AutoMapper (EF permitido) | 5 | ✅ |
| D-03 INFRASTRUCTURE — solo PassPlat prohibido | 11 | ✅ |
| D-04 CORE — API pública sin tipos prohibidos | 2 | ✅ |
| D-04 CORE-CROSSCUTTING — API pública limpia | 2 | ✅ |
| D-04 APPLICATION — API pública sin EF/Infra | 2 | ✅ |
| D-05 — sin FrameworkReference ASP.NET en CORE/DATA/APPLICATION | 12 | ✅ |
| D-05 — Auth.Abstractions/JwtBearer/WebApi SÍ ASP.NET | 3 | ✅ |
| D-06 — EF transitivo no crea acceso directo | 1 | ✅ |
| **Total** | **47** | **47/47 PASS** |

Técnica: `Assembly.LoadFrom` (metadata) para referencias; `GetExportedTypes` + firma de miembros
públicos para API expuesta. **0 dependencias NuGet nuevas** (decisión de usuario: reflection).

---

## 5. Gates

### S24.1 — Dependency Rules PASS
- D-01: 0 símbolos `PassPlat` en los 27 proyectos (grep) + test.
- D-02: Roslyn `find_circular_dependencies(project)` → `hasCycles:false`; test DFS confirma.
- D-03: extracción de usings externos reales por proyecto (20 con externos) coinciden con la matriz.

### S24.2 — Architecture Tests PASS
- Build 0 errores; 47/47 PASS (detalle §4).

### S24.3 — No Cycles gate PASS
- Roslyn: `hasCycles:false` (CBP.slnx, 24 proyectos cargados).
- Test D-02 confirma el mismo invariante por reflection sobre los 23 assemblies portables.

### S24.4 — Core Boundary gate PASS
- `CBP`/`CBP.Results`: hojas, 0 ProjectReference, 0 paquetes, 0 símbolos externos no-BCL.
- `CBP.Events`/`CBP.Caching.Abstractions` (CORE-CROSSCUTTING): única externa = abstracciones
  `Microsoft.Extensions.*` (DI, Logging.Abstractions) — permitido.
- 0 dependencias Core → App/Infra/UI/PassPlat.

---

## 6. Deudas no bloqueantes (trasladadas)

| Deuda | Detalle |
|-------|---------|
| `using EF` muerto ×2 | `AsynchronousServiceCollectionExtensions.cs:2`, `SynchronousServiceCollectionExtensions.cs:2` — eliminar en sprint de higiene |
| F1 | `IUnitOfWork<TDbContext>` expone `DbContext` (revisión S25) |
| F8 | FrameworkReference ASP.NET en `Authentication.Abstractions` (decisión S25) |
| F3 | Skew EF.Relational 10.0.9 vs 10.0.10 (normalizar) |
| F5 | NU1903 (Excel → System.Security.Cryptography.Xml 10.0.6) — pre-existente, no regresión |

---

## 7. Trazabilidad

| Doc/Archivo | Rol |
|-------------|-----|
| `S23-CBP-Dependency-Discovery.md` | Descubrimiento + clasificación (cerrado) |
| `CBP-Dependency-Rules.md` | **Normativo**: reglas D-01..D-06 validadas |
| `PassPlat.CBP.Architecture.Test/DependencyBoundaryTests.cs` | 47 tests automatizados |
| `PassPlat.CBP.Architecture.Test/CbpCatalog.cs` | Clasificación de ensamblados (punto único) |
| `PassPlat.slnx` | +`PassPlat.CBP.Architecture.Test.csproj` |
| `AGENTS.md` | Sin modificaciones en S24 |

---

## 8. Conclusiones

1. El Core de CBP queda **protegido por reglas ejecutables**: cualquier PR futuro que introduzca
   EF/SqlClient/Serilog/Redis/ASP.NET/PassPlat en Core (o infra en CORE-CROSSCUTTING) romperá
   los tests S24.2 en CI.
2. La regla D-06 (transitiva no crea permiso) queda codificada: APPLICATION no puede usar tipos
   EF aunque DATA los exponga.
3. S24 validó el enfoque "símbolos, no usings": las reglas codificadas reflejan la realidad y
   no castigan código muerto sin tocarlo (deuda de higiene separada).
4. S24 = CLOSED / GATE PASS. Siguiente candidato: S25 (F1/F8/F3 + higiene de usings).
