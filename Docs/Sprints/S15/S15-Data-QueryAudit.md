# S15-Data-QueryAudit.md — Auditoría de Consultas EF / Rendimiento (documento compañero de F4)

# Estado          Borrador
# Tipo            ☐ Evidencia ☑ Análisis ☐ Decisión
# Fuente          Data-Audit
# Depende de      Data-Audit
# Influye en      Refactoring, Certification
# Área            Rendimiento de consultas EF Core: AsNoTracking, Include/ThenInclude, paginación, query plans
# Framework CBP   CBP.Data.Asynchronous (RepositoryAsync<T>, GetPagedAsync, GetSeekPagedAsync)
# Cobertura       PassPlat.Datos
# Evidencia       grep sobre 57 repos · AsNoTracking=70 · Include=121 · ThenInclude=9 · GetPagedAsync=0 · GetSeekPagedAsync=0 · AsSplitQuery=0 · ToListAsync=109
# Resultado       WARNING (AsNoTracking bien usado; pero 0 paginacion via framework (todas manual), 0 AsSplitQuery ante Include multiples, sin tracking sin cache; asequible)
# Cobertura       70 %

---

## 1. Proposito

Documento compañero de `S15-Data-Audit.md`. Complementa el análisis de **cómo se escriben y ejecutan las consultas EF**: uso de AsNoTracking (lecturas), estrategia de carga (Include/ThenInclude), paginación (framework vs manual), y riesgo de N+1 de la envoltura de repos. Cada hallazgo objetivo produce deuda técnica de rendimiento identificable.

## 2. Metodo (estructura obligatoria)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Medidas de consulta en repos (contadores veredictos)

| Patron | Conteo | Estado | Confidence |
|---|---|---|---|
| `AsNoTracking()` | 70 | Bien usado en lecturas | Alta |
| `Include(...)` | 121 incidencias | carga ansiosa | Alta |
| `ThenInclude(...)` | 9 | pocas cadenas profundas | Alta |
| `AsSplitQuery`/`AsSingleQuery` | 0 | no se usa → multi-collection joins unificados | Alta |
| `GetPagedAsync` (CBP) | **0** | no se usa paginacion de framework | Alta |
| `GetSeekPagedAsync` (CBP) | 0 | no se usa cursor paging | Alta |
| `ToListAsync`/`ToList` | 109 | materializacion ejecutada | Alta |
| `Where(...).Take().Skip()` manual | (documentado en seccion 5) | paginacion manual local | Media |

## 4. Análisis por patrón

### 4.1 AsNoTracking (lecturas) → PASS
Bien usado en la mayoría de lecturas de listado; se limita a correr consultas y materializar. Utilizado para evitar tracking overhead en Dashboard/repo de consulta.

### 4.2 Paginación (gap critico)
- **0** uso de `GetPagedAsync`/`GetSeekPagedAsync` de CBP. Los listados (Usuarios, Accesos, Dashboard) aplican **paginado manual** `Skip/Take` inline o materializan completo con `ToListAsync` y filtran en memoria. Sin cursor-based para grandes volúmenes.
- Impacto: para tablas altas (IntentoAcceso, AuditoriaPwd, EmailLog con millones) el `ToList` completo en memoria es riesgo de memoria/CPU y lentitud.
- Framework S equivalente que provee `GetPagedAsync`/`GetSeekPagedAsync` (RepositoryAsync) NO se aprovecha en el detalle de paginacion.

### 4.3 Include / ThenInclude → riesgo de cartesian explosion
- 121 `Include` y solo 9 `ThenInclude`: las cadenas son de 1 nivel mayormente, pero multiples `Include` en una consulta **sin AsSplitQuery** generan JOIN cartesiano (row multiplication) al combinar multiples collector (ej. Usuario+Tenant+Roles+direccion...). No hay `AsSplitQuery()` en ningún repo.
- Esperado en pocos casos por catividad, pero no óptimo para tablas con varios colecciones.

### 4.4 N+1 potencial
- 109 `ToListAsync/ToList` en repos, muchos tras un `FirstOrDefault` + `Include` de 1 nivel (no collector): riesgo moderado de acceso lazy-triggered si no se usa eager Include correctamente. Estrategia predominante es eager-load (Include), correcta, pero verificar N+1 en Dashboard (join de multiples tablas) — e deposito en DashboardEnterpriseService.

## 5. Hallazgos

| ID | Hallazgo | Evidencia | Resultado | Accion | Confidence |
|---|---|---|---|---|---|
| **Q-AUD-001** | `AsNoTracking` ampliamente usado en lecturas — patron correcto. | 70 usos | PASS | REUTILIZAR | Alta |
| **Q-AUD-002** | **No se usa `GetPagedAsync`/`GetSeekPagedAsync` de CBP**; paginacion manual (Skip/Take o ToList+filter) en todas las listas. | grep GetPaged/GetSeek = 0 | **FAIL** | REEMPLAZAR (usar framework) | Alta |
| **Q-AUD-003** | **No `AsSplitQuery`**: multiples `Include` (ej. usuarios con roles/tenants) van en una sola query cartesiana → riesgo de multiplicacion de filas y lentitud. | AsSplitQuery = 0 | WARNING | EXTENDER (AsSplitQuery en consultas multi-collector) | Alta |
| **Q-AUD-004** | Paginacion manual puede causar `ToList` completo en tablas altas (IntentoAcceso, AuditoriaPwd, EmailLog). | `ToListAsync` =109; sin seign en repos altos | WARNING | REEMPLAZAR (seek-page por PK) | Media |
| **Q-AUD-005** | 121 Include sin ThenInclude consistente sugiere de primer nivel solo; verificar cadenas deep que causan N+1 (ej. Usuario→Accesos→Rol). | Include=121, ThenInclude=9 | WARNING | REVISAR (eager correcto) | Media |

## 6. Matriz de riesgo de query por tabla grande

| Tabla | Escala exp | Paginacion | Include multi | Riesgo |
|---|---|---|---|---|
| IntentoAcceso | alta | manual | baja | Medio |
| AuditoriaPwd | alta | manual | baja | Medio |
| EmailLog | media | manual | baja | Medio |
| Usuarios+Accesos | media | manual | si | Alto (multi Include) |
| HistorialPwd | media | manual | baja | Bajo |

## 7. Resultado (queries)

- **Bien**: AsNoTracking correcto; eager-load predominante.
- **Faltante** (TDA): paginacion de framework no usada (Q-AUD-002), AsSplitQuery ausente (Q-AUD-003), ToList manual en tablas altas (Q-AUD-004).
- Riesgo general: **Medio** (no critico hoy por volatilidad, pero deuda de escalado).

## 8. Cierre uniforme S15

| Metrica | Valor |
|---|---|
| Cobertura CBP | 55 % (repo base usada, pagin no) |
| Architecture Score | 68 / 100 |
| Confidence | Alta |
| Technical Debt | TD-QAUD-001..005 |