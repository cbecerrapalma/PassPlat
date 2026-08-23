---
description: Wireframes UI y composición de componentes para todas las pantallas de PassPlat
---

# Screens — Wireframes UI

## Overview

Este documento describe la estructura visual de todas las pantallas de PassPlat, inspirado en el template **UBold** por CoderThemes. Sirve como referencia para construir páginas Blazor con MudBlazor.

> **Nota**: PassPlat está en construcción. Estos wireframes son una guía de referencia, no especificaciones definitivas. La lógica de negcio es una base aproximada que evolucionará.

## Screens

---

### 1. Login

```
┌─────────────────────────────────────────────────────────────────────┐
│                                                                     │
│                    ┌──────────────────────────────┐                 │
│                    │                              │                 │
│                    │          [Logo]              │                 │
│                    │      [icono candado]         │                 │
│                    │    Iniciar Sesión            │                 │
│                    │                              │                 │
│                    │  ┌────────────────────────┐  │                 │
│                    │  │  Correo electrónico    │  │                 │
│                    │  └────────────────────────┘  │                 │
│                    │  ┌────────────────────────┐  │                 │
│                    │  │  Contraseña            │  │                 │
│                    │  └────────────────────────┘  │                 │
│                    │                              │                 │
│                    │  [✔] Recordar sesión         │                 │
│                    │                              │                 │
│                    │  ┌────────────────────────┐  │                 │
│                    │  │    Iniciar Sesión      │  │                 │
│                    │  └────────────────────────┘  │                 │
│                    │                              │                 │
│                    │  ¿Olvidó su contraseña?      │                 │
│                    │                              │                 │
│                    └──────────────────────────────┘                 │
│                                                                     │
│                  © 2025 PassPlat. All rights reserved.              │
└─────────────────────────────────────────────────────────────────────┘

Componentes MudBlazor:
- MudContainer centrado
- MudPaper con Elevation="8" para el card de login
- MudTextField para email + password (Variant.Outlined)
- MudCheckBox para "Recordar sesión"
- MudButton (Variant.Filled, Color.Primary, FullWidth) para submit
- MudAlert (Severity.Error) para errores de autenticación
- MudProgressLinear (Indeterminate) durante submit
```

---

### 2. Dashboard

```
┌─────────────────────────────────────────────────────────────────────┐
│  [☰] PassPlat         [🔍 Buscar...]  [🔔5] [🇪🇸] [👤 Geneva ▼]  │
├──────────┬──────────────────────────────────────────────────────────┤
│  Main    │  Welcome back, Geneva                                   │
│  ─────── │  ┌──────────────────────────────────────────────────────┐│
│  📊 Dash │  │                                    [📥] [➕]        ││
│  ─────── │  └──────────────────────────────────────────────────────┘│
│  📁 Apps │                                                         │
│          │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐   │
│  Users   │  │  👥      │ │  ✅      │ │  ⚠️      │ │  📈      │   │
│  ├─ List  │  │  128     │ │  96      │ │  12      │ │  1,024   │   │
│  ├─ Roles │  │ Usuarios │ │ Activos  │ │ Bloquead │ │ Accesos  │   │
│  └─ Perm  │  └──────────┘ └──────────┘ └──────────┘ └──────────┘   │
│          │                                                         │
│  Auditor │  ┌────────────────────┐ ┌──────────────────────────┐    │
│  ─────── │  │  Sales Analytics   │ │  Top Tenants             │    │
│  📋 Logs │  │  [📈 chart area]   │ │  ├─ Tenant Alpha — 45%  │    │
│  🗄️ Pwd  │  │                    │ │  ├─ Tenant Beta  — 30%  │    │
│          │  │  View Reports →    │ │  └─ Tenant Gamma — 25%  │    │
│          │  └────────────────────┘ └──────────────────────────┘    │
│          │                                                         │
│          │  ┌─── Recent Activities ──────────────────────────────┐ │
│          │  │  [Avatar] User A — Login exitoso        — 5 min    │ │
│          │  │  [Avatar] User B — Cambio de password   — 12 min   │ │
│          │  │  [Avatar] User C — Bloqueo de cuenta    — 25 min   │ │
│          │  │  [Avatar] User D — Nuevo acceso MFA     — 1 hora   │ │
│          │  └────────────────────────────────────────────────────┘ │
│          │                                                         │
└──────────┴─────────────────────────────────────────────────────────┘

Componentes MudBlazor:
- MudGrid (4 stat cards row + 2-column chart section)
- MudAvatar (colored per metric type) + MudText
- MudPaper (Outlined, Elevation=0) for all cards
- MudTable for recent activities (ServerData, no pager needed)
- MudSkeleton for chart placeholder area
```

---

### 3. Entity List (Table Page)

```
┌─────────────────────────────────────────────────────────────────────┐
│  [☰] PassPlat                    [🔔] [🇪🇸] [👤]                   │
├──────────┬──────────────────────────────────────────────────────────┤
│  Main    │  PassPlat / Catálogos / Tenants                         │
│  ─────── │                                                         │
│  📊 Dash │  ┌─── Tenants ─────────────────────── [🔄] [+ Nuevo] ─┐│
│          │  └──────────────────────────────────────────────────────┘│
│  📁 Apps │                                                         │
│          │  ┌─── ─────────────────────────────────────────────────┐ │
│  Users   │  │  [🔍 Buscar...]         [Estado: Todos ▼]          │ │
│  ├─ List  │  └────────────────────────────────────────────────────┘ │
│  ├─ Roles │                                                         │
│  └─ Perm  │  ┌─────────────────────────────────────────────────────┐│
│          │  │  Id │ Código │ Nombre        │ Estado  │ Acciones   ││
│          │  │  ────┼───────┼────────────────┼─────────┼─────────── ││
│  Auditor │  │  1  │ ADM   │ Administrador  │ [Activo]│ [✏️] [🗑️]││
│  ─────── │  │  2  │ USR   │ Usuario        │ [Inact] │ [✏️] [🗑️]││
│  📋 Logs │  │  3  │ MGR   │ Manager        │ [Activo]│ [✏️] [🗑️]││
│  🗄️ Pwd  │  │  4  │ DEV   │ Developer      │ [Activo]│ [✏️] [🗑️]││
│          │  │  5  │ SUP   │ Support        │ [Inact] │ [✏️] [🗑️]││
│          │  │     │       │                │         │           ││
│          │  │  [Pager: < 1  2  3  ...  10  [10 per page] >]     ││
│          │  └─────────────────────────────────────────────────────┘│
│          │                                                         │
└──────────┴─────────────────────────────────────────────────────────┘

Componentes MudBlazor:
- MudTable con ServerData, MudTablePager (PageSizeOptions="10,25,50")
- ColGroup para anchos de columna (Id: 80px, Estado: 120px, Acciones: 100px)
- MudChip (Color.Success / Color.Default) para estado
- MudIconButton + MudTooltip para acciones primarias
- MudMenu (3-dot) para acciones adicionales (opcional si >2)
- MudTextField (Immediate, Clearable) + MudSelect para filtros
- MudPaper (Outlined) para el toolbar de filtros
- NoRecordsContent con SearchOff icon

MudTable columns:
  col 1: Id (80px)          → @context.Id
  col 2: Código (120px)     → @context.Codigo
  col 3: Nombre (flex)      → @context.Nombre + caption secondary
  col 4: Estado (120px)     → MudChip según Activo
  col 5: Acciones (140px)   → MudIconButton[Edit, Delete] + MudMenu[⋮]
```

---

### 4. Entity Detail (Tabs Page)

```
┌─────────────────────────────────────────────────────────────────────┐
│  [☰] PassPlat                    [🔔] [🇪🇸] [👤]                   │
├──────────┬──────────────────────────────────────────────────────────┤
│  Users   │  [←] PassPlat / Core / Usuarios / Juan Pérez           │
│  ─────── │                                                         │
│  ├─ List │  ┌──────────────────────────────────────────────────────┐│
│  ├─ Roles│  │  [JP Avatar]  Juan Pérez           [✏️ Editar]     ││
│  └─ Perm │  │  [Activo]  📅 Creado: 15 Ene 2025  🕐 Últ: 12 Mar││
│          │  └──────────────────────────────────────────────────────┘│
│  Auditor │                                                         │
│  ─────── │  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌───────────────┐│
│  📋 Logs │  │  📈     │ │  ✅     │ │  ⚠️     │ │  🔒           ││
│  🗄️ Pwd  │  │  48     │ │  12     │ │  3      │ │  2FA Activo   ││
│          │  │ Accesos │ │ Sesiones│ │ Bloqueo│ │  Seguridad    ││
│          │  └─────────┘ └─────────┘ └─────────┘ └───────────────┘│
│          │                                                         │
│          │  ┌──────────────────────────────────────────────────────┐│
│          │  │  [General] [Seguridad] [Actividad] [Sesiones]      ││
│          │  ├──────────────────────────────────────────────────────┤│
│          │  │                                                      ││
│          │  │  TAB: General                                        ││
│          │  │  ┌──────────────────────────────────────────────────┐││
│          │  │  │  Nombre: Juan Pérez                             │││
│          │  │  │  Email: juan@email.com                          │││
│          │  │  │  Rol: Administrador                             │││
│          │  │  │  Tenant: Tenant Alpha                           │││
│          │  │  │  Estado: Activo [✅]                            │││
│          │  │  │  Creado: 15/01/2025 10:30                      │││
│          │  │  │  Modificado: 12/03/2025 14:15                  │││
│          │  │  └──────────────────────────────────────────────────┘││
│          │  │                                                      ││
│          │  └──────────────────────────────────────────────────────┘│
│          │                                                         │
└──────────┴─────────────────────────────────────────────────────────┘

Componentes MudBlazor:
- MudPaper header card (avatar + info + status + acciones)
- MudGrid 4 stat cards (Accesos, Sesiones, Bloqueos, Seguridad)
- MudTabs (General, Seguridad, Actividad, Sesiones)
- MudSkeleton mientras carga
- Cada tab panel es un child component con lazy loading (IsActive)
```

---

### 5. Roles & Permissions Admin

```
┌─────────────────────────────────────────────────────────────────────┐
│  [☰] PassPlat                    [🔔] [🇪🇸] [👤]                   │
├──────────┬──────────────────────────────────────────────────────────┤
│  Users   │  PassPlat / Admin / Roles y Permisos                   │
│  ─────── │                                                         │
│  ├─ List │  ┌─── Roles y Permisos ────────── [🔄] [+ Nuevo] ────┐│
│  ├─ Roles│  └──────────────────────────────────────────────────────┘│
│  └─ Perm │                                                         │
│          │  ┌─── ─────────────────────────────────────────────────┐ │
│  Auditor │  │  [🔍 Buscar permiso...]   [Módulo: Todos ▼]       │ │
│  ─────── │  └────────────────────────────────────────────────────┘ │
│  📋 Logs │                                                         │
│  🗄️ Pwd  │  ┌─── Rol: Administrador ──────── 12 permisos ───────┐│
│          │  │                                                    ││
│          │  │  📁 Usuarios             [Todos] [Ninguno]         ││
│          │  │  ├─ Crear usuarios       ── [●═══════════○] ON    ││
│          │  │  ├─ Editar usuarios      ── [○═══════════●] OFF   ││
│          │  │  ├─ Eliminar usuarios    ── [●═══════════○] ON    ││
│          │  │  └─ Ver usuarios         ── [●═══════════○] ON    ││
│          │  │                                                    ││
│          │  │  📁 Seguridad             [Todos] [Ninguno]        ││
│          │  │  ├─ Gestionar roles      ── [●═══════════○] ON    ││
│          │  │  ├─ Configurar MFA       ── [○═══════════●] OFF   ││
│          │  │  ├─ Ver logs             ── [●═══════════○] ON    ││
│          │  │  └─ Bloquear usuarios    ── [●═══════════○] ON    ││
│          │  │                                                    ││
│          │  │  📁 Auditoría             [Todos] [Ninguno]        ││
│          │  │  ├─ Ver auditoría        ── [●═══════════○] ON    ││
│          │  │  └─ Exportar informes    ── [○═══════════●] OFF   ││
│          │  └────────────────────────────────────────────────────┘│
│          │                                                         │
│          │  ┌─── Rol: Usuario ──────────────── 4 permisos ───────┐│
│          │  │  ...                                               ││
│          │  └────────────────────────────────────────────────────┘│
│          │                                                         │
└──────────┴─────────────────────────────────────────────────────────┘

Componentes MudBlazor:
- MudSelect para selector de rol (MudAutocomplete si hay muchos roles)
- MudCard por cada módulo/agrupación de permisos
- MudSwitch (Color="Color.Primary") para toggle de cada permiso
- MudText con MudIcon (CheckCircle/Folder) para encabezados de módulo
- "Todos" / "Ninguno" links para toggle masivo por módulo
- MudOverlay + MudProgressCircular durante toggle
- MudTextField para búsqueda + MudSelect para filtro de módulo
- MudBadge o MudChip con count de permisos activos
```

---

### 6. Account Settings

```
┌─────────────────────────────────────────────────────────────────────┐
│  [☰] PassPlat                    [🔔] [🇪🇸] [👤]                   │
├──────────┬──────────────────────────────────────────────────────────┤
│  Main    │  PassPlat / Configuración / Account Settings           │
│  ─────── │                                                         │
│  📊 Dash │  ┌─── Account Settings ───────────────────────────────┐│
│          │  │  Manage your account settings and preferences.     ││
│  📁 Apps │  └────────────────────────────────────────────────────┘│
│          │                                                         │
│  Users   │  ┌────────────┐ ┌─────────────────────────────────────┐│
│  ├─ List │  │  [Avatar]  │ │  Personal Info                     ││
│  ├─ Roles│  │  Geneva K. │ │  ┌───────────────────────────────┐ ││
│  └─ Perm │  │  Art Dir   │ │  │ First Name  │ Last Name      │ ││
│          │  │  ───────── │ │  │ [Geneva   ] │ [K.          ] │ ││
│  Auditor │  │  👤 Pers   │ │  ├───────────────────────────────┤ ││
│  ─────── │  │  🔒 Secur  │ │  │ Email Address                │ ││
│  📋 Logs │  │  🔔 Notif  │ │  │ [geneva@example.com       ] │ ││
│  🗄️ Pwd  │  │            │ │  │ 📧 Click to change email    │ ││
│          │  └────────────┘ │  ├───────────────────────────────┤ ││
│          │                 │  │ Bio                           │ ││
│          │                 │  │ [Creative director...      ]  │ ││
│          │                 │  ├───────────────────────────────┤ ││
│          │                 │  │ Address Line 1                │ ││
│          │                 │  │ [123 Main St               ]  │ ││
│          │                 │  ├───────────────────────────────┤ ││
│          │                 │  │ City         │ Country        │ ││
│          │                 │  │ [New York  ] │ [USA         ] │ ││
│          │                 │  └───────────────────────────────┘ ││
│          │                 │                                     ││
│          │                 │  ┌─────────────────────────────────┐││
│          │                 │  │  [          Save Changes      ] │││
│          │                 │  └─────────────────────────────────┘││
│          │                 └─────────────────────────────────────┘│
│          │                                                         │
└──────────┴─────────────────────────────────────────────────────────┘

Componentes MudBlazor:
- MudGrid (3 col sidebar profile + 9 col form)
- MudPaper sidebar con MudAvatar + nombre + MudNavMenu
- MudPaper form con secciones separadas por MudDivider
- MudTextField (Variant.Outlined) para todos los campos
- MudButton (Variant.Filled, Color.Primary) para Save Changes
```

---

### 7. Error Pages (404 / 403 / 500)

```
┌─────────────────────────────────────────────────────────────────────┐
│                                                                     │
│                                                                     │
│                                                                     │
│                      ┌──────────────────────────────┐               │
│                      │                              │               │
│                      │          😕                  │               │
│                      │    ───                      │               │
│                      │      404                     │               │
│                      │    ───                      │               │
│                      │  Page Not Found              │               │
│                      │                              │               │
│                      │  The page you are looking    │               │
│                      │  for does not exist.         │               │
│                      │                              │               │
│                      │  ┌────────────────────────┐  │               │
│                      │  │   Back to Dashboard    │  │               │
│                      │  └────────────────────────┘  │               │
│                      │                              │               │
│                      └──────────────────────────────┘               │
│                                                                     │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

Color variations:
- 404: Color.Info (blue) — deactivate navigation sidebar
- 403: Color.Warning (amber) — "Access Denied"
- 500: Color.Error (red) — "Internal Server Error"

Componentes MudBlazor:
- MudContainer centrado sin sidebar
- MudPaper text-center con icono grande (ErrorOutline, Warning, Lock)
- MudText Typo.h1 para el código de error
- MudText Typo.h6 para el mensaje
- MudButton (Variant.Filled, Color.Primary) para volver
```

---

## Component Mapping by Screen

| Screen | Layout | Primary MudTable | Primary MudCard | Dialogs | States |
|--------|--------|-----------------|-----------------|---------|--------|
| Login | Centered, no sidebar | — | — | — | Loading (submit), Error (creds) |
| Dashboard | Sidebar + content | Recent Activities (no pager) | 4 stat cards + chart + top tenants | — | Loading (skeletons), Empty (first run) |
| Entity List | Sidebar + content | ✅ ServerData table | Filter toolbar | Create/Edit dialog, Delete confirm | Loading (MudTable.Loading), Empty (NoRecordsContent), Error (snackbar) |
| Entity Detail | Sidebar + content | Per-tab tables | Header card + 4 stat cards | Edit dialog | Loading (MudSkeleton), Not-found (MudPaper error) |
| Roles & Permissions | Sidebar + content | — | Per-module permission cards | Create permiso dialog, confirm delete | Loading (MudOverlay on toggle), Empty (no permissions) |
| Account Settings | Sidebar + content | — | Profile sidebar + settings form | — | Saving (button disabled), Success (snackbar) |
| Error Page | Full screen, no sidebar | — | — | — | Static, no loading needed |

## Routing Summary

| Route | Screen | Component | Parameters |
|-------|--------|-----------|------------|
| `/login` | Login | `Pages/Auth/Login.razor` | — |
| `/dashboard` | Dashboard | `Pages/Dashboard.razor` | — |
| `/tenants` | Entity List | `Pages/Tenants/Index.razor` | — |
| `/tenant/{id:int}` | Entity Detail | `Pages/Tenants/Detail.razor` | `Id` |
| `/apps` | Entity List | `Pages/Apps/Index.razor` | — |
| `/usuarios` | Entity List | `Pages/Usuarios/Index.razor` | — |
| `/usuario/{id:int}` | Entity Detail | `Pages/Usuarios/Detail.razor` | `Id` |
| `/roles` | Entity List | `Pages/Roles/Index.razor` | — |
| `/admin/permisos` | Roles & Permissions | `Pages/Permisos/Index.razor` | — |
| `/account/settings` | Account Settings | `Pages/Account/Settings.razor` | — |
| `/error/{code:int}` | Error Page | `Pages/Error/ErrorPage.razor` | `code` |

## Responsive Breakpoints

| Breakpoint | MudBlazor | Sidebar | Table behavior |
|------------|-----------|---------|---------------|
| ≥ 1280px | `lg` | Visible (default, ~260px) | Normal |
| 960-1279px | `md` | Visible (compact, ~60px icons) | Normal |
| 600-959px | `sm` | Offcanvas (toggle via hamburger) | Scroll horizontal |
| < 600px | `xs` | Offcanvas | Stacked cards (DataLabel) |
