---
description: Build Blazor components, pages, and layouts following the MudBlazor design system + Ubold patterns
---

# Frontend Development Skill — PassPlat

## Overview

Guía el desarrollo frontend de PassPlat asegurando que todos los componentes Blazor, páginas y layouts sigan el sistema de diseño MudBlazor. Inspirado en **UBold Admin Template** por CoderThemes.

> **Nota**: PassPlat está en construcción. La lógica de negocio es una base aproximada, no definitiva. El foco actual es lograr un diseño profesional y moderno.

## When to Use

- Crear nuevos componentes Blazor
- Construir páginas o layouts
- Implementar funcionalidades UI
- Trabajar con componentes MudBlazor
- Añadir estados de carga, vacíos o manejo de errores

## Core References

**CRITICAL:** Antes de escribir código frontend, leer:

| Documento | Propósito |
|-----------|-----------|
| `docs/ui/frontend-dev.md` | Esta guía: colores, componentes, patrones Ubold |
| `docs/ui/screens.md` | Wireframes UI y composición de componentes |
| `AGENTS.md` | Arquitectura del proyecto, reglas Result/Service/DAL |

## Layout System (Ubold-style)

```
┌──────────────────────────────────────────────────────────────────┐
│  MudAppBar (topbar)                                              │
│  [☰] [Logo] [Buscar...]  [🔔5] [🇪🇸] [👤 Geneva ▼]             │
├──────────┬───────────────────────────────────────────────────────┤
│  MudDraw │  MudMainContent                                       │
│  (sidebar│                                                       │
│   nav)   │  Breadcrumb: PassPlat / Catálogos / Tenants           │
│          │                                                       │
│          │  ┌─ Page Header ────────────────────────────────────┐ │
│          │  │  Título                    [🔄] [+ Nuevo]       │ │
│          │  └──────────────────────────────────────────────────┘ │
│          │                                                       │
│          │  ┌─ Stat Cards Row ─────────────────────────────────┐ │
│          │  │  [Avatar] [Avatar] [Avatar] [Avatar]            │ │
│          │  └──────────────────────────────────────────────────┘ │
│          │                                                       │
│          │  ┌─ Toolbar ─────────────────────────────────────────┐ │
│          │  │  [Buscar...]  [Filtro ▼]                         │ │
│          │  └──────────────────────────────────────────────────┘ │
│          │                                                       │
│          │  ┌─ MudTable ────────────────────────────────────────┐ │
│          │  │  Id │ Nombre    │ Estado      │ Acciones         │ │
│          │  │  ───────────────────────────────────────────────  │ │
│          │  │  1  │ Admin     │ [Activo]    │ [✏️] [🗑️] [⋮]  │ │
│          │  │  [Pager: < 1 2 3 ... 10 >  [10 per page]        │ │
│          │  └──────────────────────────────────────────────────┘ │
│          │                                                       │
│          │  ┌─ Admin Customizer ───────────────────────────────┐ │
│          │  │  [⚙️] Panel flotante derecha (tema, sidebar)    │ │
│          │  └──────────────────────────────────────────────────┘ │
└──────────┴───────────────────────────────────────────────────────┘
```

## Workflow

### Step 1: Understand the Context

1. Read `docs/ui/screens.md` for the specific screen wireframe
2. Check existing components in `PassPlat.Web/Pages/` for similar patterns

### Step 2: Plan the Component

- [ ] Component location (Pages/, Shared/, Components/)
- [ ] Parameter design
- [ ] Which MudBlazor components to use
- [ ] Loading, empty, and error states needed
- [ ] Breadcrumb and page header appropriate
- [ ] Spanish labels

### Step 3: Implementation Checklist

#### DI / Injections

```razor
@inject ApiClient Api
@inject ISnackbar Snackbar
@inject IDialogService Dialog
@inject NavigationManager Navigation
```

#### Colors (MudBlazor)

- Usar colores semánticos: `Color.Primary`, `Color.Success`, `Color.Warning`, `Color.Error`, `Color.Info`, `Color.Default`, `Color.Secondary`
- NEVER hardcode hex colors
- Stat card avatar colors:

| Métrica | Avatar Color |
|---------|-------------|
| Usuarios totales, registros | `Color.Primary` |
| Activos, exitosos, completados | `Color.Success` |
| Bloqueados, advertencias, pendientes | `Color.Warning` |
| Errores, críticos, cancelados | `Color.Error` |
| Hoy, trending, stats temporales | `Color.Info` |
| Archivos, documentos, inactivos | `Color.Secondary` |

#### Typography

- Headers: `Typo.h4` (page title), `Typo.h5` (stat value), `Typo.h6` (card title, dialog)
- Body: `Typo.body1` (primary), `Typo.body2` (secondary)
- Captions: `Typo.caption` con `Color="Color.Secondary"`
- Breadcrumb: `Typo.body2` con separadores `/`

#### Spacing

- Page wrapper: `MudContainer`
- Page padding: `pa-4` en MudPaper headers, `pa-6` en forms
- Card padding: `pa-4` (stat cards, toolbars), `pa-6` (forms)
- Component spacing: `Class="mb-4"`, `gap-3`, `gap-2`
- Stat cards: `d-flex align-center gap-3`

#### MudBlazor Components

| Componente | Cuándo usar | NO USAR |
|-----------|-------------|---------|
| `MudTable` + `ServerData` + `MudTablePager` | Listas con paginación servidor | `MudDataGrid` |
| `MudCard` / `MudCardHeader` / `MudCardContent` | Dashboards, entity cards, roles | — |
| `MudPaper` | Stat cards, action bars, toolbars, forms | — |
| `MudPaper` | `Elevation="0" Outlined="true"` para tarjetas | — |
| `MudIconButton` + `MudTooltip` | Acciones en filas de tabla | `MudFab` |
| `MudMenu` + `MudMenuItem` | 3+ acciones, profile dropdown, notificaciones | — |
| `MudDialog` + `IMudDialogInstance` | Todos los CRUD create/edit | — |
| `MudTabs` + `MudTabPanel` | Páginas detalle con pestañas | — |
| `MudOverlay` + `MudProgressCircular` | Loading durante operaciones (toggle, guardado) | — |
| `MudSwitch` | Toggles booleanos, asignación de permisos | — |
| `MudChip` | Badges de estado, contadores | — |
| `MudAvatar` + `MudAvatarGroup` | Perfiles, grupos en tarjetas de rol | — |
| `MudSkeleton` | Loading placeholder en detail pages | — |
| `MudAlert` | Estados de error (entidad no encontrada) | — |
| `MudBreadcrumbs` / `MudText` | Navegación breadcrumb | — |
| `MudBadge` | Notificaciones no leídas en topbar | — |
| `MudDrawer` | Panel customizer, offcanvas sidebar | — |
| `MudNavMenu` + `MudNavLink` | Sidebar navigation, sub-menú settings | — |
| `MudSelect` / `MudAutocomplete` | Selectores con búsqueda (tenant, app, rol) | — |
| `MudTextField` | Búsquedas y campos de formulario | — |

#### Page Header (breadcrumb + title + actions)

```razor
<MudText Typo="Typo.body2" Class="mb-1 d-flex align-center gap-1 text-secondary">
    <MudIcon Icon="@Icons.Material.Filled.House" Size="Size.Small" />
    PassPlat / @_seccion / @_tituloPagina
</MudText>

<MudPaper Class="pa-4 mb-4 d-flex flex-wrap align-center" Elevation="0" Outlined="true">
    <MudText Typo="Typo.h4" Class="flex-grow-1 mb-0">@_tituloPagina</MudText>
    <div class="d-flex gap-2">
        <MudIconButton Icon="@Icons.Material.Filled.Refresh" Size="Size.Medium"
            OnClick="@Refrescar" Disabled="@_cargando" />
        <MudButton Variant="Variant.Filled" Color="Color.Primary"
            StartIcon="@Icons.Material.Filled.Add" OnClick="@AbrirCrear">
            Nuevo @_entidad
        </MudButton>
    </div>
</MudPaper>
```

#### Stat Card

```razor
<MudPaper Class="pa-4 d-flex align-center gap-3" Elevation="0" Outlined="true">
    <MudAvatar Color="Color.Primary" Variant="Variant.Circular">
        <MudIcon Icon="@Icons.Material.Filled.People" />
    </MudAvatar>
    <div>
        <MudText Typo="Typo.h5" Class="font-weight-bold mb-0">@Value</MudText>
        <MudText Typo="Typo.caption" Color="Color.Secondary">@Label</MudText>
    </div>
</MudPaper>

@code {
    [Parameter] public string Value { get; set; } = "";
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public Color AvatarColor { get; set; } = Color.Primary;
    [Parameter] public string Icon { get; set; } = Icons.Material.Filled.People;
}
```

#### Status Chip

```razor
<MudChip Color="@(activo ? Color.Success : Color.Default)"
         Size="Size.Small">
    @(activo ? "Activo" : "Inactivo")
</MudChip>
```

| Estado | Color |
|--------|-------|
| Activo | `Color.Success` |
| Inactivo | `Color.Default` |
| Suspendido / Bloqueado | `Color.Warning` |
| Error / Eliminado | `Color.Error` |
| Pendiente | `Color.Info` |

#### States

- **Loading list**: `MudTable Loading="@_loading"` (manejo interno)
- **Loading detail**: `MudSkeleton Width="100%" Height="200px"`
- **Empty list**: `<NoRecordsContent>` con `SearchOff` icon + "No hay registros"
- **Error API**: `catch (Exception ex) { Snackbar.Add($"Error: {ex.Message}", Severity.Error); }`
- **Error not-found**: `MudPaper` centrado con `ErrorOutline` icon + "X no encontrado" + botón volver
- **Loading overlay**: `<MudOverlay Visible="@_guardando"><MudProgressCircular Indeterminate="true" /></MudOverlay>`

#### Icons

```razor
@Icons.Material.Filled.Name
```

| Icono | Propósito |
|-------|-----------|
| `Add` | Nuevo/Crear |
| `Edit` | Editar |
| `Delete` | Eliminar |
| `Visibility` | Ver detalle |
| `MoreVert` | Menú 3 puntos |
| `Search` / `SearchOff` | Buscar / vacío |
| `Refresh` | Refrescar |
| `House` | Home breadcrumb |
| `People` | Usuarios |
| `Shield` | Seguridad |
| `Badge` | Rol |
| `Folder` | Módulo |
| `CheckCircle` | Éxito / permiso |
| `Warning` | Advertencia |
| `Settings` | Configurar |
| `ArrowBack` | Volver |
| `Notifications` / `NotificationsNone` | Notificaciones |
| `Person` | Perfil |
| `Logout` | Cerrar sesión |
| `Dashboard` | Dashboard |
| `ErrorOutline` | Error |
| `Inbox` | Vacío alternativo |
| `CalendarToday` / `Schedule` | Fechas |
| `TrendingUp` | Métricas |
| `Download` | Exportar |

#### Create/Edit Dialog

```razor
<MudDialog Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">@(EsEdicion ? "Editar" : "Nuevo") X</MudText>
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_nombre" Label="Nombre" Variant="Variant.Outlined" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => MudDialog.Cancel())">Cancelar</MudButton>
        <MudButton Variant="Variant.Filled" Color="Color.Primary"
            OnClick="@Guardar" Disabled="@_guardando">
            @(_guardando ? "Guardando..." : (EsEdicion ? "Guardar cambios" : "Crear"))
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public bool EsEdicion { get; set; }
    [Parameter] public XDto? X { get; set; }

    private DialogOptions _dialogOptions = new() { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true };
    private bool _guardando;

    protected override void OnInitialized()
    {
        if (EsEdicion && X is not null) { /* poblar campos */ }
    }

    private async Task Guardar()
    {
        _guardando = true;
        try
        {
            if (EsEdicion)
            {
                var ok = await Api.PutAsync<XDto>($"api/x/{X.Id}", dto);
                if (ok is not null) { Snackbar.Add("X actualizado", Severity.Success); MudDialog.Close(DialogResult.Ok(true)); }
                else Snackbar.Add("Error al actualizar", Severity.Error);
            }
            else
            {
                var result = await Api.PostAsync<XDto>("api/x", dto);
                if (result is not null) { Snackbar.Add("X creado", Severity.Success); MudDialog.Close(DialogResult.Ok(true)); }
                else Snackbar.Add("Error al crear", Severity.Error);
            }
        }
        catch (Exception ex) { Snackbar.Add($"Error: {ex.Message}", Severity.Error); }
        finally { _guardando = false; }
    }
}
```

#### Delete Confirmation

```csharp
private async Task ConfirmarEliminar(XDto item)
{
    var parameters = new DialogParameters
    {
        ["Title"] = "Confirmar",
        ["Message"] = $"¿Desactivar {item.Nombre}?",
        ["ConfirmText"] = "Sí, desactivar",
        ["CancelText"] = "Cancelar",
        ["Color"] = Color.Error
    };
    var dialog = await Dialog.ShowAsync<ConfirmDialog>("", parameters);
    var result = await dialog.Result;
    if (!result.Canceled)
    {
        var ok = await Api.DeleteAsync($"api/x/{item.Id}");
        if (ok)
        {
            Snackbar.Add("X desactivado", Severity.Success);
            if (_table is not null) await _table.ReloadServerData();
        }
        else Snackbar.Add("Error al desactivar", Severity.Error);
    }
}
```

#### Server-Reload Handler

```csharp
private MudTable<TDto>? _table;
private bool _loading;
private string _searchString = "";
private string _filtroEstado = "";

private async Task<TableData<TDto>> ServidorDatos(TableState state, CancellationToken ct)
{
    try
    {
        var query = $"api/x/page?page={state.Page + 1}&pageSize={state.PageSize}&search={_searchString}";
        if (!string.IsNullOrEmpty(_filtroEstado))
            query += $"&estado={_filtroEstado}";
        var response = await Api.GetAsync<PagedResponse<TDto>>(query);
        if (response is not null)
            return new TableData<TDto> { Items = response.Items, TotalItems = response.TotalCount };
    }
    catch (Exception ex)
    {
        Snackbar.Add($"Error al cargar: {ex.Message}", Severity.Error);
    }
    return new TableData<TDto> { Items = [], TotalItems = 0 };
}

private async Task Refrescar()
{
    if (_table is not null) await _table.ReloadServerData();
    Snackbar.Add("Datos actualizados", Severity.Info);
}
```

### Step 4: File Structure

```
PassPlat.Web/
├── Pages/
│   ├── Auth/
│   │   └── Login.razor
│   ├── Tenants/
│   │   ├── Index.razor        # List page (MudTable)
│   │   └── Detail.razor       # Detail page (MudTabs)
│   ├── Usuarios/
│   │   ├── Index.razor
│   │   └── Detail.razor
│   ├── Roles/
│   │   └── Index.razor
│   ├── Permisos/
│   │   ├── Index.razor        # Admin permissions page
│   │   └── PermisoDialog.razor  # Create permission dialog
│   ├── Account/
│   │   └── Settings.razor
│   └── Error/
│       └── ErrorPage.razor
├── Shared/
│   ├── MainLayout.razor
│   ├── NavMenu.razor
│   └── ConfirmDialog.razor
└── Components/
    └── (future reusable components)
```

### Step 5: Verification

- [ ] Todos los labels, tooltips y mensajes en español
- [ ] Breadcrumb: "PassPlat / Sección / Página"
- [ ] `MudTable` con `ServerData`, `MudTablePager`, `<NoRecordsContent>`
- [ ] `MudTooltip` en cada `MudIconButton` de acciones
- [ ] 3-dot `MudMenu` para 3+ acciones en una fila
- [ ] Status chip con color semántico
- [ ] `@inject ApiClient`, `ISnackbar`, `IDialogService`, `NavigationManager`
- [ ] `_guardando` bool en operaciones de guardado (evita doble submit)
- [ ] `try/catch` con `Snackbar.Add(Severity.Error)` en toda llamada API
- [ ] Estados: loading (MudSkeleton/MudTable.Loading), empty (NoRecordsContent), error (Snackbar/MudAlert)
- [ ] `ColGroup` con `style="width: Xpx"` en MudTable
- [ ] `DataLabel="@("columna")"` en cada MudTd (responsive)
- [ ] `DialogOptions` con `MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true`
- [ ] Refresh tras CRUD: `await _table.ReloadServerData()`
- [ ] `MudAvatar` + `MudAvatarGroup` para representación visual de usuarios
- [ ] Stat cards con avatar coloreado + icon + métrica + label
- [ ] Detail pages: header card (avatar + info + status + acciones) + MudTabs
- [ ] Colores semánticos (nunca hex hardcodeado)

## Quick Reference: Common Patterns

### Page Structure (List)

```razor
@page "/ruta"

<MudContainer>
    @* Breadcrumb *@
    <MudText Typo="Typo.body2" Class="mb-1 d-flex align-center gap-1 text-secondary">
        <MudIcon Icon="@Icons.Material.Filled.House" Size="Size.Small" />
        PassPlat / @_seccion / @_tituloPagina
    </MudText>

    @* Page header *@
    <MudPaper Class="pa-4 mb-4 d-flex flex-wrap align-center" Elevation="0" Outlined="true">
        <MudText Typo="Typo.h4" Class="flex-grow-1 mb-0">@_tituloPagina</MudText>
        <div class="d-flex gap-2">
            <MudIconButton Icon="@Icons.Material.Filled.Refresh" Size="Size.Medium"
                OnClick="@Refrescar" Disabled="@_cargando" />
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                StartIcon="@Icons.Material.Filled.Add" OnClick="@AbrirCrear">
                Nuevo @_entidad
            </MudButton>
        </div>
    </MudPaper>

    @* Filter toolbar *@
    <MudPaper Class="pa-4 mb-4" Elevation="0" Outlined="true">
        <MudGrid>
            <MudItem xs="12" sm="6" md="4">
                <MudTextField @bind-Value="_searchString" Label="Buscar..."
                    Variant="Variant.Outlined" Immediate="true" Clearable="true" />
            </MudItem>
            <MudItem xs="6" sm="3" md="2">
                <MudSelect T="string" Label="Estado" Variant="Variant.Outlined"
                    Value="_filtroEstado" ValueChanged="@OnFiltroCambiado">
                    <MudSelectItem Value="">Todos</MudSelectItem>
                    <MudSelectItem Value="Activo">Activo</MudSelectItem>
                    <MudSelectItem Value="Inactivo">Inactivo</MudSelectItem>
                </MudSelect>
            </MudItem>
        </MudGrid>
    </MudPaper>

    @* Table *@
    <MudTable @ref="_table" T="XDto" ServerData="@ServidorDatos"
        Dense="true" Hover="true" Loading="@_loading">
        <ColGroup>
            <col style="width: 80px;" />
            <col />
            <col style="width: 100px;" />
            <col style="width: 140px;" />
        </ColGroup>
        <HeaderContent>
            <MudTh>Id</MudTh>
            <MudTh>Nombre</MudTh>
            <MudTh>Estado</MudTh>
            <MudTh>Acciones</MudTh>
        </HeaderContent>
        <RowTemplate>
            <MudTd DataLabel="Id">@context.Id</MudTd>
            <MudTd DataLabel="Nombre">
                <MudText Typo="Typo.body2">@context.Nombre</MudText>
                <MudText Typo="Typo.caption" Color="Color.Secondary">@context.Codigo</MudText>
            </MudTd>
            <MudTd DataLabel="Estado">
                <MudChip Color="@(context.Activo ? Color.Success : Color.Default)" Size="Size.Small">
                    @(context.Activo ? "Activo" : "Inactivo")
                </MudChip>
            </MudTd>
            <MudTd DataLabel="Acciones">
                <MudTooltip Text="Editar">
                    <MudIconButton Icon="@Icons.Material.Filled.Edit"
                        Size="Size.Small" OnClick="@(() => AbrirEditar(context))" />
                </MudTooltip>
                <MudTooltip Text="Eliminar">
                    <MudIconButton Icon="@Icons.Material.Filled.Delete"
                        Size="Size.Small" Color="Color.Error"
                        OnClick="@(() => ConfirmarEliminar(context))" />
                </MudTooltip>
            </MudTd>
        </RowTemplate>
        <PagerContent>
            <MudTablePager PageSizeOptions="new int[]{10, 25, 50}" />
        </PagerContent>
        <NoRecordsContent>
            <MudText Align="Align.Center" Class="py-6" Color="Color.Secondary">
                <MudIcon Icon="@Icons.Material.Filled.SearchOff" Class="mr-2" />
                No hay registros
            </MudText>
        </NoRecordsContent>
    </MudTable>
</MudContainer>
```

### Detail Page (Header + Tabs + Stat Cards)

```razor
@* Header card *@
<MudPaper Class="pa-4 mb-4 d-flex flex-wrap align-center gap-4" Elevation="0" Outlined="true">
    <MudAvatar Color="Color.Primary" Size="Size.Large" Variant="Variant.Circular">
        @_entity.Iniciales
    </MudAvatar>
    <div class="flex-grow-1">
        <MudText Typo="Typo.h4" Class="mb-1">@_entity.Nombre</MudText>
        <div class="d-flex flex-wrap gap-2 align-center">
            <MudChip Color="Color.Success" Size="Size.Small">Activo</MudChip>
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                <MudIcon Icon="@Icons.Material.Filled.CalendarToday" Size="Size.Small" Class="mr-1" />
                Creado: @_entity.FecCrea.ToString("dd MMM yyyy")
            </MudText>
        </div>
    </div>
    <MudButton Variant="Variant.Filled" Color="Color.Primary"
        StartIcon="@Icons.Material.Filled.Edit" OnClick="@AbrirEditar">
        Editar
    </MudButton>
</MudPaper>

@* Stat cards *@
<MudGrid Class="mb-4">
    <MudItem xs="6" md="3">
        <MudPaper Class="pa-4 d-flex align-center gap-3" Elevation="0" Outlined="true">
            <MudAvatar Color="Color.Primary" Variant="Variant.Circular">
                <MudIcon Icon="@Icons.Material.Filled.TrendingUp" />
            </MudAvatar>
            <div>
                <MudText Typo="Typo.h5" Class="font-weight-bold mb-0">@_stats.Item1</MudText>
                <MudText Typo="Typo.caption" Color="Color.Secondary">@_stats.Item2</MudText>
            </div>
        </MudPaper>
    </MudItem>
</MudGrid>

@* Tabs *@
<MudPaper Elevation="0" Outlined="true">
    <MudTabs>
        <MudTabPanel Text="General"> @* content *@ </MudTabPanel>
        <MudTabPanel Text="Seguridad"> @* content *@ </MudTabPanel>
        <MudTabPanel Text="Actividad"> @* content *@ </MudTabPanel>
    </MudTabs>
</MudPaper>
```

### 3-Dot Action Menu

```razor
<MudMenu>
    <ActivatorContent>
        <MudIconButton Icon="@Icons.Material.Filled.MoreVert" Size="Size.Small" />
    </ActivatorContent>
    <ChildContent>
        <MudMenuItem OnClick="@(() => VerDetalle(context))">
            <MudIcon Icon="@Icons.Material.Filled.Visibility" Size="Size.Small" Class="mr-2" />Ver detalle
        </MudMenuItem>
        <MudMenuItem OnClick="@(() => AbrirEditar(context))">
            <MudIcon Icon="@Icons.Material.Filled.Edit" Size="Size.Small" Class="mr-2" />Editar
        </MudMenuItem>
        <MudDivider />
        <MudMenuItem OnClick="@(() => ConfirmarEliminar(context))" Class="text-error">
            <MudIcon Icon="@Icons.Material.Filled.Delete" Size="Size.Small" Class="mr-2" />Eliminar
        </MudMenuItem>
    </ChildContent>
</MudMenu>
```

### Role Card with Permissions + Avatar Group

```razor
<MudCard Elevation="0" Outlined="true">
    <MudCardHeader>
        <CardHeaderContent>
            <MudText Typo="Typo.h6">@rol.Nombre</MudText>
            <MudText Typo="Typo.caption" Color="Color.Secondary">@rol.TenantNombre</MudText>
        </CardHeaderContent>
        <CardHeaderActions>
            @* 3-dot menu *@
        </CardHeaderActions>
    </MudCardHeader>
    <MudCardContent>
        @foreach (var permiso in rol.Permisos.Take(4))
        {
            <MudText Typo="Typo.body2" Class="d-flex align-center">
                <MudIcon Icon="@Icons.Material.Filled.CheckCircle"
                    Size="Size.Small" Color="Color.Success" Class="mr-1" />
                @permiso.Nombre
            </MudText>
        }
        <MudDivider Class="my-2" />
        <div class="d-flex align-center justify-space-between">
            <div class="d-flex align-center gap-2">
                <MudAvatarGroup Max="3" Size="Size.Small">
                    @foreach (var user in rol.Usuarios.Take(4))
                    {
                        <MudAvatar Size="Size.Small" Color="Color.Primary">@user.Iniciales</MudAvatar>
                    }
                </MudAvatarGroup>
                <MudText Typo="Typo.caption" Color="Color.Secondary">@rol.Usuarios.Count usuarios</MudText>
            </div>
            <MudText Typo="Typo.caption" Color="Color.Secondary">Actualizado @rol.FecMod?.ToString("g")</MudText>
        </div>
    </MudCardContent>
</MudCard>
```

### Dashboard Page

```razor
<MudPaper Class="pa-4 mb-4 d-flex flex-wrap align-center" Elevation="0" Outlined="true">
    <div>
        <MudText Typo="Typo.caption" Color="Color.Secondary">Welcome back,</MudText>
        <MudText Typo="Typo.h4" Class="font-weight-bold">@_nombreUsuario</MudText>
    </div>
</MudPaper>

@* Stat cards row + charts section in MudGrid *@
```

### Account Settings Layout

```razor
<MudGrid>
    <MudItem xs="12" md="3">
        <MudPaper Class="pa-4 text-center" Elevation="0" Outlined="true">
            <MudAvatar Size="Size.Large" Color="Color.Primary" Class="mb-3 mx-auto">GK</MudAvatar>
            <MudText Typo="Typo.h6">@_nombreUsuario</MudText>
            <MudText Typo="Typo.caption" Color="Color.Secondary">@_rolUsuario</MudText>
            <MudDivider Class="my-3" />
            <MudNavMenu Class="text-left">
                <MudNavLink Href="/account/settings">Personal Info</MudNavLink>
                <MudNavLink Href="/account/security">Security</MudNavLink>
                <MudNavLink Href="/account/notifications">Notifications</MudNavLink>
            </MudNavMenu>
        </MudPaper>
    </MudItem>
    <MudItem xs="12" md="9">
        <MudPaper Class="pa-6" Elevation="0" Outlined="true">
            @* Form sections separated by MudDivider *@
            <MudButton Variant="Variant.Filled" Color="Color.Primary">Save Changes</MudButton>
        </MudPaper>
    </MudItem>
</MudGrid>
```

### Loading State

```razor
@if (_cargando)
{
    <MudSkeleton Width="100%" Height="200px" Class="mb-4" />
    <MudSkeleton Width="100%" Height="400px" />
}
else if (_entity is null)
{
    <MudPaper Class="pa-8 text-center" Elevation="0" Outlined="true">
        <MudIcon Icon="@Icons.Material.Filled.ErrorOutline" Size="Size.Large" Color="Color.Error" />
        <MudText Typo="Typo.h6" Class="mt-2" Color="Color.Error">X no encontrado</MudText>
        <MudButton Variant="Variant.Text" Color="Color.Primary"
            OnClick="@(() => Navigation.NavigateTo("/entities"))" Class="mt-2">
            Volver a lista
        </MudButton>
    </MudPaper>
}
else { @* content *@ }
```

## Spanish Conventions

| Contexto | Español |
|----------|---------|
| Título de página | Plural: "Tenants", "Roles", "Usuarios" |
| Breadcrumb | "PassPlat / @_seccion / @_tituloPagina" |
| Botón crear | "Nuevo X" / "Nueva X" según género |
| Búsqueda | `Label="Buscar..."` |
| Estado | "Todos", "Activo", "Inactivo" |
| Confirmar | Title: "Confirmar", Sí: "Sí, desactivar" |
| Diálogo crear | "Crear" / "Guardar cambios" |
| Tooltips | "Editar", "Eliminar", "Ver detalle" |
| Vacío | "No hay registros" |
| Éxito | "X creado", "X actualizado", "X desactivado" |
| Error | "Error al cargar" |

## Ubold Pattern Adoption

| Patrón | Estado |
|--------|--------|
| Breadcrumb + page header | ✅ Hecho |
| Stat cards con avatar color | ✅ Hecho |
| Status chips semánticos | ✅ Hecho |
| Entity cards + avatar groups | ✅ Hecho |
| Filter toolbar | ✅ Hecho |
| 3-dot action menu | ✅ Hecho |
| Profile dropdown | 🟡 Medio |
| Notification dropdown | 🟡 Medio |
| Theme customizer panel | 🟡 Medio |
| Account settings page | 🟡 Medio |
| Dashboard layout | 🟡 Bajo |
| Error pages | 🟡 Bajo |

## Do NOT

- Usar `MudDataGrid` (usar `MudTable`)
- Usar `MudFab` (usar botón en toolbar)
- Hardcodear colores hex (usar `Color.*`)
- Saltar estados loading/empty/error
- Usar CSS custom cuando MudBlazor provee la funcionalidad
- Olvidar inyectar `NavigationManager`
- Usar fetch síncrono en `OnInitialized` (usar `OnInitializedAsync`)
- Usar inglés en labels, tooltips o mensajes UI
- Llamar `SaveChangesAsync` desde servicios (solo desde WebAPI consumer)
- Pasar `null` a `Result<T>.Success()` sin `allowNull: true`
