# S15-MultiTenant-Audit.md — Multi-tenant / Tenant Context (F8)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          CBP-Inventory
# Depende de      Inventory
# Influye en      MultiTenant-Propagation, Certification
# Area            Multi-tenancy / aislamiento por tenant (F8)
# Framework CBP   CBP.MultiTenant (CBP.Infraestructure) — ITenantContext, ITenantResolver<Tenant>, ITenantMapper<Tenant>, ITenantInitializer, TenantContext, TenantInitializer, TenantInfo, AddMultiTenantScoped, TenantErrors
# Cobertura       PassPlat.WebAPI (Program.cs) | Aplicacion | Datos
# Evidencia       Program.cs:217-220 (AddMultiTenantScoped<JwtTenantContext>, ITenantResolver/TenantMapper/Initializer) · :242 UseTenantResolutionMiddleware · PassPlatTenantResolver/Mapper · JwtTenantContext · TenanEntity (UsuarioTenant) · TenantResolutionMiddleware(ICacheService) · AuthenticationContext/claims TenantId-UsuarioTenantId
# Resultado       REUTILIZAR+EXTENDER (framewnk MultiTenant usado, pero la resolucion/id de miembro es capa propia UsuarioTenant)
# Cobertura       75 % (ver F11)
# Riesgo          Medio
# Prioridad       Alta

---

## 1. Proposito

Auditar la capa multi-tenant: si consume CBP.MultiTenant (context, resolver, mapper, initializer), cómo resuelve el tenant en un request y la persistencia de membresia (UsuarioTenant) y su aislamiento por datos.

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Componentes CBP reutilizados

| Componente CBP.MultiTenant | Reuso en PassPlat | Evidencia |
|---|---|---|
| `AddMultiTenantScoped<JwtTenantContext>()` | Registro del contexto tenant Scoped | `Program.cs:217` |
| `ITenantResolver<Tenant>` | `PassPlatTenantResolver` (impl propio del contrato CBP) | `Program.cs:218` |
| `ITenantMapper<Tenant>` | `PassPlatTenantMapper` | `Program.cs:219` |
| `ITenantInitializer` | `TenantInitializer<Tenant>` (CBP) | `Program.cs:220` |
| `TenantContext` / `TenantInfo` | Modelo de contexto de CBP | `CBP.MultiTenant.Context.TenantContext` |
| `TenantResolutionMiddleware` (propio) | Resuelve IdTenant del JWT + cache (ICacheService) | `Program.cs:242`; `WebAPI/MultiTenant/TenantResolutionMiddleware.cs` |

## 4. Capa propia de membresia (UsuarioTenant)

| Componente | Funcion | Evidencia |
|---|---|---|
| `UsuarioTenant` entity | Membresia Usuario-Tenant (IdUsuario, IdTenant, Activo, IdEstado) | `Dominio/Entities/Core/UsuarioTenant.cs` |
| `UsuarioTenantRepository` (7 metodos) | `ObtenerPorUsuario`, `ObtenerMembresia`, `ResolverIdUsuarioTenant`, etc. | `Datos/Repositories/UsuarioTenantRepository.cs` |
| `Acceso.IdUsuarioTenant` | FK de la relacion (Acceso→UsuarioTenant) | `Acceso.cs:10` |
| `AuthenticationContext.IdTenant/IdUsuarioTenant` | Contexto de autenticacion tenant-scope | `Services/Authentication/AuthenticationContext.cs` |
| Claims JWT `TenantId`/`UsuarioTenantId` | Autorizacion: el JWT lleva el tenant/membresia | `AuthenticationTokenIssuer.cs` |
| `PasswordExpirationBackgroundService` | Union multi-tenant de membresias (strictest policy) | Background |

## 5. Hallazgos

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **MT-001** | `AddMultiTenantScoped` CBP se registra (contrato base respetado). | `Program.cs:217` | PASS |
| **MT-002** | Resolucion del tenant es propia (`PassPlatTenantResolver` + `TenantResolutionMiddleware` con ICacheService) en vez de default. Implementa el contrato CBP pero le agrega JWT+membership. | `Program.cs:218,242`; `TenantResolutionMiddleware.cs` | PASS (EXTENDER CBP) |
| **MT-003** | Persistencia de membresia `UsuarioTenant` es **capa propia** (CBP no provee tabla de membresia usuario-tenant activo). | `UsuarioTenant.cs`, repo | JUSTIFICAR (dominio PassPlat) |
| **MT-004** | Aislamiento de datos por tenant: Dashboard/consultas filtran por `IdTenant` via UsuarioTenant; apps globales sin IdTenant (correcto). | `DashboardEnterpriseService`, A1.5.4 | PASS |
| **MT-005** | `ResolvedTenantId` se guarda en `context.Items` y se cachea (ICacheService) — no re-consulta BD cada request. | `TenantResolutionMiddleware.cs` | PASS |
| **MT-006** | JWT claims `TenantId`/`UsuarioTenantId` autoritativos en Blazor/API (A1.7). | `AuthenticationTokenIssuer`, `CustomAuthenticationStateProvider` | PASS |
| **MT-007** | Platform scope: `IdTenant=null` representado con claims nulos; mediambientado por platform login/switch. | AuthService.PlatformLoginAsync | PASS |
| **MT-008** | El `TenantResolutionMiddleware` es **propio** a PassPlat (no es un middleware de CBP), con cache propietaria; reusa la interfaz CBP pero la logica de parseo es propia. | Program.cs:242, middleware | WARNING (duplicacion respecto a lo que CBP.MultiTenant pudiera ofrecer) |

## 6. Clasificacion general
- **Contrato CBP.MultiTenant**: REUTILIZADO (AddMultiTenantScoped + resolver/mapper/initializer interfaces).
- **Logica de membresia y contexto de usuario**: PROEIA (UsuarioTenant) — correcto por ser dominio PassPlat multi-usuario.
- Duplicacion: baja — el middleware propio implementa el contrato con lógica.

## 7. Resultado F8
- **REUTILIZAR + EXTENDER / PASS**: PassPlat usa el framework CBP.MultiTenant como base de resolución, pero extiende con una capa de membresia `UsuarioTenant` (requerida por el modelo), la cual es ag(cling) propuesta.
- Aislamiento de datos: mantenido via JWT claims + filtros tenant; verificacion A1.8/24 tests + A1.9/17 tests PASS.
- Insumo F12 → acciones y trazabilidad migradas a `S15-CBP-Refactoring-Plan.md` (Nivel 3). Este doc conserva SOLO evidencia N1.

### 7.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| MT-001 | PASS | REUTILIZAR | — | — | Alta |
| MT-002 | PASS | REUTILIZAR (contrato + JWT) | — | — | Alta |
| MT-003 | PASS | JUSTIFICAR (membresia dominio) | — | — | Alta |
| MT-004 | PASS | REUTILIZAR | — | — | Alta |
| MT-005 | PASS | REUTILIZAR (cache tenant) | — | — | Alta |
| MT-006 | PASS | REUTILIZAR | — | — | Alta |
| MT-007 | PASS | REUTILIZAR | — | — | Alta |
| MT-008 | WARNING | JUSTIFICAR (middleware propio) | Baja | P3 | Alta |

### 7.2 Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 75 % |
| Architecture Score | 82 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-MT-001..008 (deuda de juicio/no critica) |

**Ver tambien**: `S15-MultiTenant-Propagation-Audit.md` — trazabilidad de `TenantId`/`UsuarioTenantId` a través de las capas.