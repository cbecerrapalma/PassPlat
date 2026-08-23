---
description: Routing y composición de pantallas PassPlat. Referencia rápida de qué componente va en qué ruta.
globs: ["**/*.razor", "**/Pages/**"]
---

# Screens — Mapa de rutas y componentes

## Routing

| Ruta | Componente | Parámetros |
|------|-----------|------------|
| `/login` | `Pages/Auth/Login.razor` | — |
| `/dashboard` | `Pages/Dashboard.razor` | — |
| `/tenants` | `Pages/Tenants/Index.razor` | — |
| `/tenant/{id:int}` | `Pages/Tenants/Detail.razor` | `Id` |
| `/apps` | `Pages/Apps/Index.razor` | — |
| `/usuarios` | `Pages/Usuarios/Index.razor` | — |
| `/usuario/{id:int}` | `Pages/Usuarios/Detail.razor` | `Id` |
| `/roles` | `Pages/Roles/Index.razor` | — |
| `/admin/permisos` | `Pages/Permisos/Index.razor` | — |
| `/account/settings` | `Pages/Account/Settings.razor` | — |
| `/error/{code:int}` | `Pages/Error/ErrorPage.razor` | `code` |

## Composición por pantalla

| Pantalla | Layout | Tabla | Cards | Dialogs | Estados |
|----------|--------|-------|-------|---------|---------|
| Login | Centrado, sin sidebar | — | MudPaper Elevation=8 | — | Loading (submit), Error creds |
| Dashboard | Sidebar + content | Recent Activities (sin pager) | 4 stat + chart + top tenants | — | Skeletons |
| Entity List | Sidebar + content | MudTable ServerData + pager | Filter toolbar | Create/Edit + Delete confirm | Loading (MudTable.Loading), Empty (NoRecordsContent), Error (snackbar) |
| Entity Detail | Sidebar + content | Por tab | Header + 4 stat | Edit dialog | MudSkeleton, Not-found MudPaper |
| Roles & Permisos | Sidebar + content | — | Por módulo con MudSwitch | Confirm delete | MudOverlay on toggle |
| Account Settings | Sidebar + content | — | Profile sidebar + form | — | Saving (btn disabled), Success snackbar |
| Error Page | Full screen, sin sidebar | — | — | — | Estático |

## Responsive

| Breakpoint | Sidebar | Tabla |
|-----------|---------|-------|
| ≥1280px (lg) | Visible ~260px | Normal |
| 960-1279px (md) | Compact ~60px | Normal |
| 600-959px (sm) | Offcanvas | Scroll horizontal |
| <600px (xs) | Offcanvas | Stacked cards (DataLabel) |

## Pantallas pendientes de implementar (Ubold pattern)

| Pantalla | Estado |
|---------|--------|
| Profile dropdown | 🟡 Pendiente |
| Notification dropdown | 🟡 Pendiente |
| Theme customizer panel | 🟡 Pendiente |
| Account settings | 🟡 Pendiente |
| Dashboard charts | 🟡 Pendiente |
| Error pages | 🟡 Pendiente |

> Para wireframes ASCII detallados: ver `docs/ui/screens.full.md` (bajo demanda)
