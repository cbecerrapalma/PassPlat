# S12 — Login Context Resolution & UI Contract Recovery

**Fecha**: 2026-08-02
**Estado**: COMPLETADO
**Cambio principal**: Login.razor — selectores App + Tenant SIEMPRE visibles

---

## Problema Reportado

El usuario reportó que `/login` mostraba "No hay aplicaciones disponibles. Contacta al administrador." — el selector de App no era visible en la UI. La FASE anterior (2R.1) resolvía automáticamente la App cuando existía una sola, ocultando el selector visual.

## Causa Raíz

El código `OnInitializedAsync()` en `Login.razor` tenía:

```csharp
else if (_apps.Count == 1)
{
    _selectedAppId = _apps[0].Id;
    Auth.AppId = _selectedAppId;
    _requiereSeleccionApp = false;  // ← Ocultaba el selector visual
    await ResolverTenantAsync();
}
```

Cuando la App se auto-resolvía, `_requiereSeleccionApp = false` significaba que el bloque `@if (_requiereSeleccionApp)` no se renderizaba — **sin selector visual de App**.

El "No hay aplicaciones disponibles" ocurría cuando `GetAppsAsync()` retornaba `[]` (API no alcanzable, CORS, WASM cacheado). La API respondía correctamente, pero el error persistía por cache de navegador o WASM stale.

## Solución Aplicada

### Cambio en `Login.razor` (markup)

**ANTES**: Dos bloques condicionales mutuamente excluyentes:
- `@if (_requiereSeleccionApp)` → solo visible cuando >1 app
- `@if (_requiereSeleccionTenant)` → solo visible cuando requiere selección

**AHORA**: Dos selectores SIEMPRE visibles cuando hay datos:
```razor
@if (_apps.Count > 0)
{
    <MudSelect Value="_selectedAppId" ValueChanged="@((int id) => { OnAppChanged(id); })" ...>
        @foreach (var app in _apps)
        {
            <MudSelectItem Value="@app.Id">@app.Nombre (@app.Codigo)</MudSelectItem>
        }
    </MudSelect>
}

@if (_tenants.Count > 0)
{
    <MudSelect Value="_selectedTenantId" ValueChanged="@((int id) => { OnTenantChanged(id); })" ...>
        @foreach (var tenant in _tenants)
        {
            <MudSelectItem Value="@tenant.Id">@tenant.Nombre</MudSelectItem>
        }
    </MudSelect>
}
```

### Cambio en `Login.razor` (lógica)

**ANTES**:
```csharp
if (_apps.Count == 1)
{
    _requiereSeleccionApp = false;  // ocultaba selector
    await ResolverTenantAsync();
}
```

**AHORA**:
```csharp
_selectedAppId = _apps.Count == 1 ? _apps[0].Id : 0;
Auth.AppId = _selectedAppId;
_tenants = await ResolverTenantAsync();
```

### Métodos eliminados (huérfanos)

- `ContinuarConApp()` — ya no necesario (selectores directos)
- `ContinuarConTenant()` — ya no necesario

### Campos eliminados (sin uso)

- `_requiereSeleccionApp` — reemplazado por `_apps.Count > 0`
- `_requiereSeleccionTenant` — reemplazado por `_tenants.Count > 0`

### Gate central preservado

```csharp
private bool IsAuthenticationContextReady => Auth.AppId > 0 && TenantIdContexto > 0;
```

## Flujo Resultante

```
/login
  ├── "Selecciona una aplicación para continuar"
  │   └── MudSelect: AccessPlat (pre-seleccionado si 1 app)
  ├── "Selecciona un tenant para continuar"
  │   └── MudSelect: [Plataforma, Abarrotes del Sur, Vestuario del Norte]
  ├── [Cuando ambos seleccionados]
  │   ├── Formulario: Usuario + Contraseña + Recordarme
  │   ├── OAuth: "O continúa con" + Google
  │   └── Botón "Iniciar Sesión" habilitado
  └── POST /api/auth/login con {NomUsuario, Password, IdApp, IdTenant}
```

## Análisis de Casos (A-F)

| Caso | Descripción | Resultado |
|------|-------------|-----------|
| A | API inalcanzable | Descartado — API responde 200 |
| B | URL incorrecta | Descartado — `ApiBaseUrl=http://localhost:5000` correcto |
| C | Desajuste DTO | Descartado — `AppItem` (Id/Codigo/Nombre) compatible con `AppDto` |
| D | Auth requerida | Descartado — `GET /api/apps/activas` es `[AllowAnonymous]` |
| E | CORS | Descartado — funciona desde el navegador |
| F | App desactivada | Descartado — PASSPLAT tiene `Activa=1` |

## Archivos Modificados

| Archivo | Cambio |
|---------|--------|
| `PassPlat.Web/Pages/Login.razor` | Selectores siempre visibles, métodos huérfanos eliminados |

## Verificación

- **Build**: 0 errores, 338 warnings (pre-existentes MudBlazor)
- **UI manual**: App → Tenant → Form+OAuth → Login → Dashboard (test_multitenant en Abarrotes)
- **Tests**: 12/12 PASS (`tests/s12-login-context-ui.spec.ts`)
