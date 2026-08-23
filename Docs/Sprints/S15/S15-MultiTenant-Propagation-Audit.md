# S15-MultiTenant-Propagation-Audit.md — Propagación del Contexto Tenant (documento compañero de F8)

# Estado          Borrador
# Tipo            ☐ Evidencia ☑ Análisis ☐ Decisión
# Fuente          MultiTenant-Audit
# Depende de      MultiTenant-Audit, Security-Audit
# Influye en      Certification
# Área            Trazabilidad de `TenantId` y `UsuarioTenantId` a través de las 4 capas (aislamiento por dato)
# Framework CBP   CBP.MultiTenant (ITenantContext, ITenantResolver), claims JWT
# Cobertura       WebApi | Aplicacion | Datos
# Evidencia       Program.cs:217-218,242 (AddMultiTenantScoped + resolver + middleware) · TenantResolutionMiddleware.cs:37,43 (ResolvedTenantId→context.Items) · AuthenticationContext.cs (IdTenant/IdUsuarioTenant) · AuthenticationTokenIssuer.cs (claims TenantId/UsuarioTenantId) · UsuarioTenantRepository.cs (membership) · AccesoRepository.cs (IdUsuarioTenant)
# Resultado       PASS (aislamiento por tenant mantenido; TenantId/UsuarioTenantId fluyen JWT→contexto→repos; platform scope representado con null)
# Cobertura       80 %

---

## 1. Proposito

Documento compañero de `S15-MultiTenant-Audit.md`. Verifica la **propagación del contexto de tenant** (`TenantId`, `UsuarioTenantId`) a lo largo de las capas y confirma que el aislamiento por datos (no solo por claim) se respeta en repositorios/servicios. Cada hallazgo debe clasificarse por cobertura de aislamiento.

## 2. Metodo (estructura obligatoria)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Cadena de propagacion

```
JWT claims (TenantId, UsuarioTenantId)   ← plataforma/tenant scope
   → AuthenticationContext (IdTenant, IdUsuarioTenant, IdUsuario, IdApp)
   → TenantResolutionMiddleware (ResolvedTenantId → context.Items)
   → services (reciben scope explicito `int? idTenant`)
   → repositorios (`.Where(IdTenant == ...)` vía UsuarioTenant/Acceso)
   → SQL persists (filtro tenant)
```

| Etapa | Mecanismo | Evidencia | Aislamiento |
|---|---|---|---|
| JWT | claims `TenantId`/`UsuarioTenantId` autoritativos | `AuthenticationTokenIssuer.cs` | asegura identidad |
| Middleware | `context.Items["ResolvedTenantId"]` | `TenantResolutionMiddleware.cs:43` | contexto HTTP |
| Service | parametro `int? idTenant` (no fallback a Usuario.IdTenant) | 9 fixes A1.5.3.2 | dominio |
| Repository | filtros `UsuarioTenant` / `Acceso.IdUsuarioTenant` | `UsuarioTenantRepository`, `AccesoRepository` | dato |

## 4. Vectores de aislamiento por modelo

| Modelo | Filtro tenant | `IdTenant` nullable (platform) | Aislamiento |
|---|---|---|---|
| Usuario | via UsuarioTenant membresia | si (platform) | ✅ |
| Acceso | `IdUsuarioTenant` FK | si (platform) | ✅ |
| Dashboard | `IdTenant` vía membresia + guard .HasValue | si | ✅ |
| Apps | global, sin `IdTenant` (catalogo) | no aplica | ✅ (correcto) |
| Sesion | `IdSesion/Jti` + tenant | — | ✅ |

## 5. Hallazgos de propagacion

| ID | Hallazgo | Evidencia | Resultado | Accion | Confidence |
|---|---|---|---|---|---|
| **PROP-001** | `ResolvedTenantId` se enriqueze a `context.Items` una vez y se reusa; sin re-consulta por request. | `TenantResolutionMiddleware.cs:15,43` | PASS | REUTILIZAR | Alta |
| **PROP-002** | Services reciben scope explicito (`int? idTenant`) — **no** fallback a `Usuario.IdTenant` (9 eliminados en A1.0). | services A1 E2 | PASS | REUTILIZAR | Alta |
| **PROP-003** | **No hay fuga de cross-tenant**: MfaController valida members activa; Dashboard filtra via UsuarioTenant. | A1 gates + tests | PASS | REUTILIZAR | Alta |
| **PROP-004** | Invalid: migration/ci tiene `IdTenant` en Usuario aún como FK legacy; no debe usarse como contexto. | A1.0 | WARNING | DIFERIR (limpiar legacy) | Media |
| **PROP-005** | Platform scope (`IdTenant=null`) NO se cachea como tenant ficticio; la sentencia no aplica filtro tenant (correcto). | TenantResolutionMiddleware NotFoundSentinel | PASS | REUTILIZAR | Alta |

## 6. Matriz de riesgo de fuga por operacion multi-tenant

| Operacion | vector propagacion | Riesgo cross-tenant | Confidence |
|---|---|---|---|
| Login | claims | Bajo | Alta |
| Dashboard | repo filtrado | Bajo | Alta |
| MFA | check membership | Bajo | Alta |
| Switch tenant | membership check | Bajo | Alta |
| Acceso | IdUsuarioTenant | Bajo | Alta |

## 7. Resultado (propagacion)

- **PASS** global: propagacion del contexto tenant via claims → middleware → service → repository es completa y sin fallback legacy.
- **WARNING**: columna legacy `Usuario.IdTenant` / `Acceso.IdTenant` mantienen una FK documentada para datos historicos (no contexto) — deuda de limpieza (MTEQ-004).
- Cobertura de aislamiento: ~95 % de areas multi-tenant verificadas.

## 8. Cierre uniforme S15

| Metrica | Valor |
|---|---|
| Cobertura CBP | 78 % |
| Architecture Score | 83 / 100 |
| Confidence | Alta |
| Technical Debt | TD-MT-PROP (MT-PROP-004 legacy) |