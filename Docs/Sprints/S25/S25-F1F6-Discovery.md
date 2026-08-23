# S25 — Discovery F1+F6: Data Abstractions Decoupling

> **Tipo**: Discovery / Documentación (READ-ONLY — 0 cambios de código)
> **Estado**: ✅ DISCOVERY COMPLETE — pendiente decisión de alcance S25 por el usuario
> **Pre-condición**: S24 CLOSED/GATE PASS (2026-08-13). Tests de gobernanza S24 intactos (47/47).
> **Regla**: Ningún cambio en `CBP.Data.Abstractions` está autorizado por este documento. Solo se mapea evidencia y se evalúan opciones.

---

## 1. Objetivo

Mapear la deuda **F1** (`IUnitOfWork<TDbContext>` conoce `DbContext` → EF Core en el contrato de persistencia) y **F6** (`Services.Abstractions → Data.Abstractions → EF Core transitivo`) para decidir **si** y **cómo** desacoplarlas sin:

- crear una abstracción superficial acoplada a EF bajo otro nombre (advertencia explícita del usuario)
- romper los 47 architecture tests de S24 (gobernanza permanente)
- romper la paridad `CBP.Data.Asynchronous ↔ CBP.Data.Synchronous`

---

## 2. Método

- Búsqueda por tipos EF en contratos de `CBP.Data.Abstractions` (8 archivos).
- Inventario de consumidores de `IUnitOfWorkAsync<...>` / `IUnitOfWorkSync<...>` en los 6 consumidores objetivo.
- Rastreo de usos reales de cada miembro del contrato en todo el árbol `D:\CODIGOS\CBP` + `D:\CODIGOS\PassPlat`.
- Verificación de la cadena F6 en `.csproj` (Services.Abstractions/Async, Data.Asynchronous).

---

## 3. Hallazgos

### D-1 — El único EF real en el contrato está en `IUnitOfWork.cs`

De los 8 archivos de `CBP.Data.Abstractions`, **solo `IUnitOfWork.cs`** tiene `using Microsoft.EntityFrameworkCore;`. Apariciones reales de tipos EF en firmas públicas:

| Ubicación | Tipo EF | Forma |
|-----------|---------|-------|
| `IUnitOfWork.cs:9` | `DbContext` | constraint `where TDbContext : DbContext` (Sync) |
| `IUnitOfWork.cs:11` | `TDbContext` | propiedad `TDbContext DbContext { get; }` (Sync) |
| `IUnitOfWork.cs:43` | `DbContext` | constraint `where TDbContext : DbContext` (Async) |
| `IUnitOfWork.cs:45` | `TDbContext` | propiedad `TDbContext DbContext { get; }` (Async) |

**Falsos positivos descartados** (aparecen pero NO son EF):

- `IQueryable<TEntity> Query()` en `IReadRepositoryAsync.cs:9` / `IReadRepositorySync.cs:9` → es `System.Linq`, BCL. No EF.
- `Expression<Func<TEntity, ...>>` en `IReadRepository*` / `IPaginationRepository` / `PaginationOptions` → `System.Linq.Expressions`, BCL. No EF.
- `DbSet`, `ChangeTracker`, `AsNoTracking` → solo aparecen en **comentarios XML**, no en firmas.

> **Conclusión D-1**: La fuga EF del contrato se reduce EXACTAMENTE a 4 líneas en `IUnitOfWork.cs` (2 constraints + 2 propiedades). Todo lo demás del contrato es BCL o tipos propios (`RawParameter`, `SpResult<T>`, `Result<T>`).

### D-2 — La propiedad `DbContext` NO tiene ningún consumidor

Búsqueda de `.DbContext` (y variantes `_uow.DbContext`, `uow.DbContext`, `work.DbContext`, etc.) en todo el árbol CBP + PassPlat:

- **0 consumidores** de la propiedad expuesta por el contrato.
- La propiedad solo se implementa en `UnitOfWorkAsync.cs:23` y `UnitOfWorkSync.cs:23` (`public TDbContext DbContext => _context;`).
- Ningún servicio, repositorio, controlador o job de PassPlat accede al `DbContext` **a través del UoW**.

> **Conclusión D-2**: La exposición de `TDbContext DbContext` en el contrato es **dead surface**. Los consumidores inyectan `PassPlatDbContext` directamente (59 repositorios con `PassPlatDbContext` en ctor), nunca vía `uow.DbContext`.

### D-3 — Qué usa realmente cada consumidor del UoW

| Consumidor | Usos reales | Miembros del contrato usados |
|------------|-------------|------------------------------|
| `CBP.Data.Asynchronous` (impl) | implementa contrato | todos (impl) |
| `CBP.Data.Synchronous` (impl) | implementa contrato | todos (impl) |
| `CBP.Services.Abstractions` | **0** referencias a `IUnitOfWork` | — |
| `CBP.Services.Async` / `Sync` | **0** referencias a `IUnitOfWork` | — |
| `PassPlat.Datos` (11 repos SP) | inyecta `IUnitOfWorkAsync<PassPlatDbContext>` | `RawQuery` (SPs), `SaveChangesAsync` |
| `PassPlat.Aplicacion` (49 sitios) | inyecta `IUnitOfWorkAsync<PassPlatDbContext>` | `SaveChangesAsync` (commit desde consumer) |
| `PassPlat.WebAPI` (72 sitios) | inyecta `IUnitOfWorkAsync<PassPlatDbContext>` | `SaveChangesAsync`, `ExecuteInTransactionAsync` (1 uso: `DispConfiablesController.cs:88`) |

**Uso de miembros específicos (todo el árbol):**

| Miembro del contrato | Consumidores externos |
|----------------------|----------------------|
| `RawQuery` | 14 (11 repos PassPlat + 2 controllers + 1 job) |
| `SaveChangesAsync` / `SaveEntitiesAsync` | 124 call-sites (Aplicacion + WebAPI) |
| `ExecuteInTransactionAsync` | 1 (`DispConfiablesController.cs:88`) |
| `Begin/Commit/Rollback Transaction` | 0 (solo internos de impl) |
| `HasChanges` / `RejectChanges` | 0 externos |
| `GetRepository<TEntity>` | solo extensiones de servicio (patrón espejo DI) |
| `GetCustomRepository<TRepository>` | 0 externos (reemplazado por DI) |
| `DbContext` | **0** |

> **Conclusión D-3**: Los consumidores usan el UoW para **3 responsabilidades**: (1) SPs vía `RawQuery`, (2) commit vía `SaveChangesAsync`, (3) una transacción atómica puntual. La propiedad `DbContext` y las operaciones de transacción explícitas son superficie no consumida.

### D-4 — Cadena F6 verificada en .csproj

```
Services.Abstractions.csproj → Data.Abstractions.csproj   (directo)
Services.Async.csproj        → Data.Abstractions + Data.Asynchronous
Data.Asynchronous.csproj     → Data.Abstractions
Data.Abstractions            → EF Core (SOLO vía IUnitOfWork.cs)
```

- `CBP.Services.Abstractions` referencia `CBP.Data.Abstractions` para `IRepositoryAsync<TEntity>` / `IRepositorySync<TEntity>` (base de `IServiceAsync`/`IServiceSync`).
- El EF transitivo de F6 existe **únicamente** porque `Data.Abstractions` carga `Microsoft.EntityFrameworkCore` en `IUnitOfWork.cs`.

> **Conclusión D-4**: F6 NO es una dependencia de contrato de servicio — es el residuo transitivo de F1. **Si F1 se resuelve, F6 desaparece automáticamente.** No hay trabajo adicional de F6 más allá del de F1.

### D-5 — Los 47 tests de gobernanza S24 confirman la superficie actual

- Los tests S24 (D-01..D-06) evalúan acoplamiento por **símbolos usados/expuestos** (reflection), no por directivas `using`.
- D-03 clasifica `CBP.Data.Abstractions` como DATA (EF permitido) → los tests S24 **no bloquean** la dependencia EF actual.
- Cualquier cambio en `IUnitOfWork.cs` **sí** alterará la superficie pública del ensamblado y debe validarse contra los tests S24 (que comparan firmas públicas).

---

## 4. Opciones de desacoplamiento (F1)

> Regla del usuario: no asumir cómo desacoplar. Se listan opciones con evidencia; **la decisión es del usuario.**

### Opción A — Minimal: eliminar la propiedad `TDbContext DbContext` del contrato

| Aspecto | Detalle |
|---------|---------|
| Cambio | Quitar `TDbContext DbContext { get; }` de `IUnitOfWorkSync` y `IUnitOfWorkAsync` |
| Impacto | **0 call-sites** (D-2: nadie la usa) |
| Riesgo | Ninguno funcional |
| ¿Resuelve F1? | **Parcial**. Sigue la constraint `where TDbContext : DbContext` en la firma |
| ¿Superficial? | No — elimina dead surface real sin equivalente EF renombrado |

### Opción B — Contract-level: quitar también la constraint `where TDbContext : DbContext`

| Aspecto | Detalle |
|---------|---------|
| Cambio | Además de la propiedad, quitar la constraint del genérico en la interfaz |
| Impacto | El genérico `TDbContext` queda sin constraint en la interfaz; la implementación `UnitOfWorkAsync<TDbContext>` conserva su propia constraint |
| Efecto | La firma pública de `CBP.Data.Abstractions` deja de nombrar `DbContext` → **EF desaparece del contrato** |
| Riesgo | Genérico sin constraint en interfaz = semántica débil; `IUnitOfWorkAsync<TDbContext>` ya no garantiza que `TDbContext` sea un DbContext |
| ¿Resuelve F1? | **Sí** (contract deja de conocer EF) |
| ¿Superficial? | ⚠️ Riesgo de "abstracción acoplada con otro nombre": el genérico sigue siendo `TDbContext`, solo pierde la garantía de tipo. No añade valor real si la implementación es 100% EF |

### Opción C — Rename + relocación del genérico (NO recomendada, anti-patrón)

| Aspecto | Detalle |
|---------|---------|
| Cambio | Renombrar `TDbContext` → `TUnitOfWorkContext` o similar, sin quitar EF |
| Riesgo | **Exactamente el anti-patrón que el usuario prohibió**: abstracción acoplada a EF con otro nombre |
| ¿Resuelve F1? | No |

### Opción D — RETAIN + documentar (F1 aceptada como limitación conocida)

| Aspecto | Detalle |
|---------|---------|
| Cambio | Ninguno. Documentar que `IUnitOfWork<TDbContext>` es intencionalmente EF-typed |
| Justificación | El UoW expone `DbContext` porque los consumidores quieren "un DbContext como unidad de trabajo" (patrón EF). La capa DATA es, por clasificación S24, la única capa con derecho a EF |
| ¿Resuelve F1? | No (queda como deuda) |
| Costo | Mínimo. F6 permanece transitivo |

### Opción E — Interfaz no genérica + fábrica (desacople real)

| Aspecto | Detalle |
|---------|---------|
| Cambio | `IUnitOfWorkAsync` sin genérico, exponiendo solo `RawQuery`, `SaveChangesAsync`, transacciones, repositorios vía `IRepositoryFactory` |
| Impacto | Los 132 call-sites (`IUnitOfWorkAsync<PassPlatDbContext>` → `IUnitOfWorkAsync`) + DI registration |
| ¿Resuelve F1? | **Total** — el contrato no conoce EF, ni siquiera por genérico |
| Riesgo | Cambio de superficie grande (124+ call-sites en PassPlat), re-registro DI, re-validación S24 |
| ¿Superficial? | No — es el desacople real, pero el mayor blast radius |

---

## 5. Decisión pendiente del usuario

> Este documento NO autoriza cambios. Se requiere decisión explícita entre:

1. **A** (minimal, 0 riesgo) — eliminar `TDbContext DbContext` del contrato.
2. **B** (contract-level, F1 resuelto, genérico sin constraint).
3. **D** (RETAIN + documentar) — F1 como limitación aceptada de la capa DATA.
4. **E** (desacople total, mayor blast radius) — interfaz no genérica.

**Recomendación preliminar del discovery** (sin voto): **A o D** para un sprint incremental seguro; **B** solo si se acepta la semántica débil del genérico; **E** requiere un sprint dedicado de refactor con 132 call-sites.

---

## 6. Reglas de ejecución (si se autoriza S25)

- **Paridad Async↔Sync obligatoria**: cualquier cambio en `IUnitOfWorkAsync` debe replicarse en `IUnitOfWorkSync` (regla permanente del usuario).
- **Tests S24 deben seguir pasando** (47/47) tras el cambio.
- **`SaveChangesAsync` sigue llamado solo desde el consumer** (WebAPI/web), nunca dentro de repositorios/servicios.
- **Build 0 errores 0 warnings** tras cada cambio (`dotnet build PassPlat.slnx`).
- Si la decisión es A o B: el cambio toca SOLO `CBP.Data.Abstractions\IUnitOfWork.cs` + eventualmente las impls `UnitOfWorkAsync.cs` / `UnitOfWorkSync.cs` (si quieren mantener la constraint a nivel de clase).

---

## 7. Trazabilidad

| Referencia | Fuente |
|------------|--------|
| F1 (IUnitOfWork conoce DbContext) | `S24-Core-Boundary-Enforcement.md` §deudas · S23 discovery |
| F6 (Services→Data.Abstractions→EF transitivo) | `S24-Core-Boundary-Enforcement.md` §deudas |
| Reglas D-01..D-06 | `CBP-Dependency-Rules.md` (normativo S24) |
| Veredicto usuario S24 (S25 CANDIDATE, F1+F6 frente único) | sesión 2026-08-13 |
| 47 tests gobernanza | `PassPlat.CBP.Architecture.Test\DependencyBoundaryTests.cs` |
