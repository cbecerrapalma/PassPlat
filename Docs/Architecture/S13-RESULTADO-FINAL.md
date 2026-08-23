# S13 — RESULTADO FINAL

> Sprint S13 — Post-Login Authorization, CBP Compliance & App/Tenant Configuration.
> Fecha de cierre: 2026-08-03 · Estado: ✅ **CERTIFICADO**

---

## 1. Resumen ejecutivo

Sprint S13 eliminó los 403 del Dashboard post-login (clasificados `CONFIG` y resueltos con
graceful degradation + gates), certificó el runtime oficial (`https://localhost:5001/api`,
`https://localhost:7275`) sobre HTTPS con JWT verificado, audió la adopción CBP (9/10 áreas ✅),
analizó el alcance App/Tenant y la jerarquía de configuración (read-only), completó la
regresión completa (build + xUnit + 5 suites E2E) y verificó la sincronización de los 6 SP
vigilados contra `PASSWORDS SP.sql` (0 divergencias funcionales).

## 2. Entregables por fase

| Fase | Entregable | Estado |
|------|-----------|--------|
| F1 | Baseline canónico (puertos, JWT, cadena Browser→API→DB) | ✅ |
| F2/F3 | 403 Dashboard resueltos vía gates (`_iamAccess`/`_monitoreoAccess`) | ✅ |
| F4 | Matriz JWT interno vs OAuth (mismo emisor, única deuda `IdApp=1` OAuth) | ✅ |
| F5 | Auditoría CBP — 9/10 ✅, 1 ⚠️ CBP.Events | ✅ |
| F6–F11 | Análisis App/Tenant + jerarquía configuración (read-only) | ✅ |
| F12/F13 | E2E Dashboard — **5/5 PASS** | ✅ |
| F14 | Regresión completa — build 0 err · xUnit 66/66 · E2E 41+37+2 | ✅ |
| F15 | SQL Sync — 6 SP vigilados, 0 divergencias funcionales | ✅ |
| F16 | Documentación + reporte final | ✅ |

## 3. Evidencia de regresión

| Gate | Resultado | Detalle |
|------|-----------|---------|
| `dotnet build PassPlat.slnx` | ✅ 0 errores | 2 warnings NU1603 pre-existing |
| `dotnet test PassPlat.Aplicacion.Test.csproj` | ✅ **66/66** | xUnit, 51s |
| `faseA19` + `faseA18` | ✅ **41/41** (17+24) | Gates A1.9 + A1.8 |
| `fase12` + `fase14` | ✅ **37 passed, 2 skipped** | Federación UI + Identidades |
| `s13-dashboard-e2e.spec.ts` | ✅ **5/5** | Dashboard Operacional + IAM |

Ningún gate de certificación (S12, A1.8, A1.9, fase12, fase14) presentó regresión → sin STOP.

## 4. Correcciones realizadas en S13

1. **403 Dashboard post-login** (`DashboardOperacional.razor`, `IamDashboard.razor`):
   gates `_iamAccess`/`_monitoreoAccess` — clasificación `CONFIG`, graceful degradation.
   Endpoints gateados ya no se llaman desde UI.
2. **Playwright 404** en `s13-dashboard-e2e.spec.ts`: eliminado prefijo `api/` duplicado en
   constantes (la base `API` ya incluye `/api`).

## 5. Deuda técnica registrada (fuera de alcance S13)

| Deuda | Ubicación | Propuesta |
|-------|-----------|-----------|
| `IdApp=1` hardcodeado en flujo OAuth | `ExternalAuthService.cs:289` | Parametrizar desde App activa del proveedor |
| CBP.Events no usado para despacho de dominio (EmailQueue directo) | Email subsystem | Migrar a `DomainEventDispatcher` |
| Mojibake en literales de 2 SPs del canónico | `PASSWORDS SP.sql` (`SP_ProvIden_VincularUsuario`, `SP_Usuario_Crear`) | Regenerar archivo desde BD (`SCRIPT AS CREATE`) |

## 6. Documentación generada

| Doc | Contenido |
|-----|-----------|
| `Docs/Architecture/S13-Environment-Baseline.md` | F1 — puertos, secretos, JWT, pipeline HTTP |
| `Docs/Architecture/S13-F4-JWT-Internal-vs-OAuth.md` | F4 — comparativa emisores |
| `Docs/Architecture/S13-JWT-Internal-vs-OAuth-Matrix.md` | F4 — matriz de claims |
| `Docs/Architecture/S13-CBP-Compliance.md` | F5 — auditoría CBP 9/10 |
| `Docs/Architecture/S13-App-Tenant-Scope-Analysis.md` | F6–F11 — alcance App/Tenant |
| `Docs/Architecture/S13-Configuration-Hierarchy.md` | F6–F11 — jerarquía Global/Tenant/App |
| `Docs/Architecture/S13-F15-SP-Sync.md` | F15 — sync de 6 SP vigilados |
| `Docs/Architecture/S13-RESULTADO-FINAL.md` | Este reporte |

## 7. Checklist de cierre

- [x] 403 post-login eliminados con evidencia (sin 4xx en dashboards E2E)
- [x] Runtime oficial certificado (5001 HTTPS / 7275 HTTPS)
- [x] Auditoría CBP documentada (9/10, gap registrado)
- [x] Alcance App/Tenant analizado (read-only, sin cambios de código)
- [x] 5/5 tests E2E Dashboard
- [x] Regresión completa verde: build 0 err · 66/66 xUnit · 41/41 A1.8+A1.9 · 37/37 fase12+14
- [x] 22/22 SPs presentes · 0 divergencias funcionales · encoding defect documentado
- [x] Deuda técnica registrada para sprints futuros

---

*Sprint S13 cerrado el 2026-08-03. Build 0 errores / 0 warnings nuevas.*
