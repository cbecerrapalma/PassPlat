# Security Model Gap Analysis

> Generado: 2026-07-22  
> Propósito: Identificar brechas entre el código fuente, la base de datos y los scripts de instalación.

---

## Gap 1: Controllers sin [Authorize(Policy=...)] específica

Estos controllers usan `[Authorize]` genérico sin política concreta:

| Controller | Riesgo |
|-----------|--------|
| ProvIdenController | Proveedores de identidad sin protección granular |
| ConfProvIdenController | Configuración OAuth sin control de acceso |
| IdenExtController | Identidades externas sin auditoría de permisos |
| GruposController | Grupos de usuarios sin permisos específicos |
| GruposUsuariosController | Asignación de grupos sin permisos |
| RolesHerenciaController | Herencia de roles sin permisos específicos |
| DispController | Dispositivos sin control |
| IPsController | Direcciones IP sin control |
| UserAgentsController | User agents sin control |
| EstadosMFAController | Catálogo MFA editable sin permiso |
| ResultadosAccesoController | Catálogo resultados sin permiso |
| TiposAuditoriaController | Catálogo auditoría sin permiso |
| EmailLogController | Logs de email sin permiso granular |
| EmailAssetsController | Assets de email sin permiso |
| TiposBloqueoController | Catálogo bloqueos sin permiso |

**Acción**: Asignar políticas FEDERACION_VER, FEDERACION_CONFIG_VER, GRUPOS_VER, etc.

---

## Gap 2: Policies definidas en controllers pero sin módulo propio en BD

| Policy | Controller | Módulo actual | Módulo propuesto |
|--------|-----------|--------------|-----------------|
| USUARIOS_VERSESIONES | SesionesController | 101 (Usuarios) | 150 (Sesiones) |
| SESIONES_REVOCAR | SesionesController | 101 (Usuarios) | 150 (Sesiones) |
| USUARIOS_VERBLOQUEOS | BloqueosController | 101 (Usuarios) | 170 (Bloqueos) |
| USUARIOS_VERMFA | MfaController | 101 (Usuarios) | 160 (MFA) |
| USUARIOS_VERDISP | DispConfiablesController | 101 (Usuarios) | 810 (Dispositivos) |
| ADMIN | AudIdenExtController | — | 240 (Auditoría Federación) |

**Decisión arquitectónica**: Mantener o separar. La propuesta actual separa en módulos propios con rangos nuevos (150, 160, 170, 810).

---

## Gap 3: Módulos existentes en BD pero sin página Blazor

| Módulo BD | Código | Pagina Blazor |
|-----------|--------|--------------|
| 106 Herencia Roles | IAM_HERENCIA | ❌ (solo dialog RolesHerenciaDialog) |
| 107 Matriz Permisos | IAM_MATRIZ | ✅ MatrizPermisos/Index |
| 113 Apps × Módulos | APPS_MODULOS | ❌ Sin página |
| 134 Cuentas × Tenant | CORR_CUENTAS_TENANT | ✅ TenantEmailAccounts |
| 135 Cuentas × App | CORR_CUENTAS_APP | ✅ AppEmailAccounts |
| 136 Historial Envíos | CORR_HISTORIAL | ❌ Sin página (solo EmailLog API) |

---

## Gap 4: Páginas Blazor sin NavMenu

| Ruta | Página | Permiso usado | En NavMenu |
|------|--------|--------------|-----------|
| /sesiones | Sesiones/Index | SESIONES_VER (no existe en BD) | ❌ |
| /bloqueos | Bloqueos/Index | BLOQUEOS_VER (no existe en BD) | ❌ |
| /mfa | MFA/Index | MFA_VER (no existe en BD) | ❌ |
| /dispositivos | Dispositivos/Index | — | ❌ |
| /disp-confiables | DispConfiables/Index | USUARIOS_VERDISP | ❌ |
| /dominios-tenant | DominiosTenant/Index | TENANTS_VER | ❌ |
| /config-tenants | ConfigTenants/Index | CONFIG_APP_VER | ❌ |
| /roles | Roles/Index | ROLES_DETAIL_VER | ❌ (enlazado como /admin/roles) |
| /password | CambiarPassword | — | ❌ |
| /password-seguridad | PasswordSeguridad | — | ❌ |
| /tokens-rest | TokensRest | — | ❌ |

---

## Gap 5: NavMenu sin permisos

| Entrada NavMenu | Ruta | Tiene permiso |
|----------------|------|--------------|
| Panel Principal | / | — (OK, siempre visible) |
| Proveedores (Federación) | /federacion/providen | ❌ |
| Config Proveedores | /federacion/confproviden | ❌ |
| Identidades Externas | /federacion/iden-ext | ❌ |

---

## Gap 6: Permisos en BD sin RolesPermisos (huérfanos)

Según seed actual, ADMIN tiene todos (1-66). Pero existen 74 permisos en BD — los IDs 67-74 no están asignados.

| ID | Código | Creado por |
|----|--------|-----------|
| 67-74 | ? | Migraciones manuales o certificaciones previas |

**Acción**: Investigar y documentar en próxima fase.

---

## Gap 7: Roles sin usuarios

| Rol | Id | Usuarios asignados |
|-----|----|-------------------|
| ADMIN (1) | 1 | sistema (1) |
| EDITOR (2) | 2 | admin_tenant (2) |

**Sin usuarios**: No aplica. Todos los roles tienen al menos un usuario.

---

## Gap 8: Módulos sin permisos asignados

| Módulo | Permisos |
|--------|----------|
| 106 Herencia Roles | ✅ 2 permisos |
| 107 Matriz Permisos | ✅ 1 permiso |
| 113 Apps × Módulos | ✅ 2 permisos |
| 134 Cuentas × Tenant | ✅ 1 permiso |
| 135 Cuentas × App | ✅ 1 permiso |
| 136 Historial Envíos | ✅ 1 permiso |

Todos los módulos con al menos 1 permiso. ✅

---

## Gap 9: Políticas de Contraseña sin Roles asignados

RolesPoliticasPwd: 0 registros. La política DEFAULT (Id=1) es global y no requiere asignación a roles específicos. ✅

---

## Gap 10: OAuth — ProvIden incompleto en seed

| Proveedor | En seed | En BD actual |
|-----------|---------|-------------|
| GOOGLE | ❌ | ✅ (Id=1) |
| MICROSOFT | ❌ | ❌ |
| GITHUB | ❌ | ✅ (Id=2) |
| APPLE | ❌ | ❌ |
| LINKEDIN | ❌ | ✅ (Id=3) |
| FACEBOOK | ❌ | ✅ (Id=4) |
| INSTAGRAM | ❌ | ❌ |

**Acción**: Seed debe contener los 7 proveedores.

---

## Priorización de acciones

| Prioridad | Gap | Impacto | Acción |
|-----------|-----|---------|--------|
| 🔴 Alta | 1 | Control de acceso | Asignar políticas a controllers sin [Authorize(Policy=...)] |
| 🔴 Alta | 2 | Arquitectura | Crear módulos nuevos (Sesiones, Bloqueos, MFA, Federación, Dispositivos) |
| 🔴 Alta | 5 | Seguridad | Agregar permisos a NavMenu de Federación |
| 🟡 Media | 4 | UX | Agregar entradas NavMenu para páginas sin navegación |
| 🟡 Media | 10 | OAuth | Completar ProvIden en seed (7 proveedores) |
| 🟢 Baja | 3 | Consistencia | Evaluar si falta página para módulo 113, 136 |
| 🟢 Baja | 6 | Limpieza | Investigar permisos 67-74 en BD |

---

## Métricas finales

| Métrica | Valor |
|---------|-------|
| Controllers totales | 64 |
| Controllers con policy específica | 43 (67%) |
| Controllers solo [Authorize] genérico | 15 (23%) |
| Controllers [AllowAnonymous] | 3 (5%) |
| Políticas distintas en controllers | 49 |
| Permisos en BD | 74 |
| Módulos en BD | 29 |
| Páginas Blazor | 30 |
| Entradas NavMenu | 28 (25 con permiso, 3 sin) |
| Roles | 2 |
| Proveedores OAuth en BD | 4 |
| Proveedores OAuth requeridos | 7 |
