# Informe Técnico: Arquitectura Formal de Módulos

**Proyecto:** PassPlat  
**Componente:** Sistema de Módulos (Modulos, AppsModulos, TiposModulo)  
**Fecha:** 2026-06-12  
**Versión:** 1.0

---

## 1. Justificación de Diseño

### Problema original

El sistema de permisos original almacenaba el módulo como un campo de texto libre (`Permisos.Modulo` varchar). Esto generaba:

- **Inconsistencia**: El mismo módulo podía escribirse como "Seguridad", "SEGURIDAD", "seguridad" por errores tipográficos.
- **Sin jerarquía**: No existía relación padre-hijo entre módulos, imposibilitando menús anidados.
- **Sin control de visibilidad**: No se podía diferenciar entre módulos de sistema, de tenant o compartidos.
- **Sin relación App-Módulo**: No se podía asignar módulos específicos a aplicaciones.
- **Navegación rígida**: Los menús laterales estaban hardcodeados en el frontend.

### Solución implementada

Se creó una arquitectura formal de módulos con tres nuevas tablas:

```
TiposModulo (catálogo fijo: SYSTEM, TENANT, SHARED)
    └── Modulos (jerárquico, auto-referencia IdModuloPadre)
            └── AppsModulos (relación N:M entre Apps y Modulos)
                    └── Permisos.IdModulo (FK reemplaza Permisos.Modulo varchar)
```

### Principios de diseño

1. **Separación SYSTEM vs TENANT**: `TiposModulo` con IDs fijos (1=SYSTEM, 2=TENANT, 3=SHARED) permite filtrar visibilidad según `EsSistema` del usuario sin joins adicionales.
2. **Jerarquía infinita**: `Modulos.IdModuloPadre` permite niveles ilimitados de anidamiento (menús, submenús, sub-submenús).
3. **Asignación por aplicación**: `AppsModulos` permite que cada aplicación (Web, WebAPI, Mobile) tenga su propio árbol de navegación.
4. **Migración automática**: Los valores existentes de `Permisos.Modulo` se migran a `Modulos.Codigo` mediante seed script, preservando compatibilidad.
5. **Navegación dinámica**: Endpoint `GET /api/modulos/menu` construye el árbol de navegación desde BD, eliminando menús hardcodeados.

---

## 2. Riesgos

### Riesgos identificados y mitigados

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|-------------|---------|------------|
| **SPs no actualizados** que referencian `Permisos.Modulo` | Media | Alto | Se actualizó `SP_Permisos_Usuario_Efectivos` para JOIN con `Modulos`. Auditoría completa de los 8 SPs. |
| **Referencias legacy en frontend** a `PermisoDto.Modulo` | Alta | Alto | Se corrigieron todas las referencias en razor files (4 archivos, 12 errores CS1061). Build 0 errores. |
| **Seed de módulos incompleto** que no cubra todos los valores de `Permisos.Modulo` existentes | Baja | Medio | Seed script itera sobre `SELECT DISTINCT Modulo FROM Permisos` y crea módulos automáticamente. |
| **Rotura de compatibilidad en DTOs** | Alta | Alto | Se mantuvo `RolPermisoDto.Modulo` como propiedad poblada desde `Modulos.Codigo` via AutoMapper. |

### Riesgos residuales

- **Permisos huerfanos**: Si un `Modulo` se desactiva, los permisos asociados quedan referenciando un módulo inactivo. Se recomienda migración manual o lógica soft-delete.
- **Carga de menú**: El endpoint `GET /api/modulos/menu` ejecuta múltiples queries (módulos + permisos del usuario + filtro por app). Para tenants con muchos módulos, considerar caching.

---

## 3. Impacto sobre Seguridad

### Mejoras

1. **Control granular de visibilidad**: Los módulos tipo `SYSTEM` (`IdTipoModulo=1`) solo son visibles para usuarios con `EsSistema=true`, evitando que usuarios de tenant accedan a funciones administrativas globales.
2. **Endpoint protegido**: `GET /api/modulos/menu` requiere autenticación JWT y está cubierto por la policy `PERMISOS_VER`.
3. **Validación de FK**: `Permisos.IdModulo` es FK obligatoria (`NOT NULL`), garantizando que todo permiso tenga un módulo válido.
4. **No exposición de módulos SYSTEM**: El backend filtra módulos SYSTEM antes de retornar el árbol de navegación, incluso si el frontend intentara acceder.

### Consideraciones

- La visibilidad de módulos se evalúa en backend (`ModuloService.ObtenerVisiblesMenuAsync`). Nunca confiar en filtros del frontend.
- `AppsModulos` tiene unique index filtrado (`WHERE Activo = 1`) para evitar duplicados activos, pero no impide asignar módulos SYSTEM a aplicaciones de tenant. Esta validación debe hacerse a nivel aplicación.

---

## 4. Impacto sobre Permisos

### Cambio estructural

| Antes | Después |
|-------|---------|
| `Permiso.Modulo` varchar(50) | `Permiso.IdModulo` int (FK → `Modulos.Id`) |
| Sin relación con tabla de módulos | FK con `Modulos`, `ON DELETE NO ACTION` |
| Agrupación por string | Agrupación por `Modulos.Codigo` (normalizado) |
| Sin orden definido entre módulos | `Modulos.Orden` permite ordenamiento explícito |

### Compatibilidad

- `RolPermisoDto.Modulo` se conserva como propiedad string, poblada desde `Modulos.Codigo` via AutoMapper.
- `PermisosUsuarioEfectivosResult.Modulo` se conserva en el SP, ahora poblado via JOIN con `Modulos`.
- El filtro por módulo en el frontend sigue funcionando: agrupa por `ModuloNombre ?? ModuloCodigo`.

### Impacto en queries

- Antes: `SELECT * FROM Permisos WHERE Modulo = 'Seguridad'`
- Después: `SELECT * FROM Permisos p JOIN Modulos m ON p.IdModulo = m.Id WHERE m.Codigo = 'SEGURIDAD'`

Join adicional, pero permite joins más potentes (traer nombre del módulo, tipo, etc.).

---

## 5. Impacto sobre Autenticación

### Sin cambios estructurales

La autenticación (JWT, login, MFA, sesiones) no se vio afectada por este cambio. Los claims de `is_system` y `IdTenant` ya existían y son utilizados por el nuevo sistema para filtrar módulos.

### Mejora indirecta

El sistema de módulos permite ahora crear permisos específicos por módulo (e.g., `USUARIOS_VER`, `ROLES_EDITAR`), que pueden asignarse a roles y evaluarse vía policies:

```csharp
[Authorize(Policy = "USUARIOS_VER")]
```

Esto ya era posible antes, pero ahora los permisos están correctamente categorizados por módulo.

---

## 6. Impacto sobre Multitenancy

### Mecanismo actual

- `Tenants.EsSistema`: Marca tenants de sistema (no aplica filtro por tenant).
- `Usuarios.EsSistema`: Marca usuarios con acceso total (no aplican filtros multi-tenant).
- `ITenantContext.CurrentId`: Proporciona el ID del tenant actual desde el JWT.

### Impacto en módulos

| Aspecto | Antes | Después |
|---------|-------|---------|
| Módulos visibles para tenant | No aplicable | Módulos tipo TENANT y SHARED |
| Módulos visibles para sistema | No aplicable | Todos (SYSTEM, TENANT, SHARED) |
| Filtro por tenant en módulos | No aplicable | Vía `AuthRepository.ObtenerUsuarioBasicoAsync` + `EsSistema` |

### Tablas involucradas

| Tabla | Tiene IdTenant? | Estrategia |
|-------|----------------|------------|
| `Modulos` | No | Módulos son globales; el filtro es por tipo (SYSTEM/TENANT/SHARED) |
| `AppsModulos` | No | Asignación global; cada tenant puede tener distintas apps con distintos módulos |
| `Permisos` | No | Los permisos son globales; el tenant determina qué roles/usuarios tienen qué permisos |

Las tablas de módulos son globales (sin `IdTenant`). El multi-tenant se aplica indirectamente: los usuarios de tenant solo ven módulos TENANT y SHARED, mientras que los usuarios de sistema ven todos.

---

## 7. Impacto sobre Repository/UoW

### Nuevos repositorios

| Repositorio | Métodos clave |
|-------------|---------------|
| `ModuloRepository` | `ObtenerRaicesAsync`, `ObtenerArbolCompletoAsync`, `ObtenerVisiblesMenuAsync`, `ObtenerPorAppAsync` |
| `AppModuloRepository` | `ObtenerPorAppAsync`, `ObtenerPorModuloAsync`, `AsignarModuloAsync`, `DesasignarModuloAsync` |

### Nuevos servicios

| Servicio | Métodos clave |
|----------|---------------|
| `ModuloService` | `CrearAsync`, `ActualizarAsync`, `ObtenerArbolCompletoAsync`, `ObtenerRaicesAsync`, `ObtenerVisiblesMenuAsync`, `ObtenerPorAppAsync` |
| `AppModuloService` | `AsignarModuloAsync`, `DesasignarModuloAsync`, `ObtenerPorAppAsync` |

### Patrón de uso

```csharp
// Controlador
var result = await _service.ObtenerVisiblesMenuAsync(idUsuario, idApp, ct);
return FromResultQuery(result);
```

Todos los servicios siguen el patrón estándar:
1. Repositorio retorna `Result<T>` con try-catch `DB_ERROR`.
2. Servicio verifica `IsFailure` antes de usar `Value`.
3. Controlador usa `FromResult` / `FromResultQuery`.

### Registros DI

```csharp
// DatosDependencyInjection
services.AddScoped<IModuloRepository, ModuloRepository>();
services.AddScoped<IAppModuloRepository, AppModuloRepository>();

// AplicacionDependencyInjection
services.AddServiceAsync<IModuloService, ModuloService>();
services.AddServiceAsync<IAppModuloService, AppModuloService>();
```

---

## 8. Impacto sobre UI Dinámica

### Endpoint de navegación

```
GET /api/modulos/menu
Headers: X-App-Id (default: 1)
Auth: JWT (NameIdentifier → IdUsuario)
```

Respuesta: árbol jerárquico de `ModuloDto` con `SubModulos` anidados.

### Uso en frontend

El endpoint se consume en el layout principal del frontend (e.g., `MainLayout.razor`) para construir el menú lateral dinámicamente:

```razor
@foreach (var modulo in _menu)
{
    if (modulo.SubModulos.Count == 0)
    {
        <MudNavLink Href="@modulo.Ruta" Icon="@modulo.Icono">@modulo.Nombre</MudNavLink>
    }
    else
    {
        <MudNavGroup Title="@modulo.Nombre" Icon="@modulo.Icono" Expanded="false">
            @foreach (var sub in modulo.SubModulos)
            {
                <MudNavLink Href="@sub.Ruta" Icon="@sub.Icono">@sub.Nombre</MudNavLink>
            }
        </MudNavGroup>
    }
}
```

### Ventajas

- **Sin hardcode**: Agregar un nuevo módulo en BD lo hace visible automáticamente en el menú.
- **Configurable por app**: Cada aplicación puede tener su propio conjunto de módulos.
- **Visibilidad controlada**: Módulos SYSTEM invisibles para usuarios de tenant.
- **Orden explícito**: `Modulos.Orden` define la posición en el menú.

### Consideraciones

- El endpoint requiere una llamada API al cargar la página. Considerar caching del menú (e.g., `CBP.Caching`) para reducir latencia.
- Los módulos sin `Ruta` ni `SubModulos` no generan entradas de menú navegables (son agrupaciones lógicas).
- `EsVisibleMenu = false` permite tener módulos que agrupan permisos pero no aparecen en el menú.

---

## 9. Recomendaciones Futuras

### Corto plazo

1. **Caching de menú**: Implementar `IDistributedCache` para cachear el árbol de navegación por usuario+app con invalidación al modificar `AppsModulos`.
2. **Auditoría de módulos**: Agregar eventos de auditoría (`CBP.Events`) para cambios en módulos (creación, modificación, asignación a apps).
3. **Validación a nivel servicio**: Impedir asignación de módulos SYSTEM a aplicaciones de tenant en `AppModuloService`.

### Mediano plazo

4. **Permisos por módulo**: Crear un endpoint `GET /api/modulos/{id}/permisos` que retorne los permisos de un módulo específico, útil para la UI de asignación masiva.
5. **UI de administración de módulos**: Crear páginas CRUD para módulos (actualmente solo existen APIs).
6. **Ordenamiento drag & drop**: Agregar endpoint `PUT /api/modulos/reordenar` para cambiar el orden de módulos y sub-módulos desde una UI.

### Largo plazo

7. **Roles por módulo**: Permitir asignar roles a nivel de módulo (e.g., "Administrador de Seguridad" tiene todos los permisos del módulo SEGURIDAD).
8. **Módulos por tenant**: Considerar agregar `ModulosTenant` para que cada tenant pueda tener módulos personalizados sin afectar a otros.
9. **Internacionalización**: Agregar soporte multi-idioma para `Modulos.Nombre` y `Modulos.Descripcion`.
10. **Migración a GraphQL**: Para la carga del árbol de navegación, GraphQL permitiría al frontend solicitar exactamente los campos necesarios.

---

## Anexo A: Estructura de Tablas

### TiposModulo
| Columna | Tipo | Descripción |
|---------|------|-------------|
| Id | int (PK) | 1=SYSTEM, 2=TENANT, 3=SHARED |
| Codigo | varchar(20) | Código único |
| Nombre | varchar(100) | Nombre descriptivo |
| Activo | bit | Soft-delete |

### Modulos
| Columna | Tipo | Descripción |
|---------|------|-------------|
| Id | int (PK, IDENTITY) | Auto-incremental |
| IdModuloPadre | int? (FK→Modulos) | Padre (jerarquía) |
| IdTipoModulo | int (FK→TiposModulo) | SYSTEM/TENANT/SHARED |
| Codigo | varchar(50) | Código único |
| Nombre | varchar(100) | Nombre |
| Descripcion | varchar(255)? | Opcional |
| Ruta | varchar(500)? | Ruta de navegación |
| Icono | varchar(50)? | Icono MudBlazor |
| Orden | tinyint | Posición |
| EsVisibleMenu | bit | Visible en menú |
| Activo | bit | Soft-delete |
| FecCrea | datetime | Default sysdatetime() |

### AppsModulos
| Columna | Tipo | Descripción |
|---------|------|-------------|
| Id | int (PK, IDENTITY) | Auto-incremental |
| IdApp | int (FK→Apps) | Aplicación |
| IdModulo | int (FK→Modulos) | Módulo |
| Activo | bit | Soft-delete |
| FecCrea | datetime | Default sysdatetime() |

### Permisos (columna modificada)
| Columna | Antes | Después |
|---------|-------|---------|
| Modulo | varchar(50) | *(eliminado)* |
| IdModulo | *(no existía)* | int NOT NULL (FK→Modulos) |

## Anexo B: Seed de Datos

```sql
-- TiposModulo fijos
INSERT INTO TiposModulo (Id, Codigo, Nombre) VALUES
(1, 'SYSTEM', 'Sistema'),
(2, 'TENANT', 'Tenant'),
(3, 'SHARED', 'Compartido');

-- Migración desde Permisos.Modulo existentes
INSERT INTO Modulos (Codigo, Nombre, IdTipoModulo, EsVisibleMenu, Orden)
SELECT DISTINCT
    UPPER(REPLACE(Modulo, ' ', '_')),
    Modulo,
    2,  -- TENANT por defecto
    0,  -- No visible en menú (agrupación lógica)
    0
FROM Permisos
WHERE Modulo IS NOT NULL
  AND Modulo NOT IN (SELECT Codigo FROM Modulos);

-- Actualizar FK
UPDATE p SET p.IdModulo = m.Id
FROM Permisos p
JOIN Modulos m ON UPPER(REPLACE(p.Modulo, ' ', '_')) = m.Codigo
WHERE p.IdModulo IS NULL;
```
