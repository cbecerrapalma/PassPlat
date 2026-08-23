# S15-Audit-Methodology.md — Metodología y Gobernanza de la Auditoría S15 (documento raíz)

# Tipo          ☑ Decisión (gobernanza de la propia auditoría)
# Estado        FINAL — Gobernanza raíz, línea base arquitectónica oficial
# Tipo          ☐ Evidencia ☐ Análisis ☑ Decisión (Gobernanza raíz)
# Fuente        N/A (root)
# Depende       N/A
# Influye       Todos
# Fase          Nivel 0 (raíz — no es Evidencia/Análisis/Decisión de negocio, sino gobierno del proceso)
# Licencia      Este documento ES la fuente única de reglas. NINGÚN otro documento repite estas reglas: todos referencian este.

---

## 1. Objetivo

Fijar permanentemente la metodología, métricas, convenciones y criterios de certificación que gobiernan S15 y las auditorías posteriores (S16, S17…). Es el **único** lugar donde viven estas reglas; el resto de documentos las referencian y permiten que el mantenimiento futuro sea mínimo.

Como **baseline arquitectónica oficial**, S16/S17 compararán contra el estado congelado en esta cert expedición, no volverán a medir todo desde cero.

---

## 2. Alcance de S15

- **Objetivo**: construir el repositorio de evidencia de adopción del framework CBP en PassPlat, validado, trazable y certificable.
- **Método**: Solo lectura sobre código (análisis, clasificación, documentación). Prohibido modificar comportamiento.
- **Entregables**: N documentos organizados en 3 niveles + el presente de gobernanza.
- **Solución**: `PassPlat.slnx` (28 proyectos), C# net10.0; Framework `D:\CODIGOS\CBP` (24 proyectos).
- **Dominio de los IDs de hallazgos**: cada doc define su propio prefijo (ej. `LOG-`, `EVENT-`, `DATA-`) — ver convención de IDs §12.

---

## 3. Arquitectura documental — Niveles de clasificación

Todo documento, salvo este de gobernanza, SE clasifica en UNO y SOLO UNO de 3 niveles.

| Nivel | Nombre | Contiene | Prohibido |
|---|---|---|---|
| **Nivel 1** | Evidencia | Hechos verificables, evidencia trazable, observaciones objetivas | Recomendaciones, decisiones, backlog, Acción |
| **Nivel 2** | Análisis | Interpretaciones multifactoriales que integran evidencia de N1 | Descubrir evidencia NUEVA; proponer decisiones |
| **Nivel 3** | Decisión | Acciones arquitectónicas, backlog, matriz, certificación | Hallazgos nuevos; consolidar sin respaldo de N1/N2 |

### Gobernanza de flujo (obligatoria)

```
Nivel 1
   ↓  (interpretación)
Nivel 2
   ↓  (decisión)
Nivel 3
```

- **NUNCA al revés.** Un documento N3 (Compliance Matrix, Executive Summary, Refactoring Plan, Technical Debt Index, Architecture Decisions) **no puede generar un hallazgo nuevo**; solo consolida hallazgos existentes de N1/N2.
- Si durante la consolidación de N3 aparece una observación nueva, esta **debe volver** al documento N1/N2 correspondiente **antes** de poder formar parte de cualquier decisión.
- **Refactoring Plan**: ninguna acción puede existir si no existe previamente un hallazgo trazable en N1 o N2.

```
Refactoring
   │
   ▼
Hallazgo (N1/N2)
   │
   ▼
Evidencia
```

Nunca `Idea → Refactoring` sin respaldo.

---

## 4. Estructura de cada documento (cabecera estandarizada)

Todo documento (salvo este) comienza con:

```
# Tipo          ☐ Evidencia  ☐ Análisis  ☐ Decisión
# Estado        Borrador | FINAL | CERTIFICADO
# Fuente        <doc(s): N1/N2 que alimentan este doc>      (N/A para N1 raíz)
# Depende de    <doc N1/N2 con evidencia que interpreta>    (N/A para N1 raíz)
# Influye en    <doc N2/N3 que dependen de este>
# Propósito     <una línea: QUÉ demuestra, NUNCA qué recomienda>
```

---

## 4. Ficha técnica (head banner) por documento

Tras la cabecera, cada doc mantiene el banner en Ay que ya incluía:

| Etiqueta | Significado |
|---|---|
| `Estado` | Borrador / FINAL / CERTIFICADO |
| `Área` | Módulo auditado (F1..F10) + framework CBP |
| `Framework CBP` | Paquete/s concreto de CBP |
| `Cobertura` | Proyectos/capas abarcadas |
| `Evidencia` | Qué fuentes verificables se cierran |
| `Resultado` | PASS/WARNING/FAIL o clasificación de acción |
| `Cobertura %` | de adopción CBP |
| `Riesgo` | Bajo/Medio/Alto/Crítico |
| `Prioridad` | P0–P3 |

---

## 5. Definiciones de estado de un área / hallazgo

| Resultado | Definición objetiva |
|---|---|
| **PASS** | Usa el framework CBP correctamente o el área es propia-legítima; sin deuda bloqueante. |
| **PASS CON OBSERVACIONES** | Correcto pero con observaciones menores no bloqueantes. |
| **WARNING** | Riesgo/deuda identificado, sin fallo funcional crítico. |
| **FAIL** | Hallazgo crítico de arquitectura, seguridad o infracción de contrato CBP. |
| **NO APLICA** | No evaluable (área fuera de contexto). |

---

## 6. Definiciones de acción (Acción)

| Acción | Definición |
|---|---|
| **REUTILIZAR** | Usar el componente CBP tal cual; no hay cambio. |
| **EXTENDER** | Extender el componente CBP con lógica de dominio; enlace legítimo (ver regla anti falso-duplicado S9). |
| **REEMPLAZAR** | Sustituir el código propio por el equivalente CBP cuando es duplicación sin valor. |
| **JUSTIFICAR** | Mantener el código con justificación documentada (dominio no cubierto); no migrar. |
| **ELIMINAR** | Remover código sin uso/duplicado/obsoleto. |
| **DIFERIR** | Posponer por ausencia de acoplamiento necesario hoy. |

> **Regla de extensiones legítimas**: No se considera desviación la existencia de código propio que actúe como extensión especializada sobre CBP y aporte capacidades que el framework no ofrece de forma nativa → **EXTENDER**, NUNCA **REEMPLAZAR**, salvo evidencia objetiva de duplicación funcional sin valor agregado.
> Ej: `AuthenticationTokenIssuer`, `JwtTenantContext`, `PermissionClaimBuilder`, `SessionManager`, `AuthenticationTokenService`.

---

## 7. Criterios de Confidence

| Nivel | Definición |
|---|---|
| **Alta** | Evidencia directa (archivo, línea, clase, grep) + verificación cruzada. |
| **Media** | Evidencia parcial o inferida de un flujo/documento. |
| **Baja** | Inferencia, sin verificación directa — se marca como estimación. |

---

## 8. Fórmula del Architecture Maturity Score

Promedio ponderado por área del Acoplamiento del módulo con CBP:

`Puntaje = (Cobertura × 0.40) + (Integración CBP × 0.30) + ((100 − Duplicación) × 0.20) + (Calidad × 0.10)`

- `Cobertura` = % de componentes del área que usan CBP (0-100).
- `Integración CBP` = uso correcto del contrato (0-100).
- `Duplicación` = % de funcionalidad reimplementada fuera de CBP.
- `Calidad` = evaluación de consistencia/correctitud (0-100).

Estado por rango: `≥90 PASS` · `75-89 REUTILIZAR/EXTENDER` · `50-74 WARNING` · `<50 FAIL`.

---

## 9. Fórmula del CBP Adoption Index

```
CBP Adoption Index = (Componentes CBP usados correctamente) / (Componentes CBP disponibles)
```
- `Disponibles` = tipos/componentes públicos de CBP en el módulo.
- `Usados` = los que PassPlat inyecta/consum` correctamente.
- Mide **solo adopción**, distinto del Architecture Score (que mide calidad). Un área puede tener adopción alta y calidad media, y viceversa.

Ejemplos de referencia del índice (ver docs por módulo):

| Módulo | Disponibles | Usados | Índice estimado |
|---|---|---|---|
| Authentication | 18 | ~17 | ~94 % |
| Logging | ~14 | ~6 | ~43 % |
| Events | ~11 | ~1 | ~9 % |

---

## 10. Regla anti falso-duplicado

- Rastro: si un componente replica LOC de uno CBP pero **extiende su contrato** (base + dominio), clasificaras s**EXTENDER**, nunca `REEMPLAZAR`.
- Solo se es `REEMPLAZAR`/`ELIMINAR` si hay evidencia objetiva de duplicación funcional sin valor agregado.

### 10.1 Clasificación de duplicación en 3 tipos

| Tipo | Definición | Ejemplo |
|---|---|---|
| **Funcional** | Repite la funcionalidad q CBP ya abstrae sin usarla | `EventPublisher` static duplica `DomainEventDispatcher` |
| **Estructural** | LOC/forma similares pero hereda/compone base CBP | `AuthenticationTokenIssuer` (misma forma, extiende `CBP.Authentication`) → EXTENDER |
| **Tecnológica** | Duplica una capability infraestructura (cache, logging, DI) | `AddMemoryCache` junto a `AddCbpCache` |

---

## 11. Cobertura de evidencia

Formato obligatorio de evidencia (por hallazgo):

```text
Proyecto · Archivo · Clase · Método · Líneas · [Commit/Branch] · Framework CBP asociado
```

Siempre con enlace trazable. Si falta un dato (ej. commit), se omite y se reduce Confidence.

---

## 12. Convención de IDs de hallazgos

- Prefijo por tipo/grupo documental: `LOG-`, `SEC-`, `EVENT-`, `DATA-`, `CACH-`, `WEB-`, `SRV-`, `MT-`, `DI-`, `CFG-`, `AUTH-`, `OBS-`, `CTX-`, `QAUD-`, `CADD-`, `COUPL-`, `EXT-`, `DUP-`.
- Numérico incremental (`001…`). Nunca reutilizar un ID eliminado.
- Cada ID NO se usa en +1 doc — su dueño es el documento N1/N2 que lo emite.

---

## 13. Convención de prioridades

| Prioridad | Definición |
|---|---|
| **P0** | Crítico: exposición de secretos, bloque de seguridad o infracción de contrato. Debe ser atendido ya. |
| **P1** | Alto: pérdida de funcionalidad, riesgo de corrección de seguridad/consistencia. Próximo sprint. |
| **P2** | Medio: mejora de mantenibilidad/consistencia, deuda no bloqueante. |
| **P3** | Bajo: limpieza/cosmética opcional. |

**Nota**: prioridad es **independiente** de Resultado. Un `FAIL` no implica alta prioridad (`CBP.Events` FAIL + REEMPLAZAR → P2); un `FAIL` en logging/seguridad → P0.

---

## 14. Convención de trazabilidad / grafo documental

- Cada doc declara `Fuente / Depende de / Influye en` en cabecera (sección 3).
- Las deps que de un documento N2 refieren exclusivamente N1 (nunca otro N2 como fuente).
- Un texto N3 depnde de N1+N2 (no de otro N3).
- Para validar el grafo, la Certification lista el conteo de docs y sus niveles.

---

## 15. Formato obligatorio de evidencia y de observación

En documentos de N1, cada observación consta de:
1. `Evidencia` → 2. `Observación objetiva` → 3. `Análisis` (en N2) → 4. `Resultado` (solo en N1/N2) → 5. `Acción` (solo en N2/N3) → 6. `Confidence` → 7. `Referencias`.

La **Acción** y recomendaciones SOLO viven en N2/N3, nunca en N1.

---

## 16. Criterios de certificación

S15 se considera **CERTIFICADO** cuando:
1. Base de datos 16 áreas + compliance + refac + deuda auditados con evidencia.
2. Los 3 niveles documentados con gobernanza de flujo respetada.
3. Cero evidencia nueva descubierta en N3.
4. Toda acción del Refactoring plan trazable a un hallazgo N1/N2.
5. `dotnet build PassPlat.slnx` = 0 errores + 0 warnings.
6. Métricas de cierre generadas: Architecture Score global + CBP Adoption Index global.

---

## 17. Evolución para S16, S17…

- **S16 no vuelve a medir todo**. Compara contra esta baseline.
- S17 compara contra S16; S18 contra S17; etc.
- Evolución objetiva de: Architecture Maturity Score, CBP Adoption Index, Technical Debt, Cobertura CBP, Duplicación, Acoplamiento.

---

## nS. Gobernanza de cambios a ESTE documento

- Cualquier cambio de regla (puntaje, índice, prioridad, definición) se registra en la **Cabecera de versiones** al final de este doc, con fecha y ADR asociado.
- No se cambia una regla en silencio: cada regla alterada genera entrada de versión.

---

## Versiones de esta metodología

| Vers | Fecha | Cambio |
|---|---|---|
| 1.0 | 2026-08-06 | Versión inicial: 3 niveles, métricas, confianza, gobernanza de flujo, ciencia de extensiones legítimas, regla anti falso-duplicado, CBP Adoption Index. Baseline arquitectónica oficial. |