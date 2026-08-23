# Security Model Traceability Matrix

> Generado: 2026-07-22  
> Propósito: Trazabilidad completa Controller → Policy → Permiso → Módulo → App → NavMenu → Página Blazor

## Convenciones

- ✅ = Existe y coincide
- ⚠️ = Existe pero con discrepancia
- ❌ = No existe / No se encuentra
- — = No aplica

---

## 1. IAM (100-199)

### Usuarios (Módulo: 110)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| UsuariosController | GET / | USUARIOS_VER | ✅ Id=1 | ✅ Mod 101 | ✅ /usuarios | ✅ Usuarios/Index | ✅ |
| UsuariosController | GET /{id} | USUARIOS_VER | ✅ | ✅ | — | ✅ UsuarioDetail | ✅ |
| UsuariosController | POST / | USUARIOS_CREAR | ✅ Id=2 | ✅ | — | ✅ UsuarioDialog | ✅ |
| UsuariosController | PUT /{id} | USUARIOS_EDITAR | ✅ Id=3 | ✅ | — | ✅ UsuarioDialog | ✅ |
| UsuariosController | DELETE /{id} | USUARIOS_ELIMINAR | ✅ Id=4 | ✅ | — | — | ✅ |

### Sesiones (Módulo: 150 — NUEVO, no existe en seed actual)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| SesionesController | GET /activas/{id} | USUARIOS_VERSESIONES | ✅ Id=51 (Mod 101) | ❌ No tiene módulo propio | ❌ No en NavMenu | ✅ Sesiones/Index | ⚠️ Permiso cuelga de Usuarios |
| SesionesController | POST /revocar | SESIONES_REVOCAR | ✅ Id=52 (Mod 101) | ❌ No tiene módulo propio | ❌ No en NavMenu | — | ⚠️ |

### Bloqueos (Módulo: 170 — NUEVO)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| BloqueosController | GET /esta-bloqueado/{id} | USUARIOS_VERBLOQUEOS | ✅ Id=53 | ✅ Mod 101 (Usuarios) | ❌ No en NavMenu | ✅ Bloqueos/Index | ⚠️ |

### MFA (Módulo: 160 — NUEVO)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| MfaController | GET /metodos/{id} | USUARIOS_VERMFA | ✅ Id=55 | ✅ Mod 101 (Usuarios) | ❌ No en NavMenu | ✅ MFA/Index | ⚠️ |

### Roles (Módulo: 120)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| RolesController | GET / | ROLES_VER | ✅ Id=13 | ✅ Mod 102 | ✅ /admin/roles | ✅ Roles/Index | ✅ |
| RolesController | POST / | ROLES_CREAR | ✅ Id=14 | ✅ | — | ✅ RolDialog | ✅ |
| RolesController | PUT /{id} | ROLES_EDITAR | ✅ Id=15 | ✅ | — | ✅ RolDialog | ✅ |
| RolesController | DELETE /{id} | ROLES_ELIMINAR | ✅ Id=16 | ✅ | — | — | ✅ |
| RolesHerenciaController | GET /hijos/{id} | — | ❌ Sin policy | ❌ | — | — | ⚠️ Solo [Authorize] genérico |

### Permisos (Módulo: 130)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| PermisosController | GET / | PERMISOS_VER | ✅ Id=17 | ✅ Mod 103 | ✅ /admin/permisos | ✅ Permisos/Index | ✅ |
| ModulosController | GET / | PERMISOS_VER | ✅ | ✅ Mod 103 | — | — | ✅ |
| MatrizPermisosController | GET / | PERMISOS_VER | ✅ | ✅ | ✅ /admin/matriz-permisos | ✅ MatrizPermisos/Index | ✅ |

### Accesos (Módulo: 140)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| AccesosController | GET /tiene-acceso | ACCESOS_VER | ✅ Id=20 | ✅ Mod 105 | ✅ /accesos | ✅ Accesos/Index | ✅ |
| AccesosController | POST /asignar | ACCESOS_ASIGNAR | ✅ Id=21 | ✅ | — | — | ✅ |
| AccesosController | POST /revocar | ACCESOS_REVOCAR | ✅ Id=22 | ✅ | — | — | ✅ |

### Grupos (Módulo: 180 — NUEVO)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| GruposController | GET / | — | ❌ Sin policy | ❌ Mod 104 existe | ✅ /admin/grupos | ✅ Grupos/Index | ⚠️ Solo [Authorize] genérico |

---

## 2. Federación (200-299)

### ProvIden (Módulo: 210 — NUEVO)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| ProvIdenController | GET / | — | ❌ Sin policy | ❌ Sin módulo | ✅ /federacion/providen | ✅ ProvIden/Index | ⚠️ Sin permisos |
| ProvIdenController | POST / | [IgnoreApi] | ❌ | ❌ | — | ✅ ProvIdenDialog (ro) | ⚠️ |

### ConfProvIden (Módulo: 220 — NUEVO)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| ConfProvIdenController | GET /tenant/{id} | — | ❌ Sin policy | ❌ Sin módulo | ✅ /federacion/confproviden | ✅ ConfProvIden/Index | ⚠️ Sin permisos |

### IdenExt (Módulo: 230 — NUEVO)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| IdenExtController | GET /usuario/{id} | — | ❌ Sin policy | ❌ Sin módulo | ✅ /federacion/iden-ext | ✅ IdenExt/Index | ⚠️ Sin permisos |

### AudIdenExt (Módulo: 240 — NUEVO)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| AudIdenExtController | GET /tenant/{id} | ADMIN | ❌ | ❌ | ❌ No en NavMenu | ❌ Sin página | ⚠️ Policy genérica ADMIN |

---

## 3. Aplicaciones (300-399)

### Apps (Módulo: 310)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| AppsController | GET / | APPS_VER | ✅ Id=9 | ✅ Mod 111 | ✅ /apps | ✅ Apps/Index | ✅ |
| AppsController | POST / | APPS_CREAR | ✅ Id=10 | ✅ | — | ✅ AppDialog | ✅ |

### Módulos (Módulo: 320 — NUEVO)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| ModulosController | POST / | PERMISOS_CREAR | ✅ Id=18 (Mod 103) | ❌ Sin módulo propio | ❌ No en NavMenu | ❌ Sin página | ⚠️ |
| TiposModuloController | GET / | MODULOS_VER | ✅ Id=60 (Mod 112) | ✅ | ❌ | ❌ | ⚠️ |

---

## 4. Plataforma (400-499)

### Tenants (Módulo: 410)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| TenantsController | GET / | TENANTS_VER | ✅ Id=5 | ✅ Mod 121 | ✅ /tenants | ✅ Tenants/Index | ✅ |

### ConfigApp (Módulo: 420)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| ConfigAppController | GET / | CONFIG_APP_VER | ✅ Id=40 | ✅ Mod 122 | ✅ /config-app | ✅ ConfigApp/Index | ✅ |

### PoliticasPwd (Módulo: 430)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| PoliticasPwdController | GET / | POLITICAS_PWD_VER | ✅ Id=37 | ✅ Mod 123 | ✅ /politicas-pwd | ✅ PoliticasPwd/Index | ✅ |

### Mantenimiento (Módulo: 440)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| MaintenanceController | POST /purge | MANTENIMIENTO_VER | ✅ Id=32 | ✅ Mod 124 | ✅ /mantenimiento | ✅ Maintenance/Index | ✅ |

---

## 5. Correos (500-599)

### Plantillas (Módulo: 510)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| EmailTemplatesController | GET / | EMAIL_TEMPLATES_VER | ✅ Id=33 | ✅ Mod 131 | ✅ /email-templates | ✅ EmailTemplates/Index | ✅ |

### Providers (Módulo: 520)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| EmailProvidersController | GET / | EMAIL_PROVIDERS_VER | ✅ Id=43 | ✅ Mod 132 | ✅ /email/providers | ✅ Providers | ✅ |

### Cuentas (Módulo: 530)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| EmailAccountsController | GET / | EMAIL_ACCOUNTS_VER | ✅ Id=44 | ✅ Mod 133 | ✅ /email/accounts | ✅ Accounts | ✅ |

---

## 6. Auditoría (600-699)

### Eventos (Módulo: 610)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| AuditoriaPwdController | GET / | AUDITORIA_VER | ✅ Id=28 | ✅ Mod 141 | ✅ /auditoria | ✅ AuditoriaPwd/Index | ✅ |

### Intentos Acceso (Módulo: 620)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| IntentosAccesoController | GET /page | INTENTOS_ACCESO_VER | ✅ Id=30 | ✅ Mod 142 | ✅ /intentos-acceso | ✅ IntentosAcceso/Index | ✅ |

### Historial Password (Módulo: 630)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| HistorialPwdController | GET / | HISTORIAL_PWD_VER | ✅ Id=29 | ✅ Mod 143 | ✅ /historial-pwd | ✅ HistorialPwd/Index | ✅ |

### Notificaciones (Módulo: 640)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| NotificacionesController | GET / | NOTIFICACIONES_VER | ✅ Id=31 | ✅ Mod 144 | ✅ /notificaciones | ✅ Notificaciones/Index | ✅ |

---

## 7. Dashboards (700-799)

### Dashboard (Módulo: 710 — NUEVO)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| DashboardController | GET / | USUARIOS_VER | ✅ | ✅ | ✅ / (Panel Principal) | ✅ Dashboard | ✅ |

### Dashboard Enterprise (Módulo: 720 — NUEVO)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| DashboardEnterpriseController | GET /ejecutivo | USUARIOS_VER | ✅ | ❌ Sin módulo propio | ✅ /admin/dashboard-enterprise | ✅ DashboardEnterprise | ⚠️ |

---

## 8. Infraestructura (800-899)

### Dispositivos (Módulo: 810 — NUEVO)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| DispController | GET / | — | ❌ Sin policy | ❌ Sin módulo | ❌ No en NavMenu | ✅ Dispositivos/Index | ⚠️ |
| DispConfiablesController | GET / | USUARIOS_VERDISP | ✅ Id=57 (Mod 101) | ❌ Sin módulo propio | ❌ No en NavMenu | ✅ DispConfiables/Index | ⚠️ |

### IPs (Módulo: 830 — NUEVO)

| Controller | Endpoint | Policy | Permiso BD | Módulo BD | NavMenu | Página Blazor | Estado |
|-----------|----------|--------|------------|-----------|---------|---------------|--------|
| IPsController | GET /direccion/{dir} | — | ❌ Sin policy | ❌ Sin módulo | ❌ | ❌ | ⚠️ |

---

## Resumen de brechas

| Tipo | Cantidad | Descripción |
|------|----------|-------------|
| ❌ Controllers sin policy específica | 8 | ProvIden, ConfProvIden, IdenExt, Grupos, Disp, IPs, UserAgents, RolesHerencia |
| ❌ Policies sin permiso en BD (nuevas propuestas) | 20+ | FEDERACION_*, SESIONES_*, MFA_*, BLOQUEOS_*, etc. |
| ❌ Módulos sin módulo en BD | 12 | Federación (5), Sesiones, Bloqueos, MFA, Grupos, Dashboards (2), Dispositivos |
| ❌ NavMenu sin permiso | 3 | ProvIden, ConfProvIden, IdenExt (sección Federación completa) |
| ⚠️ Permisos en módulo incorrecto | 4 | USUARIOS_VERSESIONES, SESIONES_REVOCAR, USUARIOS_VERBLOQUEOS, USUARIOS_VERMFA (cuelgan de Usuarios en vez de módulo propio) |
| ❌ Páginas sin ruta en NavMenu | 12 | Sesiones, Bloqueos, MFA, Dispositivos, DispConfiables, DominiosTenant, etc. |
