# S15-Security-Logging-Analysis.md — Análisis Transversal Seguridad ∩ Logging (F7.1 ∩ F7.2)

# Estado          FINAL (análisis — no genera evidencia nueva)
# Tipo            ☐ Evidencia ☑ Análisis ☐ Decisión
# Fuente          Security-Audit.md, Logging-Audit.md, Logging-Observability-Audit.md, Security-Logging-Audit.md
# Depende de      S15-Security-Logging-Audit (SEC-001..014) · Security-Audit (SEC-041..048) · Logging-Audit (LOG-001..007)
# Influye en      Certification, Refactoring-Plan, Technical-Debt-Index
# Propósito       Integrar la evidencia de seguridad y de logging en un análisis transversal: qué secretos se exponen, qué excepciones se tragan, y qué canal (ILogger v BD) cubre cada operación sensible.

---

## 1. Propósito (síntesis transversal, sin hallazgo nuevo)

Este documento integra la lectura conjunta de los cuatro docs N1 de seguridad/logging. NO descubre evidencia nueva: interpreta la cruz operacional de dos dominios que en el código conviven (registro estructurado vs. auditoría persistida). Las acciones se refieren al `S15-CBP-Refactoring-Plan.md` (N3).

Referencia metodológica: `S15-Audit-Methodology.md` §6 (Acción) y §10 (Confidence).

## 2. Matriz de cobertura Seguridad ∩ Logging (solo síntesis de SEC-001..014 + LOG-001..007)

| Operación sensible | Canal registro estructurado (ILogger/Serilog) | Auditoría persistida (BD) | Excepción tragada | Doc N1 fuente |
|---|---|---|---|---|
| Exposición ciphertext config | SI — **Console.WriteLine** (MUY MAL) | NO | — | SEC-001 / LOG-006 / CFG-001 |
| Login password | SI (AuthService) | SI (AuditoriaPwd, IntentoAcceso) | NO | SEC-003..004 |
| Login OAuth | SI (ExternalAuthService, EventIds) | SI (AudIdenExt + TraceId/Jti) | NO | SEC-009..010 |
| MFA envío | SI / parcial | parcial | **SI (SEC-005)** | SEC-005 |
| Bloqueo notificación | SI | SI (Bloqueo) | **SI (SEC-006)** | SEC-006 |
| Cambio pwd | SI (PasswordService) | SI (HistorialPwd, AuditoriaPwd) | NO | SEC-004 |

## 3. Análisis de interacción principal (decisión por evidencia N1)

### 3.1 El silencio degrada la seguridad (SEC-005, SEC-007)

`AuthService.EnviarCodigoMfaAsync` y el getter de método MFA devuelven `null` en catch. La interpretación transversal: en flujos sensibles (MFA, email), **un error interno se convierte en "sin resultado"**. Ello oculta con mono la causa raíz del fallo de envío de código MFA (bloqueo histórico FASE13) y puede producir bypass de verificación (SEC-007). Coherente con `LOG-004` (LogCritical sin usar: no hay escalado).

### 3.2 La fuga ciphertext es tranversal (SEC-001 = LOG-006 = CFG-001)

El mismo defecto se observa desde 3 prismas (seguridad, logging, configuración). Interpretación: el problema de fondo no es de "canal de logging" sino **exposición de secretos en salida no controlada** (`Console.WriteLine` con prefix ciphertext). Acciones remitidas a N3 (Refactoring: Fase 1 P0).

### 3.3 Excepciones tragadas en logging vs. auditoría persistente

La trazabilidad crítica (AuditoriaPwd, AudIdenExt) ESTÁ persistida con CorrelationId/IP/UA (SEC-010, SEC-011); el déficit es el **canal de log estructurado**: logs anulados, no recuperables en consola. Por ello X medios no requieren cambio de BD sino de pipeline de logging (LOG-002 multi-pipeline).

## 4. Conclusión del análisis transversal (sin decisión)

- La **auditoría persistida** de seguridad es sólida y completa (passwords/tokens nunca logueados en clear: SEC-004).
- El **canal de log estructurado** de seguridad es el punto débil: 1 excepción real silenciada (MFA), 1 fuga de cipher, y falta de LogCritical (LOG-004).
- La mejora de operabilidad y coherencia entre ambos canales (un único pipeline + propagation de Result + auditoría por excepción) se refiere al ✨Refactoring-Plan (Fase 1-2).

Este doc NO certifica áreas: solo aporta lectura transversal. La certificación está en `S15-Certification.md` (N3) y la puntuación de los componentes en sus docs N1.