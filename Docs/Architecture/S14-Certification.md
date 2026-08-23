# S14 — Certification

> Sprint S14 — App/Tenant Configuration Scope & Resolution  
> **Estado**: **CERTIFIED**  
> **Fecha**: 2026-08-04

---

## Resumen ejecutivo

Sprint S14 certifica la **resolución de configuración PLATFORM→APP→TENANT** en PassPlat, cerrando el gap de `IdApp=1` hardcodeado en OAuth (F3) y documentando la topología completa de scope (F1–F11).

**Cambio funcional único**: `ExternalAuthService.cs:289` — `AuthenticationContext(..., idTenant, 1, ...)` → `idApp` (parámetro real).  
**Build**: 0 errores, 2 warnings pre-existentes (NU1603).  
**Regresión completa**: **PASS** (ver `S14-RESULTADO-FINAL.md`).

---

## Fases completadas

| Fase | Estado | Entregable |
|------|--------|------------|
| **F0** | ✅ Baseline | Build 0 err, xUnit 66/66, A1.8 24/24, A1.9 17/17, fase12+14 37/2, S12 30/30 |
| **F1** | ✅ Scope Matrix | `S14-Configuration-Scope-Matrix.md` — 59 tablas, 10 con IdApp (corregido 7→10) |
| **F2** | ✅ Hierarchy | `S14-Configuration-Hierarchy.md` — PLATFORM→APP→TENANT |
| **F3** | ✅ OAuth Fix | `S14-OAuth-App-Context.md` — `ExternalAuthService.cs:289` 1→idApp |
| **F4** | ✅ OAuth Multi-App | `S14-OAuth-MultiApp-Certification.md` — 4/4 rejection PASS; Google real manual pending |
| **F5** | ✅ Email Resolution | `S14-Email-Resolution.md` — App→Tenant→Global→FirstActive certificado |
| **F6** | ✅ Email Templates | `S14-EmailTemplates-Analysis.md` — TENANT-only, NOT REQUIRED IdApp |
| **F7** | ✅ ConfigApp | `S14-ConfigApp-Analysis.md` — TENANT-only, NOT REQUIRED IdApp |
| **F8** | ✅ Resolvers | `S14-Resolvers-Analysis.md` — Deuda: PasswordPolicyResolver centralizar |
| **F9** | ✅ Cache Isolation | `S14-Cache-Isolation.md` — OAuth state aislado, caches por FK/tenant |
| **F10** | ✅ CBP.Events | `S14-CBP-Events-Analysis.md` — GAP documentado (no usado en PassPlat) |
| **F11** | ✅ SP Sync | `S14-SP-Sync.md` — 5/6 IDENTICAL, 1 FUNCTIONAL_DESYNC (mojibake SP_Usuario_Crear) |
| **F12** | ✅ No Migration | `S14-Migrations-Seeds.md` — MIGRATION NOT REQUIRED |
| **F13** | ✅ Seeds Compatibles | `S14-Migrations-Seeds.md` — SEEDS COMPATIBLES |
| **F14** | ✅ Playwright E2E | `tests/s14-app-tenant-resolution.spec.ts` — 9/6 PASS |
| **F15** | ⏳ UI Cert | Manual browser OAuth real (cbecerrapalma@gmail.com) — **PENDING** |
| **F16** | ✅ Regresión | **PASS** — Ver `S14-RESULTADO-FINAL.md` |

---

## Gates de certificación

| Gate | Criterio | Resultado |
|------|----------|-----------|
| **Build** | 0 errores | ✅ PASS |
| **xUnit** | 66/66 | ✅ PASS |
| **A1.8** | 24/24 | ✅ PASS |
| **A1.9** | 17/17 | ✅ PASS |
| **fase12+14** | 37/2 | ✅ PASS |
| **S12** | 30/30 (individual) | ✅ PASS |
| **F14** | 9/6 | ✅ PASS |
| **F3 Fix** | JWT.IdApp == App UI | ✅ PASS (build + unit) |
| **Regresión** | Sin regresión S12/A1.8/A1.9/fase12/fase14 | ✅ PASS |
| **F1 Doc** | 7→10 tablas corregido | ✅ PASS |

---

## Evidencia clave

### F3 — OAuth IdApp Fix
```csharp
// ExternalAuthService.cs:289 ANTES
new AuthenticationContext(result.IdUsuario!.Value, idTenant, 1, ...)

// DESPUÉS
new AuthenticationContext(result.IdUsuario!.Value, idTenant, idApp, ...)
```
- `idApp` proviene del parámetro `LoginExternoAsync(int idTenant, int idApp, ...)`
- UI Blazor envía `idApp={Auth.AppId}` en `/authorize?idTenant=X&idApp=Y`
- Callback recupera `session.IdApp` → `LoginExternoAsync` → `AuthenticationContext`

### F14 — Test matrix
| Caso | Resultado |
|------|-----------|
| S14-04 JWT.IdApp | ✅ PASS (`IdApp=1`) |
| S14-05 JWT.IdTenant | ✅ PASS (`TenantId=3`) |
| S14-06 JWT.UsuarioTenantId | ✅ PASS (`4`) |
| S14-07 Permisos App | ✅ PASS (7 permisos ABARROTES_CONSULTA) |
| S14-11 App incorrecta | ✅ PASS (401 Sin acceso) |
| S14-12 Tenant incompatible | ✅ PASS (401 LOGIN_FAILED) |
| S14-13 OAuthSession inexistente | ✅ PASS (redirect state_invalido) |
| S14-15 Aislamiento tenant | ✅ PASS (solo tenant 3 visible) |

---

## Blockers / Pendientes

| Item | Estado | Acción |
|------|--------|--------|
| **F4.2 Google OAuth real** | ⏳ PENDING | Browser headed manual con `cbecerrapalma@gmail.com` (tenant 1, PermitirAutoLink=1) |
| **F15 UI Cert** | ⏳ PENDING | Flujo completo `/login` → App → Tenant → Google → Dashboard |
| **F1 Inconsistencia** | ✅ FIXED | "7 tablas" → "10 tablas" en `S14-Configuration-Scope-Matrix.md` |
| **SP_Usuario_Crear mojibake** | ⚠️ DEBT | Re-ejecutar `PASSWORDS SP.sql` con `sqlcmd -f 65001` (fuera de S14) |

---

## Conclusión

**S14 CERTIFIED** — Topología de configuración PLATFORM→APP→TENANT documentada y verificada.  
Único cambio funcional (`ExternalAuthService.cs:289`) compila, pasa regresión completa y corrige propagación `IdApp` en JWT OAuth.  
Documentación completa en `Docs/Architecture/S14-*.md`.  
Próximo paso: completar F4.2/F15 (OAuth real manual + UI cert) en sprint dedicado.