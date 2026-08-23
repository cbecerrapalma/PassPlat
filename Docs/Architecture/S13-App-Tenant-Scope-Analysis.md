# S13 — App/Tenant Scope Analysis (F6–F11)

> **Fase**: S13 F6–F11 · **Carácter**: read-only (inventario + análisis, sin cambios de código)
> **Fecha**: sesión S13
> **Objetivo**: Inventariar todas las entidades y tablas que portan alcance `IdApp`, `IdTenant` (o ambos), y documentar las jerarquías de herencia/fallback de configuración y permisos entre Global → Tenant → App.

---

## 1. Metodología

- Lectura de entidades en `PassPlat.Dominio/Entities/{Catalogos,Core}`.
- Lectura de repositorios en `PassPlat.Datos/Repositories/`.
- Lectura de servicios de resolución en `PassPlat.Aplicacion/`.
- Verificación cruzada contra `D:\CODIGOS\BBDD\PASSWORDS.sql` (FKs, índices únicos, triggers).

---

## 2. Modelo de alcance: dos ejes ortogonales

PassPlat organiza cada registro de configuración / política / permiso por dos ejes que pueden
ser nulos para significar "global":

| Eje | Columna | Semántica |
|-----|---------|-----------|
| **Tenant** | `IdTenant` | `int NOT NULL` → alcance a un tenant concreto. `NULL` → global (plataforma). |
| **App** | `IdApp` | `int NOT NULL` → alcance a una app concreta. `NULL` → todas las apps. |

**Regla de precedencia**: cuanto más específico, mayor prioridad al resolver.

---

## 3. Inventario de entidades con `IdApp` (por `PASSWORDS.sql`)

Tablas con columna `IdApp` y su FK:

| Tabla | IdApp | FK ref. | Observación |
|-------|-------|---------|-------------|
| `TokensRest` | int NULL | Apps (Id) | token reset opaque, puede no estar atado a app |
| `Sesiones` | int NOT NULL | — | sesión por app |
| `IntentosAcceso` | int NULL | — | intento de login por app |
| `UsuariosPermisos` | int NULL | `FK_UsuariosPermisos_App` | permiso concedido; `NULL` = válido para toda app del tenant |
| `PoliticasPwd` | int NULL | `FK_Politicas_App` | política app-específica; `NULL` = política de tenant o global |
| `EmailLog` | int NULL | `FK_EmailLog_App` | log de email por app |
| `AppsModulos` | int NOT NULL | `FK_AppsModulos_App` | join App↔Módulo |
| `AppEmailAccounts` | int NOT NULL | `FK_AppEmailAcct_App` | cuenta email predeterminada por app |
| `Accesos` | int NOT NULL | `FK_Accesos_App` | acceso Usuario-Tenant-App-Rol |

`Apps` (línea 2346) es catálogo **global** sin `IdTenant`: una app pertenece a la plataforma, no a un tenant. De hecho no tiene columna `IdTenant`.

---

## 4. Entidades con `IdTenant` (espaciado ampliado)

Catálogo completo de tablas con FK a `Tenants (Id)` (de `PASSWORDS.sql`, líneas `FK_*_Tenant`):

`Roles` (324), `RolesHerencia` (431), `Grupos` (454), `DominioTenant` (468), `ConfSaml` (521), `ConfLdap` (584), `ConfigTenant` (618), `Sesiones` (645 idx), `ConfProvIden` (861), `Notificaciones` (926), `UsuarioTenant` (1225), `Usuarios` (1371), `SamlSession` (1446), `LdapSyncLog` (1522), `EmailTemplate` (1618), `TenantEmailAccounts` (1692/1812), `DispConfiables` (1836), `IdenExt` (1940), `HistorialIdenExt` (2100), `AudIdenExt` (2202 idx), `UsuariosPermisos` (2444), `PoliticasPwd` (2522), `RolesPoliticasPwd` (2561), `EmailLog` (2646), `Accesos` (2802).

> Nota: `Config` (ConfigApp) lleva `IdTenant` pero **no** constraint FK explícito referido en el listado (columna `IdTenant` nullable tabla `ConfigApp`), revisada con `ActualizarConfigAppDto`.

---

## 5. Entidades con AMBOS ejes (tenant + app)

| Entidad | IdTenant | IdApp | Semántica |
|---------|----------|-------|-----------|
| `Acceso` | int | int NOT NULL | un usuario tiene acceso a un `(Tenant, App, Rol)`. |
| `UsuarioPermiso` | int | int? | permiso directo: `NULL` app → aplica a toda app del tenant. |
| `Sesion` | int | int NOT NULL | sesión activa en un tenant y una app. |
| `IntentoAcceso` | int? | int? | intento de login opcionalmente por tenant/app. |
| `PoliticaPwd` | int? | int? | política app → `IdApp!=NULL`; tenant → `IdApp=NULL`; global → ambos NULL. |
| `EmailLog` | int? | int? | log opcionalmente por tenant/app. |

---

## 6. FKs con preview en `Apps (Id)` y `Tenants (Id)`

En `PASSWORDS.sql` las FKs a `Apps` son explícitas:
- `FK_UsuariosPermisos_App` (2436)
- `FK_Politicas_App` (2518)
- `FK_EmailLog_App` (2634)
- `FK_AppsModulos_App` (2676)
- `FK_AppEmailAcct_App` (2717)

---

## 7. Jerarquías de resolución observadas

### 7.1 Políticas de contraseña (Global → Tenant → App)
`PoliticaPwdRepository.ObtenerPoliticaAplicableAsync(idTenant, idApp?)`:
```
if (idApp.HasValue) → buscar (IdTenant=idTenant, IdApp=idApp, Activa)
  → si no → buscar (IdTenant=idTenant, IdApp=NULL, Activa)   // tenant
  → si no → ObtenerPoliticaGlobalAsync()                       // global
```
- `ObtenerPoliticaGlobalAsync`: `IdTenant == null && IdApp == null && Activa` — política de plataforma.
- `ObtenerPoliticaParaRolAsync(idTenant, idRol)`: política unida a un rol concreto del tenant vía `RolesPoliticasPwd`.

### 7.2 Permisos por Usuario (Tenant + App nullable)
`UsuarioPermiso`:
- `IdTenant` obligatorio: el permiso vive en un tenant.
- `IdApp` nullable: si `NULL`, el permiso aplica a **toda app** del tenant; si valor, solo a esa app.
- Verificado en EF/entidad: `int? IdApp`, y factory `Crear(..., int? idApp = null)`.
- SQL: índice filtrado `WHERE ([IdApp] IS NOT NULL)` (2384) e índice `(IdUsuario, IdPermiso, IdTenant, IdApp)` (2401) con filtro `Activo=1 Y IdApp IS NOT NULL`.

### 7.3 Cuenta de email por app/tenant/global
`EmailAccountResolverService.ResolveAsync(idApp, idTenant)`:
```
1. App-level default (AppEmailAccounts) → si cuenta activa
2. Tenant-level default (TenantEmailAccounts) → si cuenta activa
3. Global `ObtenerPredeterminadaAsync`
4. Cualquier cuenta global activa
```
La resolución respeta la precedencia Global→Tenant→App pero con App primero (documentado).

---

## 8. Tablas globales (sin identidad Tenant/App)

`App` (catálogo de apps), `Permiso`, `Módulo`, `Rol` global (`Roles` con `IdTenant NULL`), `Prof` — definidas a nivel plataforma.

- `  `Rol`: `IdTenant` nullable → `NULL` = rol global de plataforma reutilizado por los tenant; nunca se comparte a lo largo de tenants (decisión A1 / `SEED_Tenant` define 4 por tenant: ADMIN/EDITOR/SUPERVISOR/CONSULTA).

---

## 9. Hallazgos observacionales (sin acción de código en S13)

1. **`EmailAccountResolverService`** prioriza App sobre Tenant — coherente con la precedencia del modelo (Global→Tenant→App).
2. **`ConfigApp`** es configuración clave/valor a nivel global o por tenant (sin eje App). `ConfigTenant` guarda configuración "fuerte" por tenant (timeout sesión, MFA, retención auditoría). No hay solapamiento.
3. **`IdApp` de `ExternalAuthService`** hardcodeado a `1` (deuda técnica F4) — el modelo admite `IdApp` por usuario en `AccesoS`/`Permissions`, pero OAuth no lo resuelve aún.
4. Ninguna entidad con `IdApp` tiene FK compulsiva `NOT NULL` cuando podría ser global; los `NULL` significan "toda app" y eso está correcto en el modelo.

---

## 10. Documentos generados

- Este análisis (`S13-App-Tenant-Scope-Analysis.md`).
- Jerarquía de configuración discrete in `S13-Configuration-Hierarchy.md`.