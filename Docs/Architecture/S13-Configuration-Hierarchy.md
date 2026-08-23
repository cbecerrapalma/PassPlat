# S13 — Configuration Hierarchy (F6–F11)

> **Sprint**: S13 — Post-Login Authorization, CBP Compliance & App/Tenant Configuration
> **Fases**: F6→F11 (inventario read-only de configuración App/Tenant/Global)
> **Fecha**: 2026 — Sprint S13
> **Naturaleza**: Solo análisis + documentación. Sin cambios en controladores/SPs.
> **Estado**: COMPLETADO

---

## 1. Modelo de configuración en 3 niveles

PassPlat resuelve configuración en tres niveles con **precedencia App > Tenant > Global**:

| Nivel | Entidad(s) | Alcance |
|-------|-----------|---------|
| **Global** | `ConfigApp` (`IdTenant NULL`), `PoliticaPwd` (`IdTenant NULL` & `IdApp NULL`), `EmailAccount`, `EmailTemplate` (`IdTenant NULL`) | Aplicable a toda la plataforma |
| **Tenant** | `ConfigTenant`, `ConfigApp` (`IdTenant` set), `PoliticaPwd` (`IdTenant` set, `IdApp NULL`), `TenantEmailAccounts`, `ConfSaml`, `ConfLdap`, `ConfProvIden`, `EmailTemplate` (`IdTenant` set) | Aplicable dentro de un tenant |
| **App** | `PoliticaPwd` (`IdTenant`+`IdApp` set), `AppEmailAccounts`, `AppsModulos` | Aplicable dentro de una app |

---

## 2. Resolución de políticas de contraseña (PoliticaPwd)

`PoliticaPwdRepository` (app → tenant → global):

1. `ObtenerPoliticaAplicableAsync(idTenant, idApp)`:
   - Si `idApp.HasValue` → buscar `IdTenant == idTenant && IdApp == idApp.Value && Activa`
   - Si no hay → buscar `IdTenant == idTenant && IdApp == null && Activa`
   - Si no hay → `ObtenerPoliticaGlobalAsync()` (`IdTenant == null && IdApp == null && Activa`)
2. `ObtenerPoliticaGlobalAsync()` — política sin tenant ni app.
3. `ObtenerPoliticaParaRolAsync(idTenant, idRol)` — vía `RolesPoliticasPwd`
   (join `rp.IdTenant == idTenant && rp.IdRol == idRol && rp.Activo`).

**Índices únicos** (schema):
- `UX_Politicas_Global` (filtrado `IdTenant IS NULL AND IdApp IS NULL AND Activa=1`)
- `UX_Politicas_TenantApp` (filtrado `Activa=1`)
- IX `PoliticasPwd (IdTenant)`, IX `PoliticasPwd (IdTenant, IdApp)`

---

## 3. Resolución de configuración clave/valor (ConfigApp)

`ConfigAppRepository`:
- `ObtenerPorClaveAsync(clave, idTenant)` — match exacto `Clave` + `IdTenant`.
- `ObtenerPorGrupoAsync(grupo)` — cacheable (hot groups `Email`, `Branding` con
  TTL 60s via `ICacheService`).
- `ObtenerPorTenantAsync(idTenant)` — lista por tenant.
- `SetValorAsync(grupo, clave, valor, tipo, descripcion, idTenant)`.

`ConfigAppService.SetValorAsync` — siempre con `idTenant=null` (config global).
Cifrado AES-256-GCM context key `ConfigApp:{Clave}`.

**Regla práctica**: `ConfigApp` es clave/valor simple (global o por tenant), sin
`IdApp`. Para configuración app-específica se usan tablas dedicadas
(`AppEmailAccounts`, `AppsModulos`, `PoliticaPwd` con `IdApp`).

---

## 4. Resolución de cuenta SMTP (EmailAccountResolverService)

Precedencia estricta (documentada en código líneas 33–96):

1. **App**: `AppEmailAccounts.ObtenerPorAppAsync(idApp)` → `EsPredeterminada`
   desc → `Id` asc → activo.
2. **Tenant**: `TenantEmailAccounts.ObtenerPorTenantAsync(idTenant)` → misma
   selección.
3. **Global**: `EmailAccounts.ObtenerPredeterminadaAsync()`.
4. **Fallback**: primera cuenta global activa (`ObtenerActivosAsync` → `Id` asc).
5. Sin coincidencia → `EMAIL_NO_ACCOUNT`.

`BuildSmtpConfig` descifra contraseña con `IEncryptionService.Decrypt` (AES-256-GCM).

---

## 5. Configuración fuerte por tenant (ConfigTenant)

`ConfigTenantService.ObtenerPorTenantAsync(idTenant)`:
- `TimeoutSesionMin` (30 por defecto)
- `MaxSesionesConc` (5)
- `ReqMFA` (bool)
- `DiasRetAuditoria` (365)
- `PepperVersionActual` (byte 1)
- `MFAObligatorio` — **obsoleta**, usar `ReqMFA`.

---

## 6. Templates de email por tenant

`EmailTemplateService.ObtenerPorTenantAsync(idTenant)`.
`PassPlatEmailService` resuelve plantilla con `ObtenerPorNombreCulturaAsync(templateCode, "es", job.IdTenant)` —
permite fallback global (template `IdTenant NULL`) o tenant-específico según repo.

---

## 7. Resumen de precedencia

```
App    → Tenant → Global
│          │        │
PoliticaPwd: IdTenant+IdApp → IdTenant+IdApp NULL → IdTenant NULL+IdApp NULL
Email SMTP:  AppEmailAccount → TenantEmailAccount → EmailAccount global
EmailTpl:    IdTenant set → IdTenant NULL (según implementación repo)
ConfigApp:   IdTenant set → IdTenant NULL (match exacto, fallback en consumidor)
```

---

## 8. Hallazgos y recomendaciones

1. **`ConfigApp` sin `IdApp`** — si se requiere config por app, ampliar modelo
   (deuda técnica; no bloquea S13).
2. **`ConfigAppService.SetValorAsync` siempre global** — no permite setear config
   tenant-scoped por clave vía servicio público (deuda, no bloqueante).
3. **`PasswordExpirationBackgroundService`** usa `ObtenerPorTenantAsync(0)` para
   políticas y elige por `IdTenant` con fallback a `IdTenant == null`
   (multi-tenant, A1.5.3.2).
4. **Cache hot groups** (`Email`, `Branding`) con TTL corto (60s) e invalidación
   explícita en `ConfigAppService` (`InvalidarCacheGrupoAsync`) + invalidación de
   `PassPlatEmailService` — coherente con CBP.Caching.
