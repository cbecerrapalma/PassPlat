# PassPlat Dashboard — Diseño Final

> **Documento generado**: 28-Jun-2026
> **Propósito**: Auditoría funcional + Arquitectura + UX/UI para el rediseño del Dashboard como Centro de Operaciones (Operations Center)
> **Alcance**: 15 fases completas de análisis

---

## Tabla de Contenidos

1. [Análisis del Dashboard Actual](#1-análisis-del-dashboard-actual)
2. [Problemas Encontrados](#2-problemas-encontrados)
3. [Oportunidades de Mejora](#3-oportunidades-de-mejora)
4. [KPIs Recomendados](#4-kpis-recomendados)
5. [Widgets Recomendados](#5-widgets-recomendados)
6. [Gráficos Recomendados](#6-gráficos-recomendados)
7. [Alertas Recomendadas](#7-alertas-recomendadas)
8. [Información Operacional](#8-información-operacional)
9. [Información de Seguridad](#9-información-de-seguridad)
10. [Componentes Reutilizables](#10-componentes-reutilizables)
11. [Estrategia Responsive](#11-estrategia-responsive)
12. [Estrategia de Performance](#12-estrategia-de-performance)
13. [Estrategia de Cache](#13-estrategia-de-cache)
14. [Arquitectura Recomendada](#14-arquitectura-recomendada)
15. [Roadmap de Implementación](#15-roadmap-de-implementación)
16. [Priorización de Funcionalidades](#16-priorización-de-funcionalidades)
17. [Riesgos](#17-riesgos)
18. [Recomendaciones UX](#18-recomendaciones-ux)
19. [Recomendaciones UI](#19-recomendaciones-ui)
20. [Score del Dashboard Actual](#20-score-del-dashboard-actual)
21. [Apéndice: Mapa Funcional](#21-apéndice-mapa-funcional)
22. [Apéndice: Mapa de Dominios](#22-apéndice-mapa-de-dominios)

## 1. Análisis del Dashboard Actual

### Estado Actual

El Dashboard actual (PassPlat.Web/Pages/Dashboard.razor) es una página Blazor WASM de 422 líneas que presenta:

- **4 stat cards**: Usuarios registrados, Sesiones activas, Aplicaciones, Inquilinos activos
- **Activity Feed**: Timeline de auditoría reciente (tarjeta 7/12 columnas)
- **Security Status**: 2 alertas (intentos excedidos + contraseñas expiradas) + overall status
- **Quick Access**: 4 botones de navegación (Usuarios, Roles, Inquilinos, Sesiones)
- **Monitoring**: 4 botones de navegación (Bloqueos, Auditoría, Intentos, Notificaciones)

### Carga de datos

| Llamada | Endpoint | Propósito |
|---------|----------|-----------|
| 1 | GET api/Usuarios/count | Total usuarios |
| 2 | GET api/Sesiones/contar-tenant | Sesiones activas |
| 3 | GET api/Apps/count | Total aplicaciones |
| 4 | GET api/Tenants/count | Total tenants |
| 5 | GET api/AuditoriaPwd/tenant/{id} | Actividad reciente |
| 6 | GET api/Usuarios/con-intentos-excedidos | Alertas de intentos |
| 7 | GET api/Usuarios/con-password-expirada | Alertas de passwords |

### Componentes existentes reutilizables

| Componente | Propósito |
|------------|-----------|
| IamKpiCard.razor | KPI card genérica con avatar, valor, label, subtexto |
| IamInspector.razor | Panel lateral de detalle con loading, close, actions |
| IamPermissionBadge.razor | Badge de permiso |
| CrudToolbar.razor | Toolbar con búsqueda, filtro, refresh, crear |
| CrudActionsColumn.razor | Columna de acciones para tablas |
| CrudDialog.razor | Dialog genérico CRUD |
| InspectorField.razor | Field de detalle para inspector |
| ConfirmDialog.razor | Confirmación de eliminación |
| PasswordStrength.razor | Indicador de fortaleza de contraseña |
| SinPermiso.razor | Página de acceso denegado |
| RedirectToLogin.razor | Redirección a login |

---

## 2. Problemas Encontrados

### UX

| # | Problema | Severidad | Impacto |
|---|----------|-----------|---------|
| 1 | Sin visibilidad de estado general: 4 stat cards no responden qué requiere atención | Alta | El dashboard no funciona como centro de operaciones |
| 2 | Alertas limitadas: Solo 2 indicadores. Sin MFA, Email, Tenants, Apps | Alta | Riesgos pasan desapercibidos |
| 3 | Quick Access duplicado con NavMenu | Media | Espacio desperdiciado |
| 4 | Sin datos de Email: No hay visibilidad del pipeline de correos | Alta | No se sabe si los emails se están enviando |
| 5 | Sin datos de MFA: usuarios sin MFA, métodos, adopción | Alta | No se puede medir postura de seguridad |
| 6 | Activity Feed genérico: sin filtrar por criticidad | Media | Demasiado ruido, poca señal |
| 7 | Sin contexto de tenant/app | Media | El operador no sabe en qué contexto está |
| 8 | Sin estado de servicios (health checks, background) | Alta | Problemas de infraestructura pasan desapercibidos |
| 9 | Sin loading skeletons | Baja | Percepción de lentitud |
| 10 | Sin acción rápida de búsqueda | Media | El operador debe navegar a otra página |

### UI

| # | Problema | Severidad | Impacto |
|---|----------|-----------|---------|
| 11 | Diseño plano sin jerarquía visual | Media | Difícil escanear información |
| 12 | Sin gráficos: 0 gráficos. Solo números y texto | Alta | No hay tendencias, comparaciones ni distribuciones |
| 13 | Sin sparklines: no se ve evolución de KPIs | Media | No se identifica tendencia |
| 14 | Espacio desaprovechado: 4 stat cards ocupan todo el ancho | Baja | Baja densidad de datos |
| 15 | Sin personalización: mismos widgets para todos | Media | El dashboard es el mismo para todos los roles |

### Navegación

| # | Problema | Severidad | Impacto |
|---|----------|-----------|---------|
| 16 | Sin breadcrumb contextual: Solo muestra "Dashboard" | Baja | Poco contexto |
| 17 | Sin enlaces a detalle desde KPIs | Media | El operador tiene que ir al menú |
| 18 | Sin selector de tenant/app en dashboard | Alta | Usuarios multi-tenant no pueden operar |

---

## 3. Oportunidades de Mejora

### Oportunidades Estratégicas

1. **Convertir en Centro de Operaciones (Operations Center)**: Que responda 6 preguntas clave:
   - ¿Qué requiere atención inmediata?
   - ¿Existe algún riesgo de seguridad activo?
   - ¿Hay usuarios bloqueados o con problemas?
   - ¿Qué está ocurriendo con los correos y el MFA?
   - ¿Qué tenants o aplicaciones presentan incidencias?
   - ¿Cuál es el estado general de la plataforma?

2. **Dashboard por rol**: SuperAdmin, Admin Tenant, Admin App, Operador, Auditor
3. **Widgets arrastrables y configurables**: Cada usuario personaliza su dashboard
4. **Exportación de datos**: KPIs exportables a CSV/PDF
5. **Notificaciones en tiempo real**: SignalR para alertas sin recargar

### Oportunidades Técnicas

6. Consultas agregadas con SPs dedicados para KPIs
7. Cache inteligente: IMemoryCache + Redis
8. Background services para KPIs pesados
9. Endpoint único GET /api/dashboard/kpi

---

## 4. KPIs Recomendados

### 4.1 Identidad y Usuarios

| KPI | Fórmula | Frecuencia | Prioridad |
|-----|---------|------------|-----------|
| Total usuarios activos | COUNT WHERE IdEstado = Activo | 5 min | Alta |
| Usuarios bloqueados | COUNT WHERE Bloqueos.Activo = true | Tiempo real | Crítica |
| Usuarios inactivos (>90d sin login) | COUNT WHERE FecUltAcceso < NOW - 90d | Diario | Media |
| Usuarios sin email | COUNT WHERE Email IS NULL | Diario | Alta |
| Usuarios con MFA habilitado | COUNT WHERE MFA.IdEstado = Activo | 5 min | Alta |
| Usuarios sin MFA | Total - Con MFA | 5 min | Alta |
| Tasa adopción MFA | Con MFA / Total * 100 | 5 min | Alta |
| Contraseñas próximas a expirar (<7d) | COUNT WHERE FecUltCambio + DiasVigencia - NOW < 7d | Diario | Alta |
| Contraseñas expiradas | COUNT WHERE FecUltCambio + DiasVigencia < NOW | Diario | Crítica |
| Contraseñas reutilizadas | COUNT WHERE HistorialPwd hash repetido | Diario | Media |

### 4.2 Seguridad

| KPI | Fórmula | Frecuencia | Prioridad |
|-----|---------|------------|-----------|
| Intentos fallidos (última hora) | COUNT IntentosAcceso WHERE Exitoso = false AND FecIntento > NOW - 1h | Tiempo real | Crítica |
| Tasa de éxito login | Exitosos / Total * 100 | 5 min | Alta |
| IPs bloqueadas por rate limiting | COUNT IPs bloqueadas | Tiempo real | Alta |
| Dispositivos nuevos no confiables (24h) | COUNT DispConfiables WHERE FecAlta > NOW - 24h | Tiempo real | Alta |
| Bloqueos activos por tipo | COUNT Bloqueos WHERE Activo GROUP BY TipoBloqueo | Tiempo real | Crítica |
| Alertas de seguridad activas | Alertas no resueltas | Tiempo real | Crítica |
| Sesiones activas por app | COUNT Sesiones WHERE EsActiva GROUP BY IdApp | 5 min | Media |

### 4.3 Email y Comunicaciones

| KPI | Fórmula | Frecuencia | Prioridad |
|-----|---------|------------|-----------|
| Emails enviados (últimas 24h) | COUNT EmailLog WHERE FecEnvio > NOW - 24h | 5 min | Alta |
| Emails fallidos | COUNT WHERE Estado = 'fallido' | Tiempo real | Crítica |
| Emails en cola (pendientes) | COUNT WHERE Estado = 'pendiente' | Tiempo real | Alta |
| Tasa de entrega exitosa | Enviados / Total * 100 | 5 min | Alta |
| Templates más usados | COUNT GROUP BY IdTemplate | Diario | Baja |

### 4.4 Tenants y Aplicaciones

| KPI | Fórmula | Frecuencia | Prioridad |
|-----|---------|------------|-----------|
| Tenants activos / totales | COUNT WHERE Activo / COUNT * 100 | 5 min | Alta |
| Tenants suspendidos | COUNT WHERE Activo = false | 5 min | Alta |
| Apps sin email account configurado | Apps sin AppEmailAccounts | Diario | Alta |
| Distribución usuarios por tenant | COUNT Usuarios GROUP BY IdTenant | Diario | Media |

### 4.5 Operacional

| KPI | Fórmula | Frecuencia | Prioridad |
|-----|---------|------------|-----------|
| Tiempo promedio autenticación | AVG(TpoRespuesta) WHERE Exitoso = true | 5 min | Alta |
| P99 autenticación | PERCENTILE 99 TpoRespuesta | 5 min | Alta |
| Roles más asignados | COUNT Accesos GROUP BY IdRol | Diario | Media |

### 4.6 Auditoría

| KPI | Fórmula | Frecuencia | Prioridad |
|-----|---------|------------|-----------|
| Eventos auditoría (últimas 24h) | COUNT AuditoriaPwd WHERE FecAccion > NOW - 24h | 5 min | Alta |
| Eventos críticos (nivel >= 7) | COUNT WHERE NivelRiesgo >= 7 | Tiempo real | Crítica |
| Cambios de contraseña (última hora) | COUNT WHERE IdTipoAccion = CambioPassword | Tiempo real | Media |

---

## 5. Widgets Recomendados

### 5.1 KPI Cards

| Widget | Descripción | Datos | Frecuencia |
|--------|-------------|-------|------------|
| KpiCard | Valor + label + ícono + color | Count | 5 min |
| KpiTrendCard | KpiCard + sparkline + % cambio | Count + serie temporal | 5 min |
| KpiAlertCard | KpiCard + umbral (verde/amarillo/rojo) | Count + threshold | Tiempo real |
| KpiComparisonCard | Compara dos períodos | 2 counts + diff % | Diario |

### 5.2 Activity Widgets

| Widget | Descripción | Datos | Frecuencia |
|--------|-------------|-------|------------|
| ActivityTimeline | Timeline cronológico eventos recientes | AuditoriaPwd (últimos 20) | Tiempo real |
| SecurityFeed | Solo eventos seguridad | IntentosAcceso + Bloqueos + MFA | Tiempo real |
| AuditFeed | Auditoría con filtro por tipo y riesgo | AuditoriaPwd | 5 min |
| RecentChanges | Cambios recientes config sensibles | ConfigApp, PoliticasPwd | 5 min |

### 5.3 Security Widgets

| Widget | Descripción | Datos | Frecuencia |
|--------|-------------|-------|------------|
| SecurityStatus | Indicador general verde/amarillo/rojo | Múltiples KPIs | Tiempo real |
| ActiveBlocks | Lista bloqueos activos con tiempo restante | Bloqueos WHERE Activo | Tiempo real |
| FailedLogins | Barras intentos fallidos por hora | IntentosAcceso (24h) | 5 min |
| MfaAdoption | Donut: usuarios con/sin MFA | Usuarios + MFA | 5 min |
| PasswordHealth | Distribución salud contraseñas | Usuarios + HistorialPwd | Diario |
| NewDevices | Dispositivos no confiables recientes | DispConfiables (24h) | Tiempo real |

### 5.4 Email Widgets

| Widget | Descripción | Datos | Frecuencia |
|--------|-------------|-------|------------|
| EmailQueue | Cola correos (pendientes, enviando, fallidos) | EmailLog | Tiempo real |
| EmailDeliveryRate | Timeline tasa de entrega (24h) | EmailLog | 5 min |
| EmailFailures | Errores de envío recientes | EmailLog WHERE fallido | Tiempo real |
| TemplateUsage | Barras: templates más usados (24h) | EmailLog GROUP BY IdTemplate | Diario |

### 5.5 System Widgets

| Widget | Descripción | Datos | Frecuencia |
|--------|-------------|-------|------------|
| SystemHealth | Salud (API, DB, SMTP, Cache) | Health checks | 30s |
| BackgroundServices | Estado servicios segundo plano | Estado interno | 30s |
| ApiPerformance | P99/P95/P50 endpoints críticos | TpoRespuesta | 5 min |
| DataRetention | Purga datos próximos | FecRetencion | Diario |

### 5.6 Business Widgets

| Widget | Descripción | Datos | Frecuencia |
|--------|-------------|-------|------------|
| TopTenants | Top 5 tenants por actividad | Usuarios + IntentosAcceso | Diario |
| AppUsage | Uso por aplicación | Sesiones + IntentosAcceso | 5 min |
| RoleDistribution | Distribución roles asignados | Accesos GROUP BY IdRol | Diario |
| UserGrowth | Línea crecimiento usuarios (30d) | Usuarios GROUP BY FecCrea | Diario |

---

## 6. Gráficos Recomendados

### 6.1 Líneas (Trends)

| Gráfico | Widget | Justificación |
|---------|--------|---------------|
| Intentos fallidos por hora (24h) | FailedLogins | Identificar patrones de ataque |
| Usuarios nuevos por día (30d) | UserGrowth | Medir crecimiento |
| Tasa éxito login por hora | LoginSuccessRate | Detectar problemas autenticación |
| Emails enviados vs fallidos por hora | EmailDeliveryRate | Monitorear salud pipeline email |
| Sesiones activas por hora | ActiveSessions | Horas pico de uso |

### 6.2 Barras (Comparisons)

| Gráfico | Widget | Justificación |
|---------|--------|---------------|
| Intentos por aplicación | AppUsage | Comparar carga entre apps |
| Usuarios por tenant | TopTenants | Concentración de usuarios |
| Templates email más usados | TemplateUsage | Identificar más utilizados |
| Bloqueos por tipo | ActiveBlocks | Clasificar bloqueos |

### 6.3 Donut (Distributions)

| Gráfico | Widget | Justificación |
|---------|--------|---------------|
| Usuarios con/sin MFA | MfaAdoption | Medir adopción MFA |
| Estado de usuarios | UserStatus | Distribución base usuarios |
| Resultados intentos acceso | AccessResults | Salud proceso login |
| Estado de emails | EmailStatus | Salud pipeline email |

### 6.4 Treemap

| Gráfico | Widget | Justificación |
|---------|--------|---------------|
| Usuarios por tenant | TenantDistribution | Área = magnitud |

### 6.5 Heatmap

| Gráfico | Widget | Justificación |
|---------|--------|---------------|
| Intentos por hora + día | LoginHeatmap | Patrones temporales 2D |

### 6.6 Sparkline

| Ubicación | Datos | Justificación |
|-----------|-------|---------------|
| En cada KpiCard | Tendencia 24h/7d/30d | Contexto sin espacio extra |

---

## 7. Alertas Recomendadas

### 7.1 Críticas (Impacto inmediato)

| Alerta | Condición | Acción | Canal |
|--------|-----------|--------|-------|
| Ataque fuerza bruta | >10 fallidos en <5 min misma IP | Bloquear IP + notificar | Dash + Email + Notif |
| Cuenta comprometida | Login desde IP/ubicación desconocida + disp nuevo | Notificar + verificar | Dash + Email |
| Múltiples bloqueos | >5 bloqueos activos en <1h | Notificar admin seguridad | Dash + Email |
| Email caído | >5 fallidos consecutivos mismo account | Revisar SMTP | Dash + Notif |
| Token masivo | >50 tokens reset en <10 min | Posible enumeración | Dash + Email |

### 7.2 Altas (Atención en el día)

| Alerta | Condición | Acción |
|--------|-----------|--------|
| Contraseñas expiradas masivas | >10 usuarios con pwd expirada | Forzar cambio masivo |
| Tenant suspendido sin notificación | Activo=false sin evento auditoría | Revisar causa |
| App inactiva con usuarios | Activa=false con Accesos activos | Reasignar |
| SMTP caído | FecUltOk > 1h | Revisar health check |
| Adopción MFA baja | <30% usuarios activos con MFA | Campaña adopción |

### 7.3 Medias (Monitoreo)

| Alerta | Condición | Acción |
|--------|-----------|--------|
| Contraseñas próximas a expirar | >5 usuarios con <7 días | Recordatorio |
| Dispositivos nuevos | Disp no confiable registrado | Verificar |
| IPs nuevas | Login desde IP no registrada | Monitorear |
| Config seguridad modificada | Cambio PoliticasPwd/ConfigApp | Auditar |



---

## 8. Información Operacional

### Health Checks

| Componente | Indicador | Frecuencia |
|------------|-----------|------------|
| API Web | HTTP 200 en /health | 30s |
| DB SQL Server | Conexión exitosa | 30s |
| SMTP Server | Conexión host:port | 60s |
| Redis Cache | Ping exitoso | 30s |
| Background EmailJob | Última ejecución < 60s | 30s |
| Background PasswordExp | Última ejecución < 24h | 60s |

### Background Services

| Servicio | Estado | KPIs |
|----------|--------|------|
| EmailBackgroundService | Ejecutando/Detenido/Error | Cola, tasa, errores |
| PasswordExpirationBgService | Ejecutando/Detenido/Error | Última ejecución |
| Purge Maintenance | Programado/Ejecutando | Registros purgados |

### Email Queue

| Métrica | Descripción |
|---------|-------------|
| Cola actual | Emails pendientes |
| Tasa procesamiento | Emails/minuto |
| Intentos promedio | Reintentos por email fallido |

---

## 9. Información de Seguridad

### Postura General

| Indicador | Descripción |
|-----------|-------------|
| Security Score | Score compuesto (0-100): %MFA, %pwd vigentes, bloqueos, intentos |
| Riesgo general | Verde / Amarillo / Rojo |
| Incidentes activos | Alertas no resueltas |

### MFA Status

| Indicador | Descripción |
|-----------|-------------|
| Usuarios con MFA TOTP | Registrados TOTP activo |
| Usuarios con MFA Email | Registrados Email activo |
| Usuarios sin MFA | Sin ningún método |

### Password Health

| Indicador | Descripción |
|-----------|-------------|
| Contraseñas vigentes | Dentro de validez |
| Próximas a expirar | <7 días |
| Contraseñas expiradas | Fuera de vigencia |
| Usuarios con ReqCambioPwd | Pendientes cambio forzado |

### Access Intelligence

| Indicador | Descripción |
|-----------|-------------|
| Top 5 usuarios + intentos fallidos | Ranking riesgo |
| Top 5 IPs + intentos fallidos | IPs sospechosas |
| Horario mayor actividad | Hora pico login |
| Apps con más intentos fallidos | Apps objetivo |

---

## 10. Componentes Reutilizables

### DashboardLayout
```
DashboardLayout
├── DashboardBreadcrumb (tenant + app context)
├── DashboardHeader (título + fecha + selector tenant/app)
├── DashboardGrid (grilla para widgets)
└── DashboardToolbar (refresh + personalización + exportar)
```

### Widget System
```
WidgetContainer → Frame genérico con título, menú, loading, error, empty
├── WidgetHeader → Título + acciones
├── WidgetBody → Slot contenido
├── WidgetFooter → Slot acciones
├── WidgetLoading → MudSkeleton
├── WidgetEmpty → Estado sin datos
└── WidgetError → Error con reintento
```

### KPI Components
```
KpiCard → Valor + label + ícono + color
├── KpiTrendCard → KpiCard + sparkline
├── KpiAlertCard → KpiCard + umbral
├── KpiComparisonCard → KpiCard + diff %
└── KpiClickableCard → KpiCard con navegación
```

### Security Components
```
SecurityBadge → Badge nivel riesgo
SecurityAlert → Alerta expandible
SecurityChecklist → Lista chequeo seguridad
BlockList → Bloqueos activos
MfaStatus → Badge estado MFA
PasswordHealthIndicator → Barra salud contraseña
```

### Chart Components
```
LineChart / BarChart / DonutChart
TreemapChart / HeatmapChart
Sparkline / TimelineChart
```

### Data & Feedback
```
StatCardGrid / ActivityFeed / FilterBar / KpiTable / DataExport
LoadingOverlay / ErrorBoundary / EmptyState / ToastNotification
```

---

## 11. Estrategia Responsive

### Desktop (>=1280px) — 3 columnas
`
┌────────────────────────────────────────────────────────────────┐
│ Header: Breadcrumb | Título + Fecha | Selector Tenant/App     │
├────────────────────────────────────────────────────────────────┤
│ Stats Row: 6 KpiCards en 1 fila (md=2)                        │
├──────────────────────┬──────────────────────┬──────────────────┤
│ Security Widgets     │ Activity Feed        │ Email Status     │
├──────────────────────┴──────────────────────┴──────────────────┤
│ Gráficos: LoginTrends | MfaAdoption | FailedAttempts           │
├──────────────────────┬──────────────────────┬──────────────────┤
│ System Health        │ Top Tenants          │ Quick Actions    │
└──────────────────────┴──────────────────────┴──────────────────┘
`

### Notebook (960-1279px) — 2 columnas
`
┌────────────────────────────────────────────────────────────────┐
│ Header: Breadcrumb | Título | Selector Tenant/App             │
├────────────────────────────────────────────────────────────────┤
│ Stats Row: 6 KpiCards en 2 filas                              │
├─────────────────────────────┬─────────────────────────────────┤
│ Security (apilados)         │ Activity Feed (scrolleable)     │
├─────────────────────────────┼─────────────────────────────────┤
│ Email + Health              │ LoginTrends + MfaAdoption       │
├─────────────────────────────┴─────────────────────────────────┤
│ Top Tenants | Quick Actions | FailedAttempts                  │
└──────────────────────────────────────────────────────────────┘
`

### Tablet (600-959px) — 1 columna
`
┌────────────────────────────────────────────────────────────────┐
│ Header: Menú hamburguesa | Título | Selector Tenant            │
├────────────────────────────────────────────────────────────────┤
│ Stats: 6 KpiCards en 3 filas (xs=6)                            │
├────────────────────────────────────────────────────────────────┤
│ Activity Feed (scrolleable, altura limitada)                   │
├────────────────────────────────────────────────────────────────┤
│ Security Status + Block List (accordion)                       │
├────────────────────────────────────────────────────────────────┤
│ LoginTrends + MfaAdoption (apilados)                           │
├────────────────────────────────────────────────────────────────┤
│ Quick Actions | System Health                                  │
└────────────────────────────────────────────────────────────────┘
`

### Mobile (<600px) — 1 columna compacta
`
┌─────────────────┐
│ Header compacto  │
├─────────────────┤
│ Stats: 3 filas  │
│ xs=6 (2 cols)   │
├─────────────────┤
│ Activity Feed   │
│ (5 items max)   │
├─────────────────┤
│ Security Alerts  │
│ (collapse)       │
├─────────────────┤
│ Quick Actions   │
└─────────────────┘
`

### Reglas Responsive

| Breakpoint | Cols | Sidebar | KpiCards | Charts | Activity |
|------------|------|---------|----------|--------|----------|
| >=1280px | 3 | Expandido 260px | 6/fila | Todos | 15 items |
| 960-1279px | 2 | Compacto 60px | 3/fila | Apilados | 10 items |
| 600-959px | 1 | Offcanvas | 2/fila | Pequeños | 5 items |
| <600px | 1 | Offcanvas | 2/fila | Solo donut+barras | 3 items |

---

## 12. Estrategia de Performance

### Tiempo Real vs Cache vs Background

| Tipo | Componentes | Frecuencia | Estrategia |
|------|-------------|------------|------------|
| Tiempo real | Alertas, bloqueos, health | 30-60s | Polling + SignalR |
| Cache corto | Counts, KPIs seguridad | 5 min | IMemoryCache deslizante |
| Cache medio | Distribuciones, tendencias | 15 min | Redis absoluta |
| Background | Sparklines, heatmaps, 30d | 1h | BackgroundService |

### Optimizaciones de Consulta

| Consulta | Problema | Solución |
|----------|----------|----------|
| Counts varios | 7 llamadas separadas | Endpoint único /api/dashboard/kpi |
| SP KPIs | No existe | SP_Dashboard_KPI |
| Activity Feed | Filtro tenant | Index tenant + fecha desc |
| Passwords expiradas | Query LINQ pesado | Vista materializada |

### Polling

| Prioridad | Widgets | Intervalo | Técnica |
|-----------|---------|-----------|---------|
| 1 | Alertas, Health, Bloqueos | 30s | Polling + diff |
| 2 | Stats, Activity, Email | 60s | Polling + diff |
| 3 | Gráficos | 5 min | Perezoso |
| 4 | Distribuciones | 15 min | Perezoso |

---

## 13. Estrategia de Cache

### IMemoryCache (API)

| Clave | Duración | Invalidación |
|------|----------|--------------|
| dashboard:stats:{tenantId} | 5 min deslizante | CRUD usuarios/sesiones/apps/tenants |
| dashboard:alerts:{tenantId} | 2 min | Bloqueo, intento fallido, cambio pwd |

### Redis (Shared)

| Clave | Duración | Notas |
|------|----------|-------|
| dashboard:trends:{t}:{metric}:{periodo} | 15 min absoluta | Precarga BackgroundService |
| dashboard:health | 30s | Por health check |

### SignalR (Real-time)

| Evento | Trigger |
|--------|---------|
| AlertCritical | Nuevo bloqueo, ataque, email fallido |
| StatsUpdated | Cada 5 min o evento |
| HealthChanged | Cambio health check |
| EmailQueueUpdated | Nuevo email en cola |

---

## 14. Arquitectura Recomendada

### Capas

`
PassPlat.Web (Blazor WASM)
├── Dashboard Page (Operations Center)
│   ├── DashboardLayout.razor
│   ├── OperationsCenter.razor
│   ├── Widgets/ (Kpi, Security, Email, System, Activity)
│   ├── Charts/ (wrappers)
│   └── Services/DashboardStateService
└── ApiClient → HTTP →
PassPlat.WebAPI
├── DashboardController
│   GET /api/dashboard/kpi
│   GET /api/dashboard/alerts
│   GET /api/dashboard/activity
│   GET /api/dashboard/trends/{metric}
│   GET /api/dashboard/health
├── DashboardService (aplicación)
└── DashboardRepository (datos - SPs)
        ↓
SQL Server (SP_Dashboard_KPI, SP_Dashboard_Alertas, etc.) + Redis
`

### DashboardController API

`
GET /api/dashboard/kpi?tenantId={id}
→ { totalUsuarios, activos, bloqueados, sinEmail, conMFA, sinMFA,
    tasaAdopcionMFA, totalSesiones, totalApps, totalTenants,
    passwordsProximas, passwordsExpiradas, emailsPendientes,
    emailsFallidos, emailsEnviados24h, intentosFallidos1h,
    tasaExitoLogin, bloquesActivos, securityScore }

GET /api/dashboard/alerts?tenantId={id}
→ [{ tipo, severidad, mensaje, recurso, fecDeteccion }]

GET /api/dashboard/activity?tenantId={id}&limit=20
→ [{ tipo, usuario, riesgo, fec, detalles }]

GET /api/dashboard/trends/{metric}?tenantId={id}&periodo=24h|7d|30d
→ [{ fec, valor }]

GET /api/dashboard/health
→ { api, db, smtp, redis, emailBg, pwdExpBg }
`

### Stored Procedures Propuestos

| SP | Propósito | Retorno |
|----|-----------|---------|
| SP_Dashboard_KPI | Todos los KPIs | Una fila con 20+ columnas |
| SP_Dashboard_Alertas | Alertas activas | Lista |
| SP_Dashboard_Actividad | Actividad reciente | Lista paginada |
| SP_Dashboard_Tendencias | Serie temporal sparklines | Lista (fecha, valor) |
| SP_Dashboard_SecurityScore | Puntuación seguridad | Score + desglose |



---

## 15. Roadmap de Implementación

### Fase 1: Foundation (Semana 1)

| Tarea | Duración |
|-------|----------|
| DashboardController + /api/dashboard/kpi | 1 día |
| DashboardService (aplicación) | 1 día |
| DashboardRepository + SP RawQuery | 2 días |
| SP_Dashboard_KPI en SQL | 1 día |
| **Total** | **3-4 días** |

### Fase 2: Componentes Core (Semana 2)

| Tarea | Duración |
|-------|----------|
| WidgetContainer.razor genérico | 1 día |
| KpiCard + KpiTrendCard + KpiAlertCard | 1 día |
| SecurityBadge + SecurityAlert + SecurityChecklist | 1 día |
| ActivityFeed timeline | 1 día |
| StatCardGrid responsive | 0.5 día |
| **Total** | **3-4 días** |

### Fase 3: Dashboard Principal (Semana 3) — MVP

| Tarea | Duración |
|-------|----------|
| Rediseño Dashboard.razor con nuevo layout | 2 días |
| DashboardStateService con polling | 1 día |
| Selector tenant/app en header | 1 día |
| Loading skeletons + estados vacío/error | 0.5 día |
| **Total** | **3-4 días** |

### Fase 4: Widgets Seguridad (Semana 4)

| Tarea | Duración |
|-------|----------|
| ActiveBlocksWidget | 1 día |
| FailedLoginsChart (barras) | 1 día |
| MfaAdoptionWidget (donut) | 1 día |
| PasswordHealthWidget (barras) | 1 día |
| **Total** | **3-4 días** |

### Fase 5: Widgets Email (Semana 5)

| Tarea | Duración |
|-------|----------|
| EmailQueueWidget | 1 día |
| EmailDeliveryChart (línea) | 1 día |
| EmailFailuresList | 1 día |
| **Total** | **2-3 días** |

### Fase 6: Widgets Sistema (Semana 6)

| Tarea | Duración |
|-------|----------|
| SystemHealthWidget | 1 día |
| BackgroundServicesWidget | 1 día |
| ApiPerformanceWidget | 1 día |
| **Total** | **2-3 días** |

### Fase 7: Gráficos Avanzados (Semana 7)

| Tarea | Duración |
|-------|----------|
| UserGrowthChart (línea 30d) | 1 día |
| LoginHeatmap (calor hora/día) | 1.5 días |
| AppUsageChart (barras) | 1 día |
| TopTenantsTreemap | 1.5 días |
| **Total** | **3-4 días** |

### Fase 8: Personalización (Semana 8)

| Tarea | Duración |
|-------|----------|
| Widgets configurables (guardar layout por usuario) | 2 días |
| Dashboard por rol (SuperAdmin, Admin, Operador, Auditor) | 1 día |
| Selector de período en gráficos | 1 día |
| **Total** | **3-4 días** |

### Fase 9: SignalR Tiempo Real (Semana 9)

| Tarea | Duración |
|-------|----------|
| Hub SignalR en WebAPI | 1 día |
| Cliente SignalR en Blazor (DashboardStateService) | 1 día |
| Eventos de alerta en tiempo real | 1 día |
| **Total** | **2-3 días** |

### Fase 10: Performance y Cache (Semana 10)

| Tarea | Duración |
|-------|----------|
| Redis cache integration | 1 día |
| BackgroundService precarga de tendencias | 1 día |
| Optimización de SP_Dashboard_* | 1 día |
| **Total** | **2-3 días** |

---

## 16. Priorización de Funcionalidades

### MVP (Semanas 1-3) — Funcionalidad Crítica

| # | Funcionalidad | Esfuerzo | Impacto |
|---|---------------|----------|---------|
| 1 | Endpoint único /api/dashboard/kpi | 3 días | 🔴 |
| 2 | DashboardService + Repository | 2 días | 🔴 |
| 3 | SP_Dashboard_KPI | 1 día | 🔴 |
| 4 | KpiCard + KpiTrendCard + StatCardGrid | 2 días | 🔴 |
| 5 | SecurityStatus + ActiveBlocks | 2 días | 🔴 |
| 6 | ActivityFeed con niveles de riesgo | 1 día | 🔴 |
| 7 | Loading skeletons + estados vacío/error | 0.5 día | 🟡 |

### Fase 2 (Semanas 4-6) — Funcionalidad Alta

| # | Funcionalidad | Esfuerzo | Impacto |
|---|---------------|----------|---------|
| 8 | MfaAdoptionWidget (donut) | 1 día | 🔴 |
| 9 | FailedLoginsChart (barras) | 1 día | 🔴 |
| 10 | PasswordHealthWidget | 1 día | 🔴 |
| 11 | EmailQueueWidget + EmailFailures | 2 días | 🔴 |
| 12 | SystemHealthWidget | 1 día | 🟡 |
| 13 | Selector tenant/app en header | 1 día | 🟡 |

### Fase 3 (Semanas 7-10) — Funcionalidad Media

| # | Funcionalidad | Esfuerzo | Impacto |
|---|---------------|----------|---------|
| 14 | UserGrowthChart + LoginHeatmap | 2.5 días | 🟡 |
| 15 | AppUsageChart + TopTenantsTreemap | 2.5 días | 🟡 |
| 16 | Widgets configurables | 2 días | 🟢 |
| 17 | Dashboard por rol | 1 día | 🟢 |
| 18 | SignalR tiempo real | 3 días | 🟢 |
| 19 | Redis cache | 2 días | 🟢 |

---

## 17. Riesgos

### Técnicos

| Riesgo | Prob. | Impacto | Mitigación |
|--------|-------|---------|------------|
| Performance SP_Dashboard_KPI multi-tabla | Media | Alto | Indexar, NOLOCK, retention |
| Sobrecarga polling 30 widgets cada 30s | Alta | Medio | StateService centralizado + diff |
| Caché inconsistente API vs UI | Media | Medio | Eventos SignalR, expiración corta |
| Memory leak Blazor WASM con timers | Media | Alto | IDisposable en widgets |

### Funcionales

| Riesgo | Prob. | Impacto | Mitigación |
|--------|-------|---------|------------|
| Dashboard intenta ser todo para todos | Alta | Alto | MVP prioritario, evitar feature creep |
| Widgets sin valor real | Media | Alto | Validar con usuarios en beta |
| Info sensible visible a roles incorrectos | Media | Crítico | Filtro por rol en controller |

---

## 18. Recomendaciones UX

1. **El dashboard es un centro de operaciones** — Debe responder preguntas activas, no solo mostrar números.

2. **Jerarquía visual**:
   - Lo crítico arriba (alertas, bloqueos activos)
   - Lo operacional en medio (actividad, MFA, emails)
   - Lo informativo abajo (tendencias, distribuciones)

3. **Color como semáforo**: Verde=OK, Amarillo=Precaución, Rojo=Acción inmediata

4. **Cada widget responde una pregunta**: ¿Cuántos? ¿Crece? ¿Problema? ¿Qué pasó?

5. **Acción desde el widget**: Cada KPI clicable → navega a detalle.

### Flujo de Interacción

```
1. Usuario llega → ojo al SecurityStatus (arriba, grande, color dominante)
2. Si rojo → expande detalles
3. Escanea Activity Feed (centro-izquierda)
4. Revisa MFA Adoption (centro-derecha)
5. Email Queue (abajo)
6. Clica en KPI → va a detalle
```

---

## 19. Recomendaciones UI

### Paleta (MudBlazor Colors)

| Uso | Color | Significado |
|-----|-------|-------------|
| Alertas críticas | Color.Error | Rojo = acción inmediata |
| Alertas media | Color.Warning | Amarillo = precaución |
| Alertas OK | Color.Success | Verde = todo bien |
| KPI primario | Color.Primary | Principal |
| Info | Color.Info | Informativo |

### Tipografía

| Elemento | Typo |
|----------|------|
| Título dashboard | Typo.h4 |
| KPI valor | Typo.h4 |
| Widget title | Typo.h6 |
| Activity text | Typo.body2 |
| Metadata | Typo.caption + Color.Secondary |

### Spacing

| Elemento | Spacing |
|----------|---------|
| Entre widgets | gap-4 en MudGrid |
| Padding widget | pa-4 |
| Entre secciones | mb-6 |
| Entre KPIs | gap-3 |

### Estados Visuales

| Estado | Implementación |
|--------|----------------|
| Loading | MudSkeleton en WidgetContainer |
| Empty | MudIcon + MudText "Sin datos" |
| Error | MudAlert Severity.Error + botón Reintentar |
| Stale | Última actualización desactualizada |

---

## 20. Score del Dashboard Actual

### Evaluación (0-100)

| Categoría | Peso | Puntaje | Justificación |
|-----------|------|---------|---------------|
| Funcionalidad | 25% | 25/100 | Solo 4 stats + 2 alertas |
| UX | 20% | 30/100 | No responde preguntas operativas |
| UI | 15% | 40/100 | Diseño plano sin gráficos |
| Performance | 10% | 50/100 | 7 llamadas, sin cache |
| Seguridad | 10% | 30/100 | Solo 2 indicadores |
| Personalización | 10% | 10/100 | Sin personalización |
| Responsive | 5% | 50/100 | Grid básico sin adaptación |
| Mantenibilidad | 5% | 40/100 | 422 líneas en un archivo |

### Score Final: **33/100**

### Desglose por preguntas

| Pregunta | Respuesta actual | Respuesta deseada |
|----------|-----------------|-------------------|
| ¿Qué requiere atención inmediata? | No visible | SecurityStatus |
| ¿Hay riesgos de seguridad activos? | Solo 2 alertas | ActiveBlocks + FailedLogins + MfaAdoption |
| ¿Usuarios bloqueados o con problemas? | No visible | ActiveBlocksWidget + PasswordHealth |
| ¿Estado de correos y MFA? | No visible | EmailQueueWidget + MfaAdoption |
| ¿Tenants o apps con incidencias? | No visible | HealthCheckWidget |
| ¿Estado general de la plataforma? | "Sistema saludable" genérico | SecurityScore + HealthCheck |

---

## 21. Apéndice: Mapa Funcional

### Domain Catalogos (21 entidades)

`
Tenant → ConfigTenant, DominioTenant, Rol (RolesHerencia, RolesPermisos)
       → Grupo (GruposUsuarios), PoliticaPwd (RolesPoliticasPwd)
       → EmailTemplate, EmailProviders (EmailAccounts)
       → Modulos (TiposModulo, Permisos, AppsModulos)
       → TipoAsignacionPermiso, ConfigApp
App → AppsModulos, AppEmailAccounts, PoliticaPwd
EstadosUsr | ResultadosAcceso | TiposMFA | EstadosMFA | TiposDisp
TiposCambioPwd | TiposBloqueo | TiposAuditoria → Catálogos planos
`

### Domain Core (24 entidades)

`
Usuario (núcleo)
  → Acceso (N:N Apps/Roles/Tenants)
  → HistorialPwd, Sesion, TokenRest, IntentoAcceso
  → Bloqueo, MFA, AuditoriaPwd, DispConfiable
  → Notificacion, EmailLog, UsuarioPermiso, GrupoUsuario

EmailTemplate → EmailTemplateHistorial, EmailTemplatePartials
RolesPermisos (N:N), RolesHerencia (N:N jerárquico)
GruposUsuarios (N:N), AppsModulos (N:N)
TenantEmailAccounts (N:N), AppEmailAccounts (N:N)
`

### Domain Contexto (3 entidades)

`
Disp (dispositivos), IPs (direcciones IP), UserAgents (HTTP)
`

### Procesos Críticos

| Proceso | Entidades | Prioridad |
|---------|-----------|-----------|
| Login | Usuario → IntentoAcceso → HistorialPwd → Bloqueo → MFA → Sesion | Crítico |
| Cambio password | Usuario → HistorialPwd → PoliticaPwd → AuditoriaPwd | Crítico |
| Reset password | TokenRest → Usuario → HistorialPwd → EmailLog | Crítico |
| MFA | MFA → Usuario → Sesion → EmailLog | Alto |
| Envío email | EmailLog → EmailAccount → Provider → PassPlatEmailService | Alto |
| Bloqueo cuenta | Bloqueo → Usuario → IntentoAcceso → Notificacion | Alto |
| Expiración pwd | BackgroundService → Usuario → HistorialPwd → Notificacion | Medio |
| Auditoría | AuditoriaPwd → Usuario → Tenant → App | Medio |

---

## 22. Apéndice: Mapa de Dominios

### Matriz de Dominios vs Tablas

`
IDENTIDAD: Usuarios, Accesos, Roles, RolesHerencia, RolesPermisos,
           Permisos, Modulos, TiposModulo, AppsModulos,
           UsuariosPermisos, Grupos, GruposUsuarios, EstadosUsr

SEGURIDAD: Bloqueos, TiposBloqueo, PoliticasPwd, RolesPoliticasPwd,
           HistorialPwd, MFA, TiposMFA, EstadosMFA, DispConfiables,
           IntentosAcceso, Sesiones

AUTENTICACIÓN: AuthController, TokensRest, MfaController, PasswordController

EMAIL: EmailProviders, EmailAccounts, TenantEmailAccounts,
       AppEmailAccounts, EmailTemplates, EmailTemplatePartials,
       EmailTemplateHistorial, EmailLog, PassPlatEmailService,
       EmailBackgroundService

AUDITORÍA: AuditoriaPwd, TiposAuditoria, Notificaciones, EmailLog

CONFIGURACIÓN: ConfigTenants, ConfigApp, Tenants, Apps, DominiosTenant

CONTEXTO: Disp, IPs, UserAgents

CATÁLOGOS: TiposCambioPwd, TiposAuditoria, TiposMFA, EstadosMFA,
           TiposBloqueo, TiposDisp, ResultadosAcceso, EstadosUsr,
           TiposModulo, TipoAsignacionPermiso
`

### Volumen de Datos Estimado

| Tabla | Volumen | Crecimiento | Retención |
|-------|---------|-------------|-----------|
| Usuarios | Medio | Lento | Indefinido |
| Accesos | Bajo | Lento | Indefinido |
| Sesiones | Alto | Rápido | Transitorio |
| IntentosAcceso | Muy alto | Muy rápido | 1 año |
| HistorialPwd | Alto | Medio | 1 año |
| AuditoriaPwd | Muy alto | Muy rápido | 1 año |
| EmailLog | Alto | Rápido | Indefinido |
| Bloqueos | Bajo | Bajo | Indefinido |
| MFA | Bajo | Bajo | Indefinido |

---

## Resumen Ejecutivo

El Dashboard actual de PassPlat (33/100) es informativo pero no operativo. No responde las 6 preguntas clave de un centro de operaciones, carece de gráficos, no tiene visibilidad de MFA/Email/Salud del sistema, y no es personalizable por rol.

La propuesta de rediseño lo convierte en un **Operations Center** con:

- **20+ KPIs** organizados por dominio (identidad, seguridad, email, sistema, negocio)
- **18 widgets reutilizables** (KPI cards, activity feed, security status, email queue, system health)
- **7 tipos de gráficos** (líneas, barras, donut, treemap, heatmap, sparkline, timeline)
- **Alertas en 4 niveles** (críticas en tiempo real, altas, medias, informativas)
- **Arquitectura por capas** (SPs dedicados → Repository → Service → Controller → Blazor StateService)
- **Estrategia de performance** (polling inteligente, IMemoryCache + Redis, SignalR)
- **Responsive** en 4 breakpoints con adaptación de contenido

**Roadmap**: 10 semanas total (MVP funcional en 3 semanas).

---

> **Fin del documento**
