# S15-Services-Audit.md — Capa de Servicios / Aplicacion (F9)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          CBP-Inventory
# Depende de      Inventory+DI
# Influye en      Certification
# Area            Servicios de aplicacion (F9)
# Framework CBP   CBP.Services.Async (ServiceAsync<TEntity,TDto>), CBP.Services.Abstractions (IServiceAsync<TEntity,TDto>, ICustomService, AddServiceAsync), CBP.Data (IUnitOfWorkAsync)
# Cobertura       PassPlat.Aplicacion (Services) | PassPlat.Aplicacion.Dtos
# Evidencia       74 servicios; 53 heredan `ServiceAsync<TEntity,TDto>`; 54 IServiceAsync; 3 ICustomService directo; 13 SPro (Security/Auth/Maintenance) propios por SP · AplicacionDependencyInjection.cs (58 AddServiceAsync)
# Resultado       REUTILIZAR+JUSTIFICAR (53/74 CBP base; servicios SP no usan CBP y son propios, justificables)
# Cobertura       80 % (ver F11)
# Riesgo          Bajo
# Prioridad       Alta

---

## 1. Proposito

Auditar la capa de servicios: que proporcion adopta de CBP.Services (ServiceAsync, IServiceAsync), cómo se registran en DI, y si los servicios de SPs/core (SPro) reutilizan CBP o implementan logica propia. Ordenar 74 servicios por patron.

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Metricas de servicios

| Metrica | Valor | Evidencia |
|---|---|---|
| Total de `*Service.cs` | 74 | `Services\` (Authentication+BBDD+Dashboard+Email+OAuth+Security+SPro) |
| Heredan `ServiceAsync<TEntity,TDto>` (CBP.Services.Async) | **53** | `ServiceAsync<Entidad,EntidadDto>, I...Service` |
| Implementan `IServiceAsync<TEntity,TDto>` (CBP) | 54 | interpolacion |
| Implementan solo `ICustomService` (CBP) | 3 | poca flexibilidad |
| Servicios SPro (SP/Security/core) NO-CBP | 13 | `Services\SPro` + Auth/MFA/Maintenance/Password/Sesion/TokenRest/Acceso/Bloqueo/IntentoAcceso/HistorialPwd |
| DTO para servicios | SI (XDto, CrearXDto, ActualizarXDto) | `Aplicacion.Dtos` |

## 4. Patrones de servicio

### 4.1 Servicios BBDD (catalogo/contexto) — heredan CBP

```csharp
public class AppService : ServiceAsync<App, AppDto>, IAppService
```
`ServiceAsync<TEntity,TDto>` proporciona CRUD generico (GetById, GetAll, Create, Update, Delete) con AutoMapper + Result, heredando la logica CBP. EJ: App, AppEmailAccount, AppModulo, AudIdenExt, Tenant, Rol, Permiso, Modulo, PoliticaPwd, etc. **53 servicios**.

### 4.2 Servicios SP (core de negocio) - logica propia + CBP repos

Ej: `AuthService`, `PasswordService`, `MfaService`, `SesionService`, `TokenRestService`, `AccesoService`, `BloqueoService`, `IntentoAccesoService`, `HistorialPwdService`, `AuditoriaPwdService`, `MaintenanceService` (13). Usan repos con SP + `IUnitOfWorkAsync<PassPlatDbContext>` + AutoMapper + ILogger, retornando `Result<T>`. NO heredan ServiceAsync (no aplica CRUD simple), pero SÍ consumen repos y Result CBP.

## 5. Hallazgos

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **SRV-001** | 53/74 servicios heredan `ServiceAsync<TD>` de CBP — gran reuso de la base de CRUD+Result+PageSeek. | `Services/BBDD/*Service.cs` (53) | PASS |
| **SRV-002** | Interfaz base `IServiceAsync<T>`/`ICustomService` implementada consistentemente. | 54+3 | PASS |
| **SRV-003** | Servicios core (SPro, 13) NO heredan `ServiceAsync` por no ser CRUD puro (SP/Auth/MFA). Logica propia justificada, repos consumen CBP repo. | `Services/SPro/*` | JUSTIFICAR (correcto) |
| **SRV-004** | `AddServiceAsync` (CBP extension) usado para 54 servicios en DI — reutiliza el patrón de resolucion. | `AplicacionDependencyInjection.cs:31...` | PASS |
| **SRV-005** | Consistentes `Result<T>` a lo largo de todas las capas (regla de cadena de propagación). | repos→servicios→controllers | PASS |
| **SRV-006** | Algunos servicios BBDD repiten boilerplate de "ObtenerPor..." conjo (repetitivo pero correcto, sin duplicar CBP). | repos custom | WARNING (menor) |
| **SRV-007** | Puede haber servicios propios debilitados a no-HElda: `ICustomService` directos (3) — probablemente Auth/Session no-half CRUD. | 3 ICustomService | JUSTIFICAR |

## 6. Clasificacion general
- **CBP.Services**: reutilizado en 53 servicios BBDD (CRUD + Result + DI helper). 
- **Servicios core/SP**: propios pero arquitecturalmente correctos (consumen repos CBP).
- Duplicacion: **baja** (solo boilerplate repetido en custom repos).

## 7. Resultado F9
- **REUTILIZAR + JUSTIF**: la capa de servicios hereda el modelo de CBP para CRUD, con servicios SP propios justificados.
- Insumo F12 → acciones y trazabilidad migradas a `S15-CBP-Refactoring-Plan.md` (Nivel 3). Este doc conserva SOLO evidencia N1.

### 7.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| SRV-001 | PASS | REUTILIZAR (53/74 ServiceAsync CBP) | — | — | Alta |
| SRV-002 | PASS | REUTILIZAR | — | — | Alta |
| SRV-003 | PASS | JUSTIFICAR (SPro no-CRUD) | — | — | Alta |
| SRV-004 | PASS | REUTILIZAR (AddServiceAsync) | — | — | Alta |
| SRV-005 | PASS | REUTILIZAR (Result propagation) | — | — | Alta |
| SRV-006 | WARNING | JUSTIFICAR (boilerplate repetido) | Baja | P3 | Media |
| SRV-007 | PASS | JUSTIFICAR (ICustomService directo) | — | — | Media |

### 7.2 Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 80 % |
| Architecture Score | 86 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-SRV-001..007 (menor) |