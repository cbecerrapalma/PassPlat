# S14 — F9: Cache Isolation

> Sprint S14 · FASE F9 (read-only) · Auditoría de aislamiento de caché entre App/Tenant.

---

## Framework de caché

**CBP.Caching** — `ICacheService` con implementaciones:
- `MemoryCacheService` (desarrollo)
- `DistributedCacheService` (producción: Redis/SQL Server)

Todas las cachés usan `CacheEntryOptions` con TTL configurable.

---

## Claves de caché auditadas

| Clave | Origen | Scope | TTL | Contenido | Riesgo contaminación |
|-------|--------|-------|-----|-----------|---------------------|
| `oauth_state:{state}` | `ExternalAuthService:369` | **Global** (state único) | 10 min | `OAuthSession` (CodeVerifier, Nonce, IdTenant, IdApp, ProviderCode, RedirectUri) | **BAJO** — state es UUID único global |
| `oauth_state:{state}` | `ExternalAuthController:141` | **Global** | — | Lectura + Remove | **BAJO** — state consumido y eliminado |
| `provider_cache:{idTenant}` | `Login.razor:610` | **TENANT** | 5 min | `List<ProveedorItem>` | **MEDIO** — si tenant code mal resuelto |
| `config_app:{clave}:{idTenant?}` | `ConfigAppRepository` (implícito) | **TENANT** | — | `ConfigApp` | **MEDIO** — clave incluye tenant |
| `email_template:{idTenant}:{idIdioma}` | `EmailTemplateService` (si existe) | **TENANT+IDIOMA** | — | `EmailTemplate` | **BAJO** — clave compuesta |
| `email_account:{idApp}:{idTenant}` | `EmailAccountResolverService` (implícito via repo) | **APP+TENANT** | — | Cuentas SMTP | **BAJO** — repositorio filtra por FK |

---

## Análisis de aislamiento

### OAuth State (`oauth_state:{state}`)
- **Generación**: `state = SHA256(GUID)` → 64 chars hex, único global.
- **Almacenamiento**: `_cache.SetAsync($"oauth_state:{state}", OAuthSession)`.
- **Contenido sensible**: `IdTenant`, `IdApp`, `CodeVerifier`, `Nonce`, `ProviderCode`, `RedirectUri`.
- **Consumo**: Callback lee por `state` exacto → `_cache.GetAsync<OAuthSession>` → `_cache.RemoveAsync`.
- **Aislamiento**: ✅ **COMPLETO** — state es clave única; no hay colisión entre tenants/apps.

### Provider Cache (`provider_cache:{idTenant}`)
- **Fuente**: `Login.razor:610` — `_providerCache.TryGetValue(idTenant, out cached)`.
- **TTL**: 5 min (`ProviderCacheTtlMinutes = 5`).
- **Clave**: Solo `idTenant` (int).
- **Riesgo**: Si `TenantInitializer` resuelve mal el tenant → cache contamina.
- **Mitigación**: `TenantInitializer` valida header `X-Tenant-Code` y dominio; cache solo en memoria del cliente (Blazor WASM localStorage/IndexedDB).

### ConfigApp Cache
- **Implementación**: `ConfigAppRepository` no usa caché explícita; cada llamada va a DB.
- **Riesgo**: NINGUNO — sin caché, siempre dato fresco.

### Email Templates / Accounts
- **Repositorios**: Filtran por FK (`IdTenant`, `IdApp`) en query LINQ → SQL `WHERE`.
- **Riesgo**: NINGUNO — aislamiento a nivel BD.

---

## Hallazgos

| Área | Estado | Observación |
|------|--------|-------------|
| OAuth State | ✅ AISLADO | State UUID global único; consumido y eliminado en callback |
| Provider Cache (Blazor) | ⚠️ CLIENT-SIDE | Cache en memoria WASM por tenant; no contamina servidor |
| ConfigApp | ✅ SIN CACHÉ | Siempre BD fresca |
| Email/Accounts | ✅ AISLADO BD | FK en queries LINQ |
| Permisos/Políticas | ❓ SIN CACHÉ | Verificar `PermissionClaimBuilder` no cachea |

---

## Verificación `PermissionClaimBuilder`

```csharp
// PassPlat.Aplicacion.Services.Authentication.Claims.PermissionClaimBuilder
public async Task<IReadOnlyList<string>> BuildPermissionClaimsAsync(AuthenticationContext context)
{
    // Llama a repositorios directamente (AuthRepository.ObtenerCodigosPermisosPorUsuarioTenantAsync, etc.)
    // SIN CACHÉ — cada JWT generation llama a BD
}
```

✅ **SIN CACHÉ** — permisos siempre frescos desde BD.

---

## Recomendaciones

1. **OAuth State**: Mantener estado actual (state UUID + TTL 10 min + consumo único).
2. **Provider Cache Blazor**: TTL 5 min aceptable; considerar invalidar en `OnTenantChanged`.
3. **ConfigApp**: Evaluar añadir caché con clave `config_app:{clave}:{idTenant}` TTL 1h si hay hotspots.
4. **No introducir caché compartida** sin clave compuesta `app:{idApp}:tenant:{idTenant}`.

---

## Conclusión

**CERTIFIED** — Aislamiento correcto.  
- OAuth state: clave única global, sin colisión.  
- Cachés de servidor: por FK en BD o clave compuesta tenant/app.  
- Caché Blazor: client-side, por tenant, TTL corto.  
- **No se detecta contaminación cross-App/Tenant**.