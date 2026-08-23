---
description: Reglas frontend Blazor/MudBlazor para PassPlat. Leer antes de escribir cualquier componente UI.
globs: ["**/*.razor", "**/*.razor.cs", "**/Pages/**", "**/Components/**", "**/Shared/**"]
---

# Frontend — Reglas PassPlat (MudBlazor + Ubold)

## Stack

- **UI**: Blazor WebAssembly + MudBlazor
- **Estilo**: Ubold Admin Template (CoderThemes) como referencia visual
- **Estado**: PassPlat.Web en construcción — diseño profesional es prioridad actual

## Componentes: usar / no usar

| Usar | No usar | Para qué |
|------|---------|----------|
| `MudTable` + `ServerData` + `MudTablePager` | `MudDataGrid` | Listas paginadas |
| `MudDialog` + `IMudDialogInstance` | — | CRUD create/edit |
| `MudIconButton` + `MudTooltip` | `MudFab` | Acciones en filas |
| `MudMenu` + `MudMenuItem` | — | 3+ acciones, dropdowns |
| `MudPaper Elevation="0" Outlined="true"` | `MudCard` para toolbars | Stat cards, toolbars, forms |
| `MudCard` / `MudCardHeader` | — | Entity cards, dashboards |
| `MudOverlay` + `MudProgressCircular` | — | Loading durante operaciones |
| `MudSkeleton` | — | Loading placeholder detail pages |
| `MudChip` | `MudBadge` para estados | Status badges |
| `MudTabs` + `MudTabPanel` | — | Detail pages con secciones |
| `MudSwitch` | `MudCheckBox` para toggles | Permisos, booleans |

## Colores — NUNCA hardcodear hex

```
Color.Primary   → totales, registros, acciones principales
Color.Success   → activo, exitoso, completado
Color.Warning   → bloqueado, pendiente, advertencia
Color.Error     → error, eliminado, crítico
Color.Info      → hoy, trending, temporal
Color.Secondary → inactivo, documentos, secundario
```

## Tipografía

```
Typo.h4         → título de página
Typo.h5         → valor stat card
Typo.h6         → card title, dialog title
Typo.body1      → contenido principal
Typo.body2      → secundario, breadcrumb
Typo.caption    → labels, fechas (siempre con Color.Secondary)
```

## Spacing estándar

```
pa-4   → MudPaper headers, stat cards, toolbars
pa-6   → forms, dialogs
mb-4   → separación entre secciones
gap-2/gap-3 → flex containers
```

## Estructura de página estándar

```
1. Breadcrumb (MudText Typo.body2 + ícono casa)
2. Page header (MudPaper Outlined: título + [Refresh] + [+ Nuevo])
3. Stat cards row (MudGrid xs=6 md=3, MudPaper Outlined)
4. Filter toolbar (MudPaper Outlined: MudTextField + MudSelect)
5. MudTable (ServerData, MudTablePager PageSizeOptions="10,25,50")
```

## Estados obligatorios en toda página/componente

```razor
@if (_cargando)     { <MudSkeleton> }      // Detail pages
@if (!_cargando && _tabla.Loading) { }     // Tables: usar Loading param de MudTable
@else if (_entidad is null) { <MudPaper error + botón volver> }
@else { contenido }
```

Snackbar para feedback: `Severity.Success` / `Severity.Error` en toda operación CRUD.

**Regla de errores**: Todo `Snackbar.Add("Error al...", Severity.Error)` debe mostrar el error real del servidor usando `Api.LastError`:
```razor
// CORRECTO — muestra el error real de la API
Snackbar.Add(Api.LastError ?? "Error al crear", Severity.Error);

// INCORRECTO — mensaje genérico sin detalle
Snackbar.Add("Error al crear", Severity.Error);
```

Los bloques `catch (Exception ex)` también deben incluir `Api.LastError`:
```razor
catch (Exception ex)
{
    Snackbar.Add(Api.LastError ?? $"Error: {ex.Message}", Severity.Error);
}
```

## Inyecciones estándar

```razor
@inject ApiClient Api
@inject ISnackbar Snackbar
@inject IDialogService Dialog
@inject NavigationManager Navigation
```

## Convenciones español (sin excepciones)

| Elemento | Texto |
|---------|-------|
| Botón crear | "Nuevo X" / "Nueva X" |
| Búsqueda | `Label="Buscar..."` |
| Filtro estado | "Todos" / "Activo" / "Inactivo" |
| Confirmar eliminar | Title: "Confirmar", Btn: "Sí, eliminar" |
| Guardar | "Guardar cambios" |
| Vacío | "No hay registros" |
| Éxito | "X creado exitosamente" |
| Error genérico | "Error al cargar los datos" |

## Columnas MudTable estándar

```
Id (80px) | Código (120px) | Nombre (flex, + caption Typo.caption secondary) | Estado (120px, MudChip) | Acciones (140px)
```

## Estado → Color chip

```
Activo      → Color.Success
Inactivo    → Color.Default
Bloqueado   → Color.Warning
Suspendido  → Color.Warning
Error       → Color.Error
Pendiente   → Color.Info
```

## Prohibiciones absolutas

- NO `MudDataGrid`
- NO `MudFab`
- NO colores hex hardcodeados
- NO inglés en labels/tooltips/mensajes UI
- NO `OnInitialized` síncrono (usar `OnInitializedAsync`)
- NO `SaveChangesAsync` desde servicios (solo desde WebAPI consumer)
- NO `Result<T>.Success(null)` sin `allowNull: true`
- NO lazy loading ni includes innecesarios en consultas API

## Referencia completa

Para snippets completos de componentes (stat card, dialog, table, role card, etc.):
→ Leer `docs/ui/frontend-dev.full.md` (versión extendida, bajo demanda)
→ Ver pantallas existentes en `PassPlat.Web/Pages/` como referencia viva
