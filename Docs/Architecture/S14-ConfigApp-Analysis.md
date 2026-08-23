# S14 — F7: ConfigApp Analysis

> Sprint S14 · FASE F7 (read-only) · Auditoría de `ConfigApp`: scope, herencia, consumidores, evaluación necesidad `IdApp`.

---

## Entidad `ConfigApp`

### Scope actual (F1)
- **TENANT** — tiene `IdTenant`, **no tiene `IdApp`**.
- Tabla de parámetros de configuración por tenant.

### Estructura clave
| Columna | Tipo | Descripción |
|---------|------|-------------|
| `Id` | int | PK |
| `IdTenant` | int | FK → Tenants (scope) |
| `Clave` | nvarchar(100) | Clave de configuración (ej: `MostrarProveedoresPlataforma`) |
| `Valor` | nvarchar(max) | Valor (string, boolean, JSON) |
| `Activo` | bit | Habilitado |
| `Grupo` | nvarchar(50) | Grupo lógico (ej: `General`, `OAuth`, `Email`) |
| `Descripcion` | nvarchar(500) | Descripción |
| `TipoDato` | nvarchar(20) | `string`, `bool`, `int`, `json` |
| `EsSistema` | bit | Solo lectura si true |

---

## Herencia y resolución

### Niveles de configuración
1. **TENANT** — `ConfigApp` filas con `IdTenant = X`.
2. **PLATFORM** — `ConfigApp` filas con `IdTenant = 1` (tenant `PLATFORM`, código `PLATFORM`).

### Resolución actual (ejemplo: `MostrarProveedoresPlataforma`)
```csharp
// ExternalLoginProviderService.cs:111-119
private async Task<bool> DebeHeredarProveedoresPlataformaAsync(CancellationToken ct)
{
    var cfgResult = await _configAppRepo.ObtenerPorClaveAsync(ConfigAppKeys.MostrarProveedoresPlataforma, null, ct);
    if (cfgResult.IsFailure || cfgResult.Value is null || !cfgResult.Value.Activo)
        return false;
    return string.Equals(cfgResult.Value.Valor, "true", StringComparison.OrdinalIgnoreCase) || cfgResult.Value.Valor == "1";
}
```

- Busca la clave en el tenant actual (parámetro `idTenant` pasado como `null` → usa contexto actual).
- Si no existe o inactivo → `false` (no hereda).
- Si existe y `Valor` = `true`/`1` → hereda proveedores del tenant plataforma.

### Claves conocidas (`ConfigAppKeys`)
| Clave | Grupo | Descripción | Herencia |
|-------|-------|-------------|----------|
| `MostrarProveedoresPlataforma` | OAuth | Mostrar proveedores de tenant plataforma en tenants hijos | Sí (explícita) |
| `AdminEmail` | General | Email administrador para notificaciones | No (por tenant) |
| `...` | ... | ... | ... |

---

## Consumidores actuales

| Servicio | Clave(s) | Uso |
|----------|----------|-----|
| `ExternalLoginProviderService` | `MostrarProveedoresPlataforma` | Heredar proveedores OAuth de plataforma |
| `EmailBackgroundService` | `AdminEmail` | Destinatario notificaciones admin |
| `UsuarioService` | Varias | Configuración general usuario |
| `AuthService` | Varias | Parámetros autenticación |

---

## ¿Requiere `IdApp`?

### Análisis
1. **Naturaleza de la configuración**: Parámetros globales de tenant (email admin, flags OAuth, límites, timeouts).
2. **Consumidores**: Todos operan a nivel **tenant**, no distinguen por aplicación.
3. **Herencia**: Solo `MostrarProveedoresPlataforma` tiene lógica de herencia plataforma→tenant; las demás son puramente por tenant.
4. **Apps inactivas**: Apps 2/3 inactivas no tienen ConfigApp propia.

### Conclusión: **NOT REQUIRED**

**Razones:**
- ConfigApp modela **políticas de tenant**, no de aplicación.
- No hay evidencia de necesidad de override por App (ej: timeout de sesión distinto por app dentro del mismo tenant).
- Agregar `IdApp` multiplicaría filas innecesariamente (10+ claves × N apps × M tenants).
- Si en futuro una app necesita override, crear `AppConfigApp` (APP_TENANT) separada.

---

## Evidencia SQL

```sql
-- ConfigApp por tenant
SELECT IdTenant, Clave, Valor, Activo, Grupo FROM ConfigApp WHERE Activo = 1 ORDER BY IdTenant, Clave;

-- Clave crítica para OAuth
SELECT * FROM ConfigApp WHERE Clave = 'MostrarProveedoresPlataforma' AND Activo = 1;

-- Herencia: tenant 3 no tiene la clave → hereda de tenant 1 (PLATFORM) si este la tiene activa
```

---

## Hallazgos

- ✅ Scope TENANT correcto y consistente.
- ✅ Herencia plataforma→tenant implementada solo donde tiene sentido (`MostrarProveedoresPlataforma`).
- ✅ No hay consumidores que requieran scope APP.
- ⚠️ Clave `AdminEmail` en tenant 1 usada como fallback global para notificaciones admin (ver FASE 13 email certification).
- ⚠️ No hay claves con override por App.

---

## Conclusión

**CERTIFIED — NOT REQUIRED** — `ConfigApp` permanece TENANT-only. No agregar `IdApp`.