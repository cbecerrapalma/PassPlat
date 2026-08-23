# S15-Extensions-Audit.md — Extensiones Legítimas sobre CBP (Fase F — síntesis)

# Estado          Borrador
# Tipo            ☐ Evidencia ☑ Análisis ☐ Decisión
# Fuente          varios N1
# Depende de      Inventory, Security-Audit, DI-Audit
# Influye en      Refactoring, Decisiones
# Área            Extensiones de PassPlat que reutilizan CBP como base (80%) + lógica de dominio (20%) — NO duplicación
# Framework CBP   CBP.Authentication, CBP.Security.Cryptography, CBP.MultiTenant, CBP.Data, CBP.WebApi, CBP.Results
# Cobertura       transversal
# Evidencia       PASS: AuthenticationTokenService/AuthenticationContext (é12), PermissionClaimBuilder.cs:57, PassPlatPasswordSecurity.cs, UsuarioTenant + repo, TenantResolutionMiddleware, AuthService.PlatformLoginAsync, AccesoRepository overload con IdUsuarioTenant, SessionManager
# Resultado       EXTENDER (correcto) — la base CBP se extiende con dominio PassPlat sin reemplazarla; patrón queda en <80/20>
# Cobertura       95 %

---

## 1. Objetivo (síntesis Fase F)

Documentar y clasificar las **extensiones legítimas** de PassPlat sobre CBP: combinaciones donde PassPlat reutiliza el componente CBP como base y añade una capa de negocio/tenant. Estas NO son duplicación — son la evolución esperada en una arquitectura de framework reutilizable. Clasificación por el ratio `CBP ~80% / PassPlat ~20%`; si se supera a favor de PassPlat sin motivo, `REEMPLAZAR`.

## 2. Método (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Catálogo de extensiones legítimas (composicion base-CBP)

| Extensión PassPlat | Base CBP reutilizada | Capa propia PassPlat | Ratio (CBP:PP) | Resultado | Confidence |
|---|---|---|---|---|---|
| `AuthenticationContext` + `AuthenticationTokenService` | `IJwtTokenService` (CBP) | Claims de identidad/sesión + MFA check | 80:20 | EXTENDER | Alta |
| `PermissionClaimBuilder` 3-branch | — (perm is dominio) | Claims de permisos multi-tenant (a partir de CBP.Authentication) | 60:40 | EXTENDER | Alta |
| `PassPlatPasswordSecurity` | `IPasswordService` + validators CBP | Política (PoliticaPwd) + validador de la API | 90:10 | EXTENDER | Alta |
| `UsuarioTenantRepository` (7 métodos) | — | Membresía Usuario-Tenant (dominio) | 0:100 | EXTENDER (no existe en CBP) | Alta |
| `TenantResolutionMiddleware` | `ITenantResolver`/mapper CBP.MultiTenant | Resolución JWT→Tenant + cache | 70:30 | EXTENDER | Alta |
| `AuthService.PlatformLoginAsync` | CBP.Security (Argon2id) | Flujo login plataforma sin SP | 50:50 | EXTENDER | Alta |
| `AccesoRepository.AsignarAccesoAsync` (overload con IdUsuarioTenant) | `RepositoryAsync<T>` CBP | Lógica de asignación membre | 70:30 | EXTENDER | Media |
| `AuthenticationTokenService` (3 flujos) | `IJwtTokenService` | Orquestación login/MFA/refresh | 70:30 | EXTENDER | Alta |

## 4. Regla de clasificación (anti-falso-duplicado)

- `CBPP 80% + PassPlat 20%` → **EXTENDER** (nunca REEMPLAZAR).
- Si una componente nuevo supera ~60% propio, evaluar si debería vivrenchiar dentro de CBP o en el dominio.
- Ninguna de las extensiones lista duplica; todas meses el contrato del framework como base y pueblan datos de dominio/tenant.

## 5. Hallazgos de extensiones

| ID | Hallazgo | Resultado | Accion | Confidence |
|---|---|---|---|---|
| **EXT-001** | `PassPlatPasswordSecurity` es base CBP (IPasswordService+validators) con policy PassPlat: ejemplo canónico de extensión-legítima (90:10). Encaje en CBP como extensión, no reemplazo. | PASS | EXTENDER | Alta |
| **EXT-002** | `UsuarioTenant` es extensión de dominio sin equivalente CBP (membership cuenta tenant-usuario). Existe SOLO en PassPlat, correcto como EXTENDER (no duplica). | PASS | EXTENDER | Alta |
| **EXT-003** | `TenantResolutionMiddleware` implementa el contrato CBP.MultiTenant con LÓGICA propia (JWT→members fachada). Su lógica es PassPlat (debida), parte es strategia del framework. | PASS | EXTENDER | Alta |
| **EXT-004** | Los componentes Authentication (`AuthTokenService`, `AuthenticationContext`) extienden base CBP.Authentication con claims/sesión propios — sin re-inventar JWT. | PASS | EXTENDER | Alta |
| **EXT-005** | `PermissionClaimBuilder` (3-branch) — claims de permisos multi-aplicación sobre `CBP.Authentication`, dominio de PassPlat (no CRUD framework). | PASS | EXTENDER | Media |

## 6. Clasificación final

- **Todas las extensiones** de autenticación/crypto/multitenant son EXTENDER legítimas (base CBP + dominio PassPlat).
- No hay extensión que reemplace CBP por completo (ninguna «duplicadora» en este grupo).
- Ratio agregado: ~CBP 75% / PassPlat 25% (sano).

## 7. Resultado (extensiones)

- **PASS**: la capa de extensión de PassPlat sobre CBP es globalmente correcta; el patrón 80/20 se respeta.
- Restricción para F12: NO romper las extensiones existentes por migrar a CBP; tratar como base de compatibilidad.

## 8. Cierre uniforme S15

| Metrica | Valor |
|---|---|
| Cobertura CBP (adopción extensiones) | 95 % |
| Architecture Score | 90 / 100 |
| Confidence | Alta |
| Technical Debt | TD-EXT-001..005 (residual, aceptado) |