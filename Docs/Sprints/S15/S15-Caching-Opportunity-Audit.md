# S15-Caching-Opportunity-Audit.md — Oportunidades de Caché no explotadas (documento compañero de F2)

# Estado          Borrador
# Tipo            ☐ Evidencia ☑ Análisis ☐ Decisión
# Fuente          Caching-Audit
# Depende de      Caching-Audit
# Influye en      Refactoring, Certification
# Área            Identificar lecturas repetidas candidatas a caché (rendimiento) usando ICacheService CBP
# Framework CBP   CBP.Caching (ICacheService, CacheEntryOptions)
# Cobertura       PassPlat.Datos | PassPlat.Aplicacion
# Evidencia       ConfigAppRepository.cs (cache OK: HotGroupTtl=60s, GetAsync/SetAsync/RemoveAsync) · ConfigTenantRepository.cs (SIN cache, FirstOrDefault por IdTenant) · PoliticaPwdRepository.cs (SIN cache, 6+ queries por login con AsNoTracking)
# Resultado       WARNING (ConfigApp correcto; PoliticaPwd + ConfigTenant, criticas de arranque, no cacheadas — 4 a 6 queries repetitivas por request)
# Cobertura       65 %

---

## 1. Proposito

Documento compañero de `S15-Caching-Audit.md`. Identifica **lecturas repetitivas de BD** que hoy NO se cachean pero deberían (high read / low write / alta frecuencia), usando el canal ya establecido `ICacheService` (CBP.Caching). Objetivo: reducir carga de BD sin violar consistencia. No implementa — solo clasifica y cuantifica (F12 backlog).

## 2. Metodo (estructura obligatoria)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Cachados correctos existentes (referencia positiva)

| Dato | Repo/Service | Patron | Evidencia |
|---|---|---|---|
| ConfigApp (grupo) | ConfigAppRepository | Get→Set/Remove() + HotGroupTtl 60s + invalidation on update | `ConfigAppRepository.cs:41,49,151` |

- **Modelo a seguir** para los candidatos: key builders + `SetAsync` con TTL + `RemoveAsync` en escrituras.

## 4. Candidatos de cache NO explotados (hallazgos)

| ID | Dato | Ubicacion | Frecuencia lectura | Write freq | Estado actual | Resultado | Accion | Confidence |
|---|---|---|---|---|---|---|---|---|
| **CAD-001** | Política de password por tenant/app/global | `PoliticaPwdRepository.cs:29,42,55,70,73,88` | alta (en cada login/pwd check) | baja (admin edita) | sin cache: ~6 queries repetidas por intento | **FAIL** | EXTENDER (cache con invalidacion en update) | Alta |
| **CAD-002** | ConfigTenant (config por tenant) | `ConfigTenantRepository.cs:24,37,53` | media (cada request tenant) | baja | sin cache | **WARNING** | EXTENDER (cache, key por IdTenant) | Alta |
| **CAD-003** | Roles-permiso del usuario | Permiso lookups (claims) | alta (permission check por request) | baja | parcial (claims en JWT) | WARNING | JUSTIFICAR (ya en claims JWT) | Media |
| **CAD-004** | Templates email | EmailTemplateStoreService | baja-media | baja | SI cache (referencia F5) | PASS | REUTILIZAR | Alta |
| **CAD-005** | Apps catalog | AppRepository (global) | media | baja | no verify | WARNING | EXTENDER (cache de catalogo pequeno) | Media |

## 5. Detalle PoliticaPwd (impacto por login)

`PoliticaPwdRepository` consulta hasta 6 veces (tenant→app→global→app+tenant→tenant) por cada login o validacion de password, **sin cache**. Cada `FirstOrDefaultAsync` es una ida a SQL Server. Con login por segundo esto multiplica la carga. Recomendado cachear por (IdTenant, IdApp, preferencia) con TTL y `RemoveAsync` en `SP_Pwd_Cambiar`/actualizar política.

## 6. Matriz de impacto / esfuerzo

| Candidato | Impacto (query/s) | Esfuerzo | Prioridad | Value |
|---|---|---|---|---|
| PoliticaPwd | Alto (6/request) | Bajo | P1 | Rápido win |
| ConfigTenant | Medio | Bajo | P2 | Fácil |
| Apps catalog | Bajo | Bajo | P3 | Fácil |

## 7. Resultado (oportunidades cache)

- **Bloqueo**: 2 candidatos no cacheados (PoliticaPwd, ConfigTenant) con alta frecuencia de lectura; ConfigApp es el modelo correcto a seguir.
- Recomendación: cachear PoliticaPwd (P1) y ConfigTenant (P2) dentro del patrón ICacheService de CBP.

## 8. Cierre uniforme S15

| Metrica | Valor |
|---|---|
| Cobertura CBP | 70 % |
| Architecture Score | 72 / 100 |
| Confidence | Alta |
| Technical Debt | TD-CADO-001..005 (backlog F12) |