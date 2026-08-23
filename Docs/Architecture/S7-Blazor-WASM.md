# S7 — Blazor WASM + MudBlazor: Lecciones Aprendidas

**Estado**: ✅ COMPLETADO  
**Sprint**: S7 (2026-07-30)  
**Propósito**: Documentar no-determinismo y patrones de Blazor WASM + MudBlazor encontrados durante la estabilización de tests Playwright.

---

## 1. MudBlazor Tab Overlay Bug

### Síntoma

`page.getByRole('tab', { name: 'Seguridad' }).click({ force: true })` no activa el tab panel correspondiente. El `ActivePanelIndex` no cambia.

### Causa

El componente `MudTabs` renderiza `<div class="mud-tabs-panels">` como un overlay flotante sobre los tabs. Este overlay intercepta pointer events — incluso con `force: true` en Playwright, el evento click no llega al tab button interno de MudBlazor.

### Solución

Usar JavaScript `dispatchEvent` para disparar el click directamente en el elemento del DOM:

```typescript
// Helper function
async function clickTab(page: Page, tabName: string) {
  const tab = page.getByRole('tab', { name: tabName });
  await tab.waitFor({ state: 'visible' });
  await page.evaluate((name) => {
    const tab = document.querySelector(`[role="tab"][name="${name}"]`)
      ?? Array.from(document.querySelectorAll('[role="tab"]'))
           .find(t => t.textContent?.trim() === name);
    if (tab) {
      tab.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
    }
  }, tabName);
}
```

### Recomendación

Siempre que un test interactúe con `MudTabs`, usar `dispatchEvent` en lugar de `click()`. No confiar en `force: true` para tabs de MudBlazor.

---

## 2. Contenido Condicional (Componentes sin Datos)

### Síntoma

Tests verificaban existencia de KPIs, tablas o secciones que no aparecían en el renderizado. Ej: "Total de sesiones activas", "Top 10 IPs", donut chart "Dispositivos por SO".

### Causa

Los componentes Blazor del dashboard usan `@if (items.Count > 0)` para decidir si renderizar una sección. Si la consulta a DB devuelve 0 filas, el nodo DOM simplemente no existe.

### Patrón típico

```razor
@if (KpiData != null)
{
    <MudPaper>
        <MudText>@KpiData.Valor</MudText>
        <MudText>@KpiData.Label</MudText>
    </MudPaper>
}
```

### Solución

- Verificar primero si hay datos en DB antes de asumir que un KPI/tabla se renderiza
- Usar datos seed conocidos para las secciones que se quieren testear
- Si no hay datos, probar solo que el endpoint retorna 200 y la página no muestra error

---

## 3. NavMenu Duplicación por Drawer

### Síntoma

Locators como `page.locator('text=Auditoría')` encontraban múltiples elementos (2+), causando ambigüedad en Playwright.

### Causa

MudBlazor Drawer (menú lateral) clona el NavMenu al abrir/cerrar. El NavMenu existe tanto en el Drawer oculto como en el visible, resultando en nodos duplicados.

### Solución

Siempre scoping locators a un contenedor específico:

```typescript
// ❌ Ambiguo — encuentra 2+ elementos
page.locator('text=Auditoría')

// ✅ Scoped — sin ambigüedad
page.locator('.mud-main-content').locator('text=Auditoría')
page.locator('header').locator('text=Auditoría')
```

---

## 4. Carga Lenta Blazor WASM

### Síntoma

Tests que navegaban a una página Blazor y luego intentaban interactuar fallaban porque los componentes no estaban renderizados.

### Causa

Blazor WASM descarga el runtime .NET + assemblies + compila + renderiza. Esto toma tiempo significativo (2-5s incluso en localhost). Playwright `waitFor` no es suficiente porque el DOM inicial ya existe (shell de la app).

### Solución

```typescript
await page.goto(`${WEB_BASE}/dashboard/enterprise`);
await page.waitForTimeout(5000); // Esperar carga Blazor WASM
```

### Recomendación

- Usar `waitForTimeout(5000)` después de `page.goto` para páginas Blazor
- Monitorear console logs para detectar errores de carga Blazor
- Verificar que el elemento `app-loading` desapareció (indica Blazor listo)

---

## 5. Centralización de WEB_BASE

### Problema

El puerto Blazor estaba hardcodeado en 5 archivos de test. Uno usaba puerto legacy incorrecto (5258 en lugar de 5273).

### Solución

Centralizar en `tests/api-config.ts`:

```typescript
export const API_BASE = process.env.API_BASE_URL ?? 'http://localhost:5000/api';
export const API = API_BASE;
export const WEB_BASE = process.env.WEB_BASE_URL ?? 'http://localhost:5273';
```

Override vía:
```powershell
$env:WEB_BASE_URL='http://localhost:7275'
$env:API_BASE_URL='http://localhost:5001/api'
npx playwright test ...
```

---

## 6. Data Corruption Silenciosa (tinyint NULL vs byte)

### Síntoma

`DashboardEnterpriseService` endpoints retornaban 500 sin stack trace claro. El try-catch capturaba la excepción pero el endpoint igual fallaba.

### Causa

`IdenExt.IdEstado` es `tinyint NULL` en SQL Server pero `byte` (non-nullable, default 2) en la entidad C#. Una fila tenía `IdEstado IS NULL`, causando `InvalidOperationException` al materializar con EF Core.

### Lección

- Validar que todas las columnas NOT NULL en C# tengan CHECK constraints o defaults en SQL
- En casos de esquema legacy donde SQL permite NULL pero C# no tolera nullable, agregar validación periódica
- Monitorear logs de excepciones de materialización EF Core — son fáciles de ignorar porque se tragan en try-catch generales

---

## 7. api-config.ts como Fuente Única de URLs

### Patrón implementado

```typescript
// tests/api-config.ts
export const API_BASE = process.env.API_BASE_URL ?? 'http://localhost:5000/api';
export const API = API_BASE;
export const WEB_BASE = process.env.WEB_BASE_URL ?? 'http://localhost:5273';
```

### Archivos que importan de api-config.ts

| Archivo | Usa |
|---------|-----|
| `faseA18-multitenant-gate.spec.ts` | API_BASE |
| `faseA19-switch-to-platform.spec.ts` | API_BASE |
| `fase13-usuario-sin-email.spec.ts` | API_BASE |
| `fase14-federacion-identidades.spec.ts` | API_BASE |
| `fase15-usuarios-enterprise.spec.ts` | API_BASE |
| `fase12-federacion-ui.spec.ts` | API_BASE + WEB_BASE |
| `fase17-dashboard-enterprise.spec.ts` | API_BASE + WEB_BASE |
| `e2e.spec.ts` | API_BASE + WEB_BASE |
| `_diag.spec.ts` | API_BASE + WEB_BASE |
| `_dump.spec.ts` | API_BASE + WEB_BASE |
| + otros tests API | API_BASE |

---

## 8. Contrato PassPlat.Web/appsettings.json

### Problema

`wwwroot/appsettings.json` tenía `ApiBaseUrl: https://localhost:5001` pero la API real corre en `http://localhost:5000`. Blazor intentaba conectar a puerto 5001 HTTPS, que no respondía.

### Fix

```
ApiBaseUrl: "https://localhost:5001"  →  "http://localhost:5000"
```

### Lección

- `appsettings.json` en Blazor WASM es estático (se sirve como archivo al navegador)
- No hay server-side rewrite: si el puerto cambia, el archivo debe actualizarse
- En desarrollo, usar HTTP para evitar certificados autofirmados

---

## 9. Resumen de Patrones Anti-Fragilidad para Tests Blazor

| # | Patrón | Descripción |
|---|--------|-------------|
| 1 | `waitForTimeout(5000)` tras `goto` | Esperar carga Blazor WASM |
| 2 | JS `dispatchEvent` para MudTabs | Bypass overlay de mud-tabs-panels |
| 3 | Scoped locators a contenedor | Evitar NavMenu duplicado por Drawer |
| 4 | Verificar datos antes de testear UI | Contenido condicional (solo se renderiza si hay datos) |
| 5 | WEB_BASE centralizada en api-config.ts | Evitar puertos hardcodeados |
| 6 | api-config.ts como override por env var | `WEB_BASE_URL` y `API_BASE_URL` |
| 7 | Validar esquema DB vs C# | Columnas NULL en SQL pero non-nullable en C# |
| 8 | Monitorear console logs Blazor | Detectar errores de carga o Materialización |

---

## 10. Referencias

- `Docs/Architecture/LEGACY-TEST-STABILIZATION.md` — S7 section con todos los cambios
- `tests/api-config.ts` — URL centralizadas
- `tests/fase17-dashboard-enterprise.spec.ts` — clickTab helper + patrones S7
- `PassPlat.Web/wwwroot/appsettings.json` — ApiBaseUrl corregido
