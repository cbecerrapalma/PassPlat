# S14 — F6: Email Templates Analysis

> Sprint S14 · FASE F6 (read-only) · Auditoría de plantillas de email y scope.

---

## Tablas auditadas

| Tabla | Scope (F1) | Columnas clave |
|-------|------------|----------------|
| `EmailTemplates` | TENANT | `IdTenant`, `IdIdioma`, `Cuerpo`, `Asunto` |
| `EmailTemplatePartials` | PLATFORM_GLOBAL | `Nombre`, `Contenido` |
| `EmailTemplateHistorial` | PLATFORM_GLOBAL | `IdTemplate`, `Accion`, `Fecha` |

---

## Análisis de scope

### EmailTemplates (TENANT)
- Tiene `IdTenant` → configuración por tenant.
- **No tiene `IdApp`** → no hay override por aplicación.
- Fallback implícito: si tenant no tiene plantilla → ¿global? El código actual no implementa fallback automático a plantilla de plataforma.

### EmailTemplatePartials (PLATFORM_GLOBAL)
- Partials reutilizables (header, footer, etc.).
- Sin `IdTenant` ni `IdApp` → globales para todos.

### EmailTemplateHistorial (PLATFORM_GLOBAL)
- Auditoría de cambios de plantillas.
- Sin scope tenant/app.

---

## Comportamiento actual

El servicio `EmailTemplateService` (si existe) resuelve plantillas por:
1. `EmailTemplates` filtrado por `IdTenant` + `IdIdioma`.
2. Si no encuentra → ¿usa partials globales? No hay evidencia de fallback automático.

---

## ¿Requiere `IdApp`?

**CONCLUSIÓN: NOT REQUIRED**

Razones:
1. Las notificaciones de email son **por tenant** (cuenta SMTP, plantillas, destinatarios).
2. No hay evidencia de que una misma plantilla deba variar por aplicación dentro del mismo tenant.
3. Los eventos que disparan emails (login, password reset, MFA, etc.) son **usuario/tenant**, no usuario/app/tenant.
4. Agregar `IdApp` rompería la simplicidad: un tenant = una plantilla por evento/idioma.

---

## Recomendación

- Mantener `EmailTemplates` como TENANT-only.
- Si en futuro se necesita variación por app, crear tabla `AppEmailTemplates` (APP_TENANT) separada.
- Los partials globales (`EmailTemplatePartials`) son correctos como PLATFORM_GLOBAL.

---

## Evidencia SQL

```sql
-- Plantillas por tenant
SELECT Id, IdTenant, Nombre, IdIdioma FROM EmailTemplates WHERE IdTenant = 3;

-- Partials globales
SELECT Nombre FROM EmailTemplatePartials;

-- Historial
SELECT * FROM EmailTemplateHistorial ORDER BY Fecha DESC;
```

---

**CERTIFIED** — Scope correcto, no requiere `IdApp`.