# S14 — F12/F13: Migraciones y Seeds

> Sprint S14 · FASE F12/F13 (condicional) — Solo si F1–F11 detectan necesidad real.

---

## Evaluación F1–F11

| Fase | Cambio de esquema | Datos semilla | Acción |
|------|-------------------|---------------|--------|
| F1 Scope Matrix | No | No | Documentación |
| F2 Hierarchy | No | No | Documentación |
| F3 OAuth Fix | No (solo código C#) | No | `ExternalAuthService.cs:289` |
| F4 OAuth Cert | No | No | Tests manuales/Playwright |
| F5 Email Resolution | No | No | Documentación |
| F6 Email Templates | No | No | Documentación |
| F7 ConfigApp | No | No | Documentación |
| F8 Resolvers | No | No | Documentación (deuda: PasswordPolicyResolver) |
| F9 Cache | No | No | Documentación |
| F10 CBP.Events | No | No | Documentación (gap) |
| F11 SP Sync | No | No | Verificación (mojibake SP_Usuario_Crear) |

---

## Conclusión

**NO HAY NECESIDAD REAL DE MIGRACIONES NI CAMBIOS DE SEMILLA**

- F3 es **único cambio funcional** y es solo código C# (`ExternalAuthService.cs:289`).
- F11 detecta solo **mojibake en literales** de `SP_Usuario_Crear` (encoding), no cambio funcional.
- Ninguna fase F1–F11 requiere:
  - `CREATE TABLE` / `ALTER TABLE` / `DROP COLUMN`
  - `ADD/DROP CONSTRAINT` / `INDEX`
  - `INSERT/UPDATE/DELETE` en tablas catálogo/config
  - Cambios en `SEED_Plataforma.sql` / `SEED_Tenant.sql`

---

## Decisión

| Fase | Estado | Comentario |
|------|--------|------------|
| **F12** | **MIGRATION NOT REQUIRED** | Sin cambios de esquema detectados |
| **F13** | **SEEDS COMPATIBLES** | Seeds actuales válidos; no requieren regeneración |

---

## Próximo sprint (si procede)

Si en futuro se centraliza `PasswordPolicyResolver` (F8 deuda) y requiere tabla/cache nueva, entonces:
1. Generar `Docs/Architecture/S14-Migration-Plan.md` (solo entonces).
2. Ejecutar migración controlada.
3. Actualizar seeds si nueva tabla catálogo.

---

**F12/F13 CERRADAS** — Sin acción requerida en S14.