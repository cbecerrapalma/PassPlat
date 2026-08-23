# S15-Caching-Audit.md — Cache (F2)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          CBP-Inventory
# Depende de      Inventory
# Influye en      Caching-Opportunity, Certification
# Area            Cache distribuida / memoria (F2)
# Framework CBP   CBP.Caching.Abstractions (ICacheService), CBP.Caching.Memory (MemoryCacheProvider), CBP.Caching.Redis, CBP.Caching.NCache, CBP.Caching
# Cobertura       PassPlat.Aplicacion | PassPlat.Datos | PassPlat.WebAPI
# Evidencia       ICacheService inyectado en 8+ sitios (ConfigAppRepository, ExternalAuthService, EmailTemplateStoreService, DashboardEnterpriseService, JwksStore, ExternalAuthController, TenantResolutionMiddleware, MfaCodeStore) · Program.cs:149 AddMemoryCache + :150 AddCbpCache (DI-002) · IMemoryCache directo SOLO en Program.cs:149 · 0 ConcurrentDictionary fuera de permitidos
# Resultado       PASS (ICacheService de CBP.Caching es el unico canal; RAM Cache directa de bajo riesgo; regla 18 de AGENTS cumplida salvo AddMemoryCache)
# Cobertura       80 % (ver F11)
# Riesgo          Bajo
# Prioridad       Media

---

## 1. Proposito

Auditar el uso de cache en PassPlat: que componente de CBP.Caching se usa, si hay caches paralelos (IMemoryCache, ConcurrentDictionary, static collections), y cuantos servicios inyectan ICacheService. Verificar cumplimiento de la regla 18 de AGENTS.md (cache exclusiva via CBP.Caching).

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Compontes CBP.Caching

| Componente CBP | Rol en PassPlat | Evidencia |
|---|---|---|
| `ICacheService` (CBP.Caching.Abstractions) | Interfaz inyectada en los servicios que cachean | grep ICacheService |
| `CBP.Caching.Memory` (MemoryCacheProvider) | Provider de memoria local usado en dev | `Program.cs:150 AddCbpCache(UseLocal MemoryCacheProvider)` |
| `CBP.Caching.Redis` | Provider distribuido (configurado para prod, ej. OAuth state) | Program.cs / AplicacionDI:108-109 |

## 4. Quien usa ICacheService (CBP.Caching)

| Servicio | Dato cacheado | Evidencia |
|---|---|---|
| `ConfigAppRepository` | ConfigApp (grupo) | `:27 ICacheService _cache` |
| `ExternalAuthService` | OAuth state/session/anti-replay | `:49,65` |
| `DashboardEnterpriseService` | KPIs de dashboard | `:55,69` |
| `EmailTemplateStoreService` | Templates de email | `:21,28` |
| `JwksStore` | JWKS (kid rotation + stale fallback) | `:14,29` |
| `ExternalAuthController` | blobs de config | `:25,37` |
| `TenantResolutionMiddleware` | config tenant resuelta | `:12` |
| `MfaCodeStore` | codigos MFA (TOTP en memoria) | `:24,27` |

Total: **8 servicios** consumen `ICacheService` (CBP.Caching). Esto es una adopcion real y consistente del framework.

## 5. MFA CodeStore

| Hallazgo | Evidencia | Clasificacion |
|---|---|---|
| `MfaCodeStore` implementa `IMfaCodeStore` y usa `ICacheService` (no `IMemoryCache` aunque anteriormente era `IMemoryCache`). | `MfaCodeStore.cs:24` | PASS |
| Almacenamiento en memoria (MemoryCacheProvider) — se pierde al reiniciar el API | comentario AGENTS "en memoria se pierde al reiniciar API" | WARNING (documentado) |

## 6. Hallazgos de caches paralelos no-CBP

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **CACH-001** | `builder.Services.AddMemoryCache()` (IMemoryCache) registrado en Program.cs:149 — cache paralelo al ICacheService. Esta es la unica referencia directa a IMemoryCache fuera de CBP. | `Program.cs:149` | **WARNING** (deuda, parece redundante con AddCbpCache:150) |
| **CACH-002** | `AddCbpCache` (CBP) con `MemoryCacheProvider` en :150 — el cache CORRECTO. Doble cache = DI-002 ya documentado en F9.2/D9.2-002. | `Program.cs:150` | WARNING (mismo DI-002) |
| **CACH-003** | `ConcurrentDictionary` = 0 en PassPlat Aplicacion/WebApi/Datos (se elimino en Sprint A). Regla #7/18 cumplida. | grep = 0 | PASS |
| **CACH-004** | Redis listo para prod (`CBP.Caching.Redis` + AddDistributedMemoryCache comment) — escalable, cumple regla 💼7/18. | `AplicacionDI.cs:108-109`, Program | PASS |
| **CACH-005** | JwksStore conserva stale fallback + kid rotation + statistics via ICacheService — maduro. | `OAuth/JwksStore.cs` | PASS |

## 7. Clasificacion final

| Cluster | Clasificacion |
|---|---|
| Interfaz `ICacheService` de CBP | REUTILIZAR (PASS) — unico canal |
| Provider Memory | REUTILIZAR (PASS) |
| Redis (multi-tenant, prod) | REUTILIZAR (PASS) |
| `IMemoryCache` directo (AddMemoryCache :149) | REUTILIZAR → eliminar/extraer (WARNING) |
| API Express | — |

## 8. Resultado F2
- **PASS**: PassPlat adoptó `ICacheService` (CBP.Caching) como unico canal de cache, salvo la registracion residual `AddMemoryCache()` en Program.cs:149 (CACH-001) que convive con `AddCbpCache` (DI-002/F9.2).
- Duplicacion: **1 registro accidental** (AddMemoryCache duplica AddCbp).
- Insumos F12: eliminar `AddMemoryCache()` o documentar su necesidad; para regla de program sin `IMemoryCache` en negocio ya se cumple.

### 8.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| CACH-001 | WARNING | REEMPLAZAR (eliminar AddMemoryCache duplicado) | Baja | P2 | Alta |
| CACH-002 | WARNING | JUSTIFICAR (mismo DI-002) | Baja | P2 | Alta |
| CACH-003 | PASS | REUTILIZAR (regla 18 cumplida) | — | — | Alta |
| CACH-004 | PASS | REUTILIZAR (Redis prod listo) | — | — | Alta |
| CACH-005 | PASS | REUTILIZAR (JwksStore maduro) | — | — | Alta |
| MFA store | PASS | REUTILIZAR (ICacheService) | — | — | Alta |

### 8.2 Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 80 % |
| Architecture Score | 78 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-CACH-001..005 |

**Ver tambien**: `S15-Caching-Opportunity-Audit.md` — oportunidades de cache no explotadas (rendimiento).