# S15-Duplication-Audit.md — Análisis de Duplicación vs Framework CBP (Fase F — síntesis)

# Estado          Borrador
# Tipo            ☐ Evidencia ☑ Análisis ☐ Decisión
# Fuente          varios N1
# Depende de      Inventory, Events-Audit, Security-Audit, DI-Audit
# Influye en      Refactoring, Technical-Debt
# Área            Duplicación funcional de PassPlat vs CBP (síntesis post A–E verificación)
# Framework CBP   CBP.Events (EventBase, Dispatcher), CBP.Security.Cryptography (PoliticaPwd), CBP.Caching (ICacheService), CBP.Data, CBP.WebApi
# Cobertura       transversal (todos los subsistemas auditados A–E)
# Evidencia       medida con LOC reales + conteos por capa (secciones 2.1/5)
# Resultado       DUP-MEDIA (duplicación de modelado puntual, no criptografía ni CRUD; acoplada a emails en eventos)
# Cobertura       60 % libre de duplicación

---

## 1. Objetivo (síntesis, NO nuevo hallazgo)

Consolida los hallazgos de duplicación de las fases A–E en un único índice: dónde PassPlat **repite la lógica que CBP ya abstrae** vs donde la reusa correctamente. Utiliza las mediciones por capa ya verificadas. No genera decisiones — las refiere al `S15-Architecture-Decisions.md`.

## 2. Duplicación real (conteos verificados en Fase A–E)

### 2.1 Duplicación de lógica core (que CBP ya abstrae)

| Área | Duplicación | Evidencia (LOC/Fase) | Resultado | Accion | Confidence |
|---|---|---|---|---|---|
| Criptografía / hashing / cifrado | **0** — toda delegada a `CBP.Security.Cryptography` | 0 re-impl (F5) | PASS | REUTILIZAR | Alta |
| CRUD repositorio | **0** — todos `RepositoryAsync<T>` CBP | 57 repos (F4) | PASS | — | Alta |
| Servicios CRUD | **0/53** (53 heredan `ServiceAsync`) | 53/74 (F9) | PASS | — | Alta |
| Generación/validación JWT | **0** — CBP.Authentication | 0 propio (F1) | PASS | — | Alta |
| Pipeline web | **0** — CBP.WebApi | IBaseApi 58/64 (F10) | PASS | — | Alta |
| Caché | **1 registro residual** `AddMemoryCache()` + ICacheService | CACH-001/DI-002 | WARNING | EXTENDER/eliminar | Alta |

### 2.2 Modelos duplicados (deuda de modelado)

| Modelo | Copia PassPlat | Copia CBP | Impacto | Acción | Confidence |
|---|---|---|---|---|---|
| `PoliticaPwd` | `Dominio\Entities\Core\PoliticaPwd.cs` | `CBP...Models\PoliticaPwd.cs` | 2 clases con mismo contrato; riesgo de drift | JUSTIFICAR (o unificar vía shared) | Media |

### 2.3 Duplicación de "eventos" (acoplamiento al email)

| Elemento | Duplicación funcional de CBP.Events |
|---|---|
| 4 eventos (`NewIpDetected, SecurityAlert, NewDevice, DeviceRevoked`) | Definen via `EventBase` correcto, pero el **mecanismo de difusión** (publisher static que encola `EmailJob`) replica —sin reusar— el rol que `IDomainEventDispatcher` provee (COUP-001..004). |
| Publicadores static | `IPEvents.cs:101 LOC`, `DispConfiableEvents.cs:101 LOC` — 2 `public static class` que toman `IEmailQueue` por parámetro |

No es duplicación de lógica (cripto/CRUD/JWT), es **duplicación del patrón de difusión de eventos** que CBP.Events ya abstrae.

## 3. Mapa global de duplicación por capa

| Capa | Duplicación core | Duplicación patrón | Observación |
|---|---|---|---|
| Dominio | modelos compartidos (1) | — | PoliticaPwd |
| Datos | 0 | — | v57 repo base |
| Aplicación | 0 | eventos static acoplado email (COUP) | Max debt |
| WebAPI | 0 | 6 controllers ControllerBase (WEB-004) | menor |

## 4. Duplicación «justificada / no duplicación»

- `PoliticaPwd` en dominio = $ demo de contrato compartido registrado, pendiente unificación.
- `UsuarioTenantRepository` / capa membresia = **no duplicación** (CBP no provee; lógica de dominio PassPlat requerida).
- `PermissionClaimBuilder` 3-branch = no duplicación (dominio tenant).
- `PassPlatPasswordSecurity` wrapper = no duplicación (orquesta validators de CBP).

## 5. Cálculo de duplicación

| Item | Valor |
|---|---|
| Archivos duplicados de lógica core | 0 (cripto/crud/jwt) |
| Registro cache residual | 1 (AddMemoryCache) |
| Modelos duplicados | 2 (PoliticaPwd dom/CBP) |
| Publishs static duplicando bus | 2 |

**Regla anti-falso-duplicado (SBC)**: La extensión legítima (CBP 80% + PassPlat 20%) = `EXTENDER` — nunca `REEMPLAZAR`. Ej: `PassPlatPasswordSecurity` wrapper.

### 5.1 Clasificación de duplicación en 3 tipos (ver `S15-Audit-Methodology.md` §10.1)

| Tipo | Definición | Casos en PassPlat | Decisión |
|---|---|---|---|
| **Funcional** | Repite funcionalidad que CBP ya abstrae sin reutilizarla | 4 eventos publicados vía static (replica del rol de `DomainEventDispatcher`) · 1 caché residual `AddMemoryCache()` junto a `AddCbpCache` | REEMPLAZAR / ELIMINAR |
| **Estructural** | Similar en forma/LOC pero hereda o compone la base CBP (extensión) | `AuthenticationTokenService`, `AuthenticationContext`, `TenantResolutionMiddleware`, `PermissionClaimBuilder` | EXTENDER — nunca REEMPLAZAR |
| **Tecnológica** | Duplica una capacidad de infraestructura (cache/logging/DI) ya ofrecida por CBP | `AddMemoryCache()` + `ICacheService` (CACH-001/DI-002) · `AddSingleton(Log.Logger)` + `AddCbpLogging` (LOG-002) | REEMPLAZAR (unificar contrato) |

Conclusión del eje: la duplicación **funcional/tecnológica** es puntual (2 eventos-publisher, 2 registros DI duplicados); la **estructural** corresponde a extensiones legítimas EXTENDER y NO se consideras duplicación.

## 6. Resultado (duplicación)

- **Duplicación de lógica core: NULA** (cripta/CRUD/JWT/pipeline todos CBP).
- **Duplicación por patrón**: 4 eventos (acoplamientos email), 1 cache residual, 1 modelo — riesgo menor a moderado.
- Cobertura de reuso: **alta** (A-E confirma).

## 7. Cierre uniforme S15

| Metrica | Valor |
|---|---|
| Arquitecture Score (áreas Audit) | 84 / 100 |
| Duplicación total | <5 % archivos |
| Technical Debt | TD-DUP-001..008 |

**Next**: `S15-Extensions-Audit.md` (S15e) — extensiones legítimas de CBP.