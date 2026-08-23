# S15-Data-Audit.md — Capa de Datos / Repositorios / EF Core (F4)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          CBP-Inventory
# Depende de      Inventory
# Influye en      Data-Query, Certification
# Area            Datos: repositorios, EF Core, SPs (F4)
# Framework CBP   CBP.Data.Asynchronous (RepositoryAsync<T>, RawQueryRepositoryAsync), CBP.Data.Abstractions (IRepositoryAsync<T>, IRawQueryRepositoryAsync, IUnitOfWorkAsync<T>), CBP.Data.Synchronous (IUnitOfWorkAsync<PassPlatDbContext>)
# Cobertura       PassPlat.Datos | PassPlat.Aplicacion
# Evidencia       57 repos (110 herencias RepositoryAsync), 52 interfaces IRepositoryAsync, 14 SPResults, 28 refs RawQuery/SP, DatosDependencyInjection.cs
# Resultado       REUTILIZAR / PASS (repo base CBP bien adoptado; SPs via RawQuery limitados)
# Cobertura       85 % (ver F11)
# Riesgo          Bajo
# Prioridad       Alta

---

## 1. Proposito

Auditar la capa de datos: patron de repositorio, unidad de trabajo, EF Core, ejecucion de stored procedures via CBP.Data, y registros DI. Determinar que parte reutiliza CBP.Data y cual es logica propia.

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Repositorios

### 3.1 Patron base (PASS)

| Metrica | Valor | Evidencia |
|---|---|---|
| Repositorios (archivos) | 57 | `PassPlat.Datos\Repositories\` |
| Heredan `RepositoryAsync<TEntity>`) (CBP) | 57/57 | clase `: RepositoryAsync<Entidad>, IEntidadRepository` |
| Interfaces extienden `IRepositoryAsync<TEntity>` (CBP) | 52 | grep `interface I...Repository : IRepositoryAsync` |
| Repos catálogo (sin interfaz) | ~5 | solo clase concreta |
| Repos SP (Auth, Password, MFA, Sesion, TokenRest, Maintenance, ExternalAuth) | SP + EF RawQuery | `AuthRepository`, etc. |

Todos los repos pasan por la base `CBP.Data.Asynchronous.RepositoryAsync<TEntity>` con métodos bas read-async (`Query`, `GetByIdAsync`, `WhereAsync`, `FirstOrDefaultAsync`, `CountAsync`) y write-sync (`Add`, `Update`, `Remove`), retornando `Result<T>` de `CBP.Results`.

### 3.2 Uso de SPs (RawQuery)

| Patron | Evidencia | Clasificacion |
|---|---|---|
| `_rawQuery.QuerySPAsync<T>(...)` via `IRawQueryRepositoryAsync` | 14 refs | PASS |
| Resultados mapeados a `SPResults` DTO (14) | `SPResults\*.cs` | PASS |
| Deserializacion del SP ing y desernac en `QuerySPAsync<T>` | RawParameter.Int/NVarChar/Date | PASS |

Cerebros: los SP core (SP_Auth_Login, SP_MFA_Validar, SP_Sesiones_Crear, etc.) se ejecutan via `QuerySPAsync<T>` y se mapean a `LoginResult`, `ValidarMFAResult` etc.

## 4. Unidad de Trabajo

| Componente | Uso | Evidencia | Clasificacion |
|---|---|---|---|
| `IUnitOfWorkAsync<PassPlatDbContext>` | inyectado en servicios para SaveChangesAsync + transaccion + repos | API consumer | PASS |
| `SaveChangesAsync` desde consumer (WebAPI/controller) | SI (regla arquitectural que NO se guarda desde servicios) | Controllers | PASS |
| `ExecuteInTransactionAsync` | transacciones unitarias | UoW | PASS |
| No-cascade + Restrict | EF ConfigCon Restrict DeleteBehavior | Configurations | PASS |

## 5. Hallazgos

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **DATA-001** | Repositorios adoptan correctamente CBP `RepositoryAsync<T>` (paso CRUD base + Result) | 57 repos `: RepositoryAsync<Entidad>` | PASS |
| **DATA-002** | Interfaces usan `IRepositoryAsync<T>` de CBP.Abstractions (reutilizando abstracciones) | 52 interfaces `: IRepositoryAsync` | PASS |
| **DATA-003** | SP core se ejecutan via `IRawQueryRepositoryAsync` (RawParameter) — cobertura SP vs tabla | 8 SP directos + ExternalAuth | PASS |
| **DATA-004** | `IUnitOfWorkAsync` no es de CBP.Data.**Asynchronous** (`CBP.Data.Synchronous` XDokumento?) — se referencia `IUnitOfWorkAsync<T>` del paquete Synchronous para compat (documentado en Inventario). No duplica, reusa. | `IUnitOfWorkAsync<PassPlatDbContext>` importado de CBP.Data.Synchronous | **JUSTIFICAR** (limitar esta dependencia en F12) |
| **DATA-005** | Alguna interseccion redundante Small: catalo tables sin interfaz propia (solo concreto) se inyectan directamente — ok pero inconsistente con el resto | 5 repos sin interfaz | WARNING |
| **DATA-006** | `QuerySPAsync<T>` solo mapea a DTOs flat (SPResults); no hay soporte a TVPs/UDTs mas alla de RawParameter (requiere `QuerySPRawAsync` para TVP) | SPExecution raw = RawQueryRepositoryAsync | WARNING (Uso completo bajo TVP) |
| **DATA-007** | Bug preexistente de concurrencia en `AccesoRepository.AsignarAccesoAsync` (0 filas afectadas) — ajeno al CBP pero impacto en F12 | documented FASE13 | WARNING (a resolver) |
| **DATA-008** | `DbSet` vs `DbContext` directo en repos: se usa `DbSet` heredado (patron CBP), nunca `DbContext` directo desde servicios | repos solo | PASS |

## 6. Clasificaciones finales

| Cluster | Clasificacion |
|---|---|
| Base `RepositoryAsync<T>` (CRUD, Result) | REUTILIZAR (PASS) |
| Abstracciones `IRepositoryAsync`, `IUnitOfWorkAsync` | REUTILIZAR (PASS) |
| SP execution via `RawQuery` | REUTILIZAR-PAS (PASS) |
| Dependencia IUnitOfWork de `CBP.Data.Synchronous` | REUTILIZAR (documentado, transporte) |
| Repos catálogo sin interfaz | WARNING |

## 7. Resultado F4
- **REUTILIZAR/ PASS**: la capa de datos de PassPlat adopta correctamente CBP.Data (RepositoryAsync + IRawQueryRepository + IUnitOfWorkAsync + Result).
- Duplicacion: **baja** (no hay repo propio alternativo; catalog sin IFace es administrable).
- Insumos F12:
  1. Unificar los 5 repos catálogo sin interfaz a `IRepositoryAsync` (limpieza DI).
  2. Eliminar dependencia de `CBP.Data.Synchronous` para `IUnitOfWorkAsync` si CBP mueve la interfaz a Data.Asynchronous/Abstractions (deuda intraframework).
  3. Considerar `QuerySPRawAsync` para SPs con TVPs/UDTs.
  4. Resolver concurrencia en `AccesoRepository.AsignarAccesoAsync`.

### 7.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| DATA-001 | PASS | REUTILIZAR | — | — | Alta |
| DATA-002 | PASS | REUTILIZAR | — | — | Alta |
| DATA-003 | PASS | REUTILIZAR | — | — | Alta |
| DATA-004 | PASS | JUSTIFICAR (limitar dep Synchronous en F12) | Baja | P2 | Alta |
| DATA-005 | WARNING | REEMPLAZAR (interfaces a catalogo) | Baja | P3 | Alta |
| DATA-006 | WARNING | JUSTIFICAR (solo si TVP/UDT) | Baja | P3 | Media |
| DATA-007 | WARNING | REEMPLAZAR (bug concurrencia accesos) | Media | P1 | Alta |
| DATA-008 | PASS | REUTILIZAR | — | — | Alta |

### 7.2 Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 85 % |
| Architecture Score | 82 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-DATA-001..008 |

**Ver tambien**: `S15-Data-QueryAudit.md` — analisis de consultas EF (rendimiento, AsNoTracking, paginacion).