# FASE 16 ETAPA 1 — Auditoría Inicial: Identity Management Enterprise

**Fecha**: 2026-07-01
**Estado**: ✅ Auditoría completada
**Archivos analizados**: 28 entidades, 56 repositories, 70+ services, 60+ controllers, 40+ Blazor pages

---

## Resumen Ejecutivo

La plataforma PassPlat tiene un subsistema de identidad híbrida funcional (FASE 15 certificada 98/100). Esta auditoría identifica qué existe, qué falta, qué es reutilizable y qué requiere refactorización para evolucionar hacia un sistema IAM Enterprise.

---

## ETAPA 2 — Gestión Enterprise de Identidades Externas

### Qué Existe
| Componente | Archivo | Estado |
|------------|---------|--------|
| Entity `IdentidadesExterna` | `PassPlat.Dominio/Entities/Core/IdentidadesExterna.cs` | Básico |
| Repository | `IdentidadesExternaRepository.cs` | CRUD estándar |
| Service | `IdentidadesExternaService.cs` | CRUD estándar |
| Controller | `IdentidadesExternasController.cs` | GetPaged, ObtenerPorUsuario, ObtenerPorSubExterno, Create, Desvincular |
| Blazor Page | `Federacion/IdentidadesExternas/Index.razor` | Table + Inspector (Info + Tokens) |

### Campos Actuales en IdentidadesExterna
```
Id, IdUsuario, IdProvIden, IdTenant, SubExterno, ProviderUserName,
EmailExterno, NombreExterno, Avatar, MetadataJson, ClaimsJson,
AccessToken, RefreshToken, IdToken, TokenExpiration, CorrelationId,
EsPrincipal, Activo, Eliminado, FecEliminacion, IdUsuarioElimina,
UltimoLogin, FecCrea, FecMod
```

### Qué Falta
| Campo | Tipo | Descripción |
|-------|------|-------------|
| `Scopes` | `string?` | Scopes autorizados por el proveedor |
| `UltimaIP` | `string?` | Última IP desde la que se autenticó |
| `UltimoDisp` | `int?` | FK → Disp (último dispositivo) |
| `UltimoUserAgent` | `string?` | Último navegador/UA |
| `UltimoTenant` | `int?` | Último tenant utilizado |
| `IdEstado` | `byte` | FK → nuevo catálogo EstadoIdentidadExterna |
| `FecRevocacion` | `DateTime?` | Cuándo se revocó |
| `IdUsuarioRevoca` | `int?` | Quién revocó |
| `MotivoRevocacion` | `string?` | Motivo de revocación |

### Acciones Faltantes en Controller
- `PUT /{id}` — Actualizar identidad (cambiar principal, etc.)
- `POST /{id}/revocar` — Revocar identidad
- `POST /{id}/cambiar-principal` — Cambiar proveedor principal
- `GET /{id}/historial` — Historial de cambios

---

## ETAPA 3 — Estados de Identidad

### Qué Existe
- Solo `bool Activo` + `bool Eliminado` en `IdentidadesExterna`
- `ConfProvIden.Estado` (byte) con valores 0/1/2

### Qué Falta
| Componente | Tipo | Descripción |
|------------|------|-------------|
| `EEstadoIdentidadExterna` | `enum` | Pendiente(1), Autorizada(2), Revocada(3), Expirada(4), Suspendida(5), Error(6), SincronizacionPendiente(7) |
| `EstadoIdentidadExterna` | `entity (Catalogos)` | Tabla catálogo con Id, Nombre, Descripcion, Color, Orden, Activo |
| `IdEstado` column | `IdentidadesExterna` | FK al catálogo |
| Migration SQL | Script | Nueva tabla + columna + seed data |

---

## ETAPA 4 — Historial de Identidades

### Qué Existe
- `AuditoriaIdentidadExterna` con: Id, IdTenant, IdProvIden, IdUsuario, SubExterno, Evento, Resultado, Detalle, IP, UserAgent, CorrelationId, FecEvento
- `AuditoriaIdentidadExternaRepository.cs` — CRUD básico

### Qué Falta
| Tabla | Descripción |
|-------|-------------|
| `HistorialIdentidadExterna` | Registro granular de cambios: TipoCambio, ValorAnterior, ValorNuevo, RealizadoPor, EsAutomatico, Fecha, Usuario, Tenant, CorrelationId |

### Tipos de Cambio a Registrar
- Proveedor agregado / eliminado / cambiado como principal
- Proveedor revocado
- Cambio de email / avatar / nombre / scopes / estado
- Cambio realizado por admin / automáticamente

---

## ETAPA 5 — Gestión de Sesiones

### Qué Existe
| Componente | Archivo | Estado |
|------------|---------|--------|
| Entity `Sesion` | `Sesion.cs` | Completo (Id, IdUsuario, IdTenant, IdApp, IdTokenExt, IdDisp, IdIP, HashRefresh, FecInicio, UltActividad, FecExpira, EsActiva) |
| Repository | `SesionRepository.cs` | CrearSesionAsync, RevocarTodas, IntentarRotarHashRefreshAsync |
| Service | `SesionService.cs` | CRUD básico |
| Controller | `SesionesController.cs` | Activas, Revocar, Contar |
| Blazor Page | `Sesiones/Index.razor` | Table + Inspector |

### Qué Falta
| Campo/Acción | Descripción |
|--------------|-------------|
| `MetodoAutenticacion` | Local / Google / GitHub / LinkedIn / Facebook / Instagram |
| `Browser` | Extraído del User-Agent |
| `OS` | Extraído del User-Agent |
| `Pais` | Geolocalización por IP |
| Revocar JWT | Invalidar JWT específico |
| Revocar Refresh Token | Revocar token de refresco |
| Cerrar todas | Revocar todas las sesiones de un usuario |
| Auditoría | Registrar todas las operaciones de sesión |

---

## ETAPA 6 — Gestión de Dispositivos

### Qué Existe
| Componente | Archivo | Estado |
|------------|---------|--------|
| Entity `Disp` | `Disp.cs` | Id, IdTipoDisp, Fabricante, Modelo, FecPrimerReg, UltActividad |
| Entity `DispConfiable` | `DispConfiable.cs` | IdUsuario, IdDisp, Confiable, FecAlta |
| Repository | `DispRepository.cs` | CRUD básico |
| Repository | `DispConfiableRepository.cs` | CRUD básico |
| Service | `DispService.cs` | CRUD |
| Service | `DispConfiableService.cs` | MarcarConfiable, RevocarConfianza |
| Controller | `DispController.cs` | CRUD |
| Controller | `DispConfiablesController.cs` | MarcarConfiable, RevocarConfianza, ObtenerPorUsuario |
| Blazor Page | `DispConfiables/Index.razor` | Table básica |

### Qué Falta
| Página/Acción | Descripción |
|---------------|-------------|
| `DispManager.razor` | Página completa de gestión de dispositivos |
| `CantidadLogins` | Conteo de sesiones por dispositivo |
| `IP` | Última IP del dispositivo |
| `Pais` | Geolocalización |
| `Navegador` | Extraído de User-Agent |
| `SO` | Extraído de User-Agent |
| `ProveedorAuth` | Local / OAuth provider |
| Confiar / Desconfiar / Eliminar / Revocar / Bloquear | Acciones completas |

---

## ETAPA 7 — Dashboard IAM

### Qué Existe
| Sección | Estado |
|---------|--------|
| Stat Cards (Usuarios, Sesiones, Apps, Tenants) | ✅ Básico |
| Actividad Reciente (AuditoriaPwd) | ✅ Básico |
| Estado de Seguridad (Intentos excedidos, Password expirada) | ✅ Básico |
| Federación (Estadísticas, Últimas actividades) | ✅ Básico |

### Qué Falta
| Indicador | Descripción |
|-----------|-------------|
| Usuarios locales / OAuth / híbridos | Conteo por tipo |
| Usuarios sin MFA / con MFA | Seguridad |
| Usuarios bloqueados / suspendidos / eliminados | Estados |
| Logins por proveedor | Google, GitHub, LinkedIn, Facebook, Instagram |
| Errores OAuth / Login | Monitoreo |
| Top IP / Navegadores / Dispositivos / SO / Países / Tenants | Analytics |
| Auto Provisioning / Auto Link | Conteo |
| Password Local agregadas / eliminadas | Cambios |
| Revocaciones | Conteo |
| Sesiones activas / Dispositivos confiables | Estado |
| Gráficos MudChart | Visualización |

---

## ETAPA 8 — Dashboard Operacional

### Qué Existe
Nada — completamente nuevo.

### Qué Falta
| Indicador | Descripción |
|-----------|-------------|
| Emails enviados / fallidos | Monitoreo email |
| Tiempo promedio login / OAuth / JWT / MFA / Reset / AutoProvision / AutoLink | Performance |
| Errores SQL / OAuth / SMTP / MFA / JWT | Error tracking |
| Health Status | Sistema |
| Background Services | Estado de servicios |

---

## ETAPA 9 — Políticas por Proveedor

### Qué Existe en ConfProvIden
```
ClientId, ClientSecret, Scopes, Callback, RedirectUri, RolDefecto,
GuardarTokens, PermitirAutoLink, AutoProvisionar, RequiereMFALocal,
Estado, Metadata, Activo
```

### Qué Falta
| Campo | Tipo | Descripción |
|-------|------|-------------|
| `PermitirLogin` | `bool` | Si el proveedor puede usarse para login |
| `PermitirCrearUsuario` | `bool` | Si puede crear usuarios nuevos |
| `PermitirVincular` | `bool` | Si puede vincular a usuarios existentes |
| `PermitirDesvincular` | `bool` | Si puede desvincular |
| `PermitirPasswordLocal` | `bool` | Si permite agregar password local |
| `ObligaMFA` | `bool` | Si obliga MFA para este proveedor |
| `PermitirCambioEmail` | `bool` | Si sincroniza email |
| `PermitirCambioNombre` | `bool` | Si sincroniza nombre |
| `PermitirSincronizarAvatar` | `bool` | Si sincroniza avatar |
| `PermitirSincronizarPerfil` | `bool` | Si sincroniza perfil completo |
| `Prioridad` | `int` | Orden de prioridad |
| `OrdenVisual` | `int` | Orden en UI |
| `Logo` | `string?` | Logo URL |
| `Color` | `string?` | Color hex |
| `Tooltip` | `string?` | Texto tooltip |
| `Descripcion` | `string?` | Descripción del proveedor |

---

## ETAPA 10 — Sincronización de Perfil

### Qué Existe
Nada — completamente nuevo.

### Opciones de Sincronización
| Campo | Descripción |
|-------|-------------|
| `FrecuenciaSync` | Nunca / SoloPrimerLogin / Siempre |
| `SincronizarNombre` | bool |
| `SincronizarApellido` | bool |
| `SincronizarEmail` | bool |
| `SincronizarAvatar` | bool |
| `SincronizarIdioma` | bool |
| `SincronizarZonaHoraria` | bool |
| `SincronizarPais` | bool |
| `SincronizarEmpresa` | bool |
| `SincronizarCargo` | bool |

---

## ETAPA 11 — Gestión de Consentimiento OAuth

### Qué Existe
Nada — completamente nuevo.

### Tabla Necesaria: `ConsentimientoOAuth`
| Campo | Tipo | Descripción |
|-------|------|-------------|
| Id | long | PK |
| IdUsuario | int | FK |
| IdTenant | int | FK |
| IdProvIden | int | FK |
| Scopes | string | Scopes autorizados |
| FecConsentimiento | DateTime | Cuándo dio consentimiento |
| Version | string | Versión de la política |
| Activo | bool | Si está activo |
| FecRevocacion | DateTime? | Cuándo se revocó |

---

## ETAPA 12 — Mejorar Auditoría

### Qué Existe en AuditoriaIdentidadExterna
```
Id, IdTenant, IdProvIden, IdUsuario, SubExterno, Evento,
Resultado, Detalle, IP, UserAgent, CorrelationId, FecEvento
```

### Qué Falta
| Campo | Tipo | Descripción |
|-------|------|-------------|
| `TraceId` | `string?` | Distributed tracing |
| `SessionId` | `Guid?` | Sesión activa |
| `RefreshTokenId` | `string?` | Token used |
| `JwtId` | `string?` | JWT ID |
| `HttpStatus` | `int?` | HTTP status code |
| `TiempoRespuesta` | `int?` | ms |
| `Scopes` | `string?` | Scopes solicitados |
| `MetodoAutenticacion` | `string?` | Local/OAuth |
| `TipoLogin` | `string?` | Normal/MFA/Refresh |
| `Origen` | `string?` | API/UI/Mobile |
| `Destino` | `string?` | Redirect URL |
| `Codigo` | `string?` | Error code |
| `Excepcion` | `string?` | Exception message |
| `StackResumido` | `string?` | Stack trace summary |
| `IdDevice` | `int?` | FK Disp |
| `Browser` | `string?` | Browser name |
| `OS` | `string?` | Operating system |

---

## ETAPA 13 — Administración de Usuario

### Qué Existe
| Tab | Archivo | Estado |
|-----|---------|--------|
| General | `UsuarioGeneral.razor` | ✅ Completo |
| Accesos | `UsuarioAccesos.razor` | ✅ Completo |
| Sesiones | `UsuarioSesiones.razor` | ✅ Básico |
| Hist. Password | `UsuarioHistorialPwd.razor` | ✅ Completo |
| MFA | `UsuarioMFA.razor` | ✅ Completo |
| Disp. Confiables | `UsuarioDispConfiables.razor` | ✅ Básico |
| Bloqueos | `UsuarioBloqueos.razor` | ✅ Completo |
| Intentos | `UsuarioIntentosAcceso.razor` | ✅ Completo |
| Auditoría | `UsuarioAuditoria.razor` | ✅ Completo |
| Notificaciones | `UsuarioNotificaciones.razor` | ✅ Completo |
| Password | `UsuarioPassword.razor` | ✅ Completo |
| Permisos Efectivos | `UsuarioPermisosEfectivos.razor` | ✅ Completo |

### Qué Falta
| Tab | Descripción |
|-----|-------------|
| **Identidades** | Locales + Externas, Principal, Password Local, MFA |
| Acciones | Agregar/Eliminar Password Local, Cambiar principal, Agregar/Eliminar/Revocar proveedor, Forzar MFA, Forzar Logout, Revocar Sesiones/Dispositivos |

---

## ETAPA 14 — Templates Email

### Qué Existe (31 EmailJobKind)
```
PasswordReset, MfaCode, Welcome, SecurityAlert, AccountLocked,
PasswordChanged, UserActivated, UserDeactivated, UserUnblocked,
PasswordExpired, FirstLogin, MfaEnabled, MfaDisabled, NewDevice,
NewIp, DeviceRevoked, RoleAssigned, RoleRemoved, TenantCreated,
TenantSuspended, TenantReactivated, AppRegistered, ExternalLogin,
ExternalIdentityLinked, ExternalIdentityUnlinked, ProviderAdded,
ProviderRemoved, AuthError, ProviderPrincipalChanged,
PasswordLocalAdded, PasswordLocalRemoved
```

### Qué Falta
| Template | Descripción |
|----------|-------------|
| `identity-principal-changed` | Cambio de proveedor principal |
| `identity-linked-by-admin` | Admin vinculó proveedor |
| `identity-removed-by-admin` | Admin eliminó proveedor |
| `provider-disabled` | Proveedor deshabilitado |
| `provider-enabled` | Proveedor habilitado |
| `provider-authorization-revoked` | Autorización revocada |
| `provider-authorization-granted` | Autorización concedida |
| `oauth-consent-expired` | Consentimiento expirado |
| `session-revoked` | Sesión revocada |
| `security-notification` | Notificación de seguridad genérica |

---

## ETAPA 15 — Login

### Qué Existe
- Login.razor con provider buttons (icons-only, MudTooltip)
- Providers cargados desde `GET /api/auth/externo/proveedores`
- Filtrado por `ConfProvIden.Activa`
- 5 proveedores: Google, GitHub, LinkedIn, Instagram, Facebook

### Estado: ✅ Funcional — Verificar orden y que no esté hardcodeado

---

## ETAPA 16 — UX

### Componentes Reutilizables Existentes
| Componente | Archivo | Estado |
|------------|---------|--------|
| `IamKpiCard` | `Shared/IamKpiCard.razor` | ✅ |
| `CrudToolbar` | `Shared/CrudToolbar.razor` | ✅ |
| `IamInspector` | `Shared/IamInspector.razor` | ✅ |
| `ConfirmDialog` | `Shared/ConfirmDialog.razor` | ✅ |
| `SinPermiso` | `Shared/SinPermiso.razor` | ✅ |

### Qué Falta
| Componente | Descripción |
|------------|-------------|
| `IamDashboardCard` | Card específica para dashboards IAM |
| `IamStatsCard` | Card de estadísticas con gráfico |

---

## ETAPA 17 — Playwright

### Qué Existe
71 tests en 4 suites:
- fase12-federacion-ui.spec.ts (25)
- fase13-usuario-sin-email.spec.ts (22)
- fase14-federacion-identidades.spec.ts (14)
- fase15-hybrid-user.spec.ts (10)

### Tests Nuevos Necesarios
~20+ tests para nuevas funcionalidades de FASE 16.

---

## ETAPA 18 — Optimización

### Análisis Preliminar
| Área | Estado | Notas |
|------|--------|-------|
| Índices | ⚠️ | Revisar IX filtrados faltantes |
| FK | ✅ | Todas las FK configuradas |
| SP | ✅ | SPs para operaciones multi-tabla |
| N+1 | ⚠️ | Dashboard carga colecciones completas |
| LINQ | ⚠️ | Algunas consultas en memoria |
| Cache | ⚠️ | Solo OAuthSessionStore y UsedCodeStore |
| Repository | ✅ | Patrón correcto |
| UoW | ✅ | SaveChangesAsync desde controller |

---

## ETAPA 19 — Calidad

### Análisis Preliminar
| Área | Estado | Notas |
|------|--------|-------|
| Build | ✅ | 0 errores, 4 warnings pre-existing (NU1903) |
| Analyzers | ⚠️ | No configurados explícitamente |
| Dead Code | ⚠️ | PasswordLocalRemoved never triggered |
| Duplicación | ⚠️ | HashSHA256 duplicado en ExternalAuthService y AuthService |
| Arquitectura | ✅ | Clean Architecture respetada |
| Dependencias circulares | ✅ | No detectadas |

---

## ETAPA 20 — Documentación

Se generará al finalizar todas las etapas.

---

## Resumen: Qué Crear vs Qué Reutilizar

### Reutilizar (existentes)
- Todas las entidades base (Usuario, Sesion, Disp, ConfProvIden, ProvIden)
- Todos los repositories y services existentes
- Todos los controllers existentes
- Componentes Blazor (IamKpiCard, CrudToolbar, IamInspector)
- EmailQueue y PassPlatEmailService
- Login.razor (solo verificar)

### Crear (nuevos)
| Etapa | Componentes |
|-------|-------------|
| 2 | Campos en IdentidadesExterna, acciones controller, UI management |
| 3 | EEstadoIdentidadExterna enum, EstadoIdentidadExterna entity, migration |
| 4 | HistorialIdentidadExterna entity, repository, service, controller |
| 5 | Campos en Sesion, acciones avanzadas, UI mejorada |
| 6 | Página DispManager.razor, campos en Disp, acciones |
| 7 | DashboardIAM.razor con 30+ indicadores + MudChart |
| 8 | DashboardOperacional.razor con métricas de performance |
| 9 | Campos en ConfProvIden, ConfProvIdenDialog actualizado |
| 10 | Servicio de sincronización de perfil |
| 11 | ConsentimientoOAuth entity, repository, service, controller |
| 12 | Campos en AuditoriaIdentidadExterna, migration |
| 13 | Tab Identidades en UsuarioDetail.razor, acciones |
| 14 | ~10 nuevos EmailJobKind + templates |
| 17 | ~20+ nuevos tests Playwright |

### Requiere Refactorización
| Componente | Cambio |
|------------|--------|
| ConfProvIdenDialog | Agregar 12+ campos de política |
| IdentidadesExternas/Index.razor | Full management UI |
| Dashboard.razor | Sección IAM específica |
| Sesiones/Index.razor | Proveedor auth, revoke actions |
| DispConfiables/Index.razor | Full device management |

---

## Score Preliminar por Etapa

| Etapa | Complejidad | Esfuerzo Estimado |
|-------|-------------|-------------------|
| 2 | Alta | 3-4h |
| 3 | Media | 1-2h |
| 4 | Media | 2-3h |
| 5 | Media | 2-3h |
| 6 | Alta | 3-4h |
| 7 | Alta | 4-5h |
| 8 | Alta | 3-4h |
| 9 | Media | 2-3h |
| 10 | Media | 2-3h |
| 11 | Media | 2-3h |
| 12 | Media | 1-2h |
| 13 | Alta | 3-4h |
| 14 | Baja | 1-2h |
| 15 | Baja | 0.5h (verificación) |
| 16 | Baja | 1h |
| 17 | Alta | 3-4h |
| 18 | Media | 1-2h |
| 19 | Baja | 1h |
| 20 | Media | 2-3h |
| **Total** | | **~35-45h** |
