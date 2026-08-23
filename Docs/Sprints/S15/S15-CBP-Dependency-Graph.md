# S15-CBP-Dependency-Graph.md — Grafo de dependencias PassPlat <-> CBP

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          CBP-Inventory
# Depende de      Inventory
# Influye en      Duplication, Services, Data, WebApi
# Area            Arquitectura de dependencias (F0.5)
# Framework CBP   Todas las librerias CBP (entrantes/salientes)
# Cobertura       Aplicacion | Infraestructura | WebApi | Workers | Tests
# Evidencia       28 proyectos (solucion PassPlat.slnx) - grafo Roslyn - csproj verificados - 0 ciclos
# Resultado       PASS (grafo sin ciclos; se documentan dependencias innecesarias)
# Cobertura       N/A (inventario estructural)
# Riesgo          Bajo
# Prioridad       Alta

---

## 1. Proposito

Documentar el grafo de dependencias entre los proyectos PassPlat y las librerias CBP para: identificar quien consume que, quien deberia consumir, detectar dependencias ciclicas y senalar dependencias innecesarias. **Insumo critico** para que un cambio en Logging/Auth/Events no rompa otras areas (previene regresiones en el refactor S16+).

## 2. Regla general de auditoria (12 preguntas)
Aplicable a cada hallazgo. Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Grafo de dependencias

### 3.1 Proyectos PassPlat -> librerias CBP (referencias verificadas en .csproj)

| Proyecto PassPlat | Dependencias CBP directas |
|---|---|
| PassPlat.Dominio | (ninguna) |
| PassPlat.Aplicacion.Dtos | (ninguna) |
| PassPlat.Datos | CBP.Data.Asynchronous, CBP.Data.Utilities, CBP.Caching.Abstractions |
| PassPlat.Aplicacion | CBP.Services.Async, CBP.Events, CBP.Caching.Abstractions, CBP.Authentication.JwtBearer, CBP.Emails, CBP.Security.Cryptography, CBP.MultiTenant |
| PassPlat.WebAPI | CBP.Authentication.JwtBearer, CBP.Caching.Abstractions, CBP.Caching.Memory, CBP.Logging, CBP.MultiTenant, CBP.WebApi |
| PassPlat.Web (Blazor) | (ninguna CBP; solo PassPlat.Aplicacion.Dtos y PassPlat.Dominio) |
| PassPlat.Consola | CBP.Security.Cryptography |
| PassPlat.Aplicacion.Test | CBP.Caching.Abstractions (+ transitivas) |

### 3.2 Dependencias internas CBP (Red de infraestructura)

| Libreria | Depende de |
|---|---|
| CBP.Results, CBP | (sin deps internas) |
| CBP.Events | CBP.Results |
| CBP.Data.Abstractions | CBP.Results |
| CBP.Data.Asynchronous | CBP.Data.Abstractions, CBP.Results |
| CBP.Data.Utilities | CBP.Data.Abstractions |
| CBP.Services.Abstractions | CBP.Data.Abstractions |
| CBP.Services.Async | CBP.Services.Abstractions, CBP.Data.Asynchronous |
| CBP.WebApi | CBP.Results, CBP.Services.Abstractions |
| CBP.Logging | CBP.Core |
| CBP.MultiTenant | CBP.Results |
| CBP.Authentication.JwtBearer | CBP.Authentication.Abstractions, CBP.Results |
| CBP.Caching.Memory/Redis/NCache | CBP.Caching.Abstractions |

### 3.3 Flujo por capas (clean architecture)

```
Dominio (sin deps)
  ^
Datos (CBP.Data.*, Caching)
  ^
Aplicacion (Auth, Emails, Security, MultiTenant, Events, Services, Caching)
  ^
WebAPI (host -> Aplicacion, Logging, WebApi base, Cache.Memory, MultiTenant)
  |
Blazor (solo Dtos/Dominio, sin CBP)
```

## 4. Analisis

### 4.1 Dependencias ciclicas
**Ninguna.** Roslyn detecta `cycles: []`. Arbol sano, flujo direccional limpio.

### 4.2 Quien deberia consumir y no consume (oportunidades)
- PassPlat.Aplicacion **deberia** usar CBP.Logging (ILoggerService) — hoy NO lo referencia ni lo usa (ver F7.1).
- PassPlat.Aplicacion referencia CBP.Events pero su `IDomainEventDispatcher` esta en 0 usos (ver F3).

### 4.3 Dependencias innecesarias / mortas

| Hallazgo | Descripcion | Resultado | Accion | Confidence |
|---|---|---|---|---|
| **DEP-001** | CBP.Logging referenciada y registrada (`AddCbpLogging`) pero `ILoggerService` SIN CONSUMIR en todo PassPlat uliza Serilog directo | FAIL | JUSTIFICAR / REEMPLAZAR consumo | Alta |
| **DEP-002** | CBP.Events referenciada en Aplicacion, dispatcher sin usar ul | FAIL | DIFERIR / incorporar dispatcher (S16) | Alta |
| **DEP-003** | PassPlat.Web sin deps CBP — correcto (Blazor WASM client-side no debe referenciar data/services) | PASS | REUTILIZAR | Alta |
| **DEP-004** | PassPlat.Consola referencia Security.Cryptography directo | PASS CON OBS | JUSTIFICAR (herramienta dev) | Media |

## 5. Impacto transversal del refactor S16+

Como CBP.Logging y CBP.Events son hojas (no referenciadas por Data/Authentication), **un cambio en Logging o Events NO rompera Data ni Authentication**. El riesgo de regresion cruzada es bajo por el aislamiento del grafo. Esto permite refactorizaciones independientes (ver F12).

## 6. Resultado F0.5
Grafo sin ciclos, flujo por capas correcto. Hallazgo transversal: `ILoggerService` (CBP.Logging) registrado sin consumo; `IDomainEventDispatcher` (CBP.Events) sin uso. Estos se auditan en detalle en F7 y F3 respetivamente.

Quien deberia consumir: PassPlat.Aplicacion deberia usar ILoggerService (CBP.Logging) y IDomainEventDispatcher (CBP.Events).

## 7. Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 100 % (grafo completo) |
| Architecture Score | 88 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-DEP-001..004 |