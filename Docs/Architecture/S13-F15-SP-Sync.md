# S13 — F15: SQL Sync — Stored Procedures (SP vigilados)

> FASE 15 del Sprint S13 — Verificación read-only de sincronización de SPs.
> Fecha: 2026-08-03 · Estado: ✅ COMPLETADO — 0 divergencias funcionales

---

## 1. Alcance

Verificar que los **6 SP vigilados** del plan S13 (los que habían divergido del canónico en
análisis previos) están sincronizados entre la base de datos real (`PassPlat`) y el canónico
`D:\CODIGOS\BBDD\PASSWORDS SP.sql`.

**Método**: comparación de cuerpo normalizado por SP. El dump de BD se obtiene de
`sys.sql_modules.object_definition` (fuente autoritativa de lo ejecutado en runtime);
el canónico se extrae del archivo `.sql` por bloque `CREATE OR ALTER PROCEDURE`.

## 2. Metodología de normalización

Para eliminar ruido no funcional antes de comparar:

1. Se eliminan líneas de solo-comentario (`--`, `/*`, `*/`) y bloques `GO`.
2. `CREATE OR ALTER PROCEDURE` → `CREATE PROCEDURE` (SQL Server guarda el cuerpo sin `OR ALTER`).
3. Se descarta metadata de sesión que `object_definition` añade/fusiona
   (`SET NOEXEC OFF`, `SET QUOTED_IDENTIFIER ON/OFF` de cola).

Las únicas diferencias aceptadas son:
- Espaciado múltiple tras `CREATE` (`CREATE   PROCEDURE` vs `CREATE PROCEDURE`) — SQL Server
  guarda la definición tal como se creó; no afecta ejecución.
- Instrucciones `SET` de sesión al final del cuerpo.

## 3. Resultado de la comparación (22 SPs totales)

| # | SP | Resultado | Notas |
|---|----|-----------|-------|
| 1 | `SP_Auth_Login` | ✅ SÍNC | Solo spacing + `SET NOEXEC OFF` metadata |
| 2 | `SP_Auth_LoginExterno` | ✅ SÍNC | Idéntico |
| 3 | `SP_Auth_RenovarTokenProveedor` | ✅ SÍNC | Idéntico |
| 4 | `SP_Dashboard_IdenExt` | ✅ SÍNC | Idéntico |
| 5 | `SP_Dashboard_IdentidadExterna` | ✅ SÍNC | Idéntico |
| 6 | `sp_DashboardEnterprise_GetAll` | ✅ SÍNC | Solo spacing + `SET QUOTED_IDENTIFIER ON` metadata |
| 7 | `SP_IdenExt_Desvincular` | ✅ SÍNC | Solo metadata de sesión en cola |
| 8 | `SP_IdentidadExterna_Desvincular` | ✅ SÍNC | Solo metadata de sesión en cola |
| 9 | `SP_Matriz_Permisos_Leer` | ✅ SÍNC | Idéntico |
| 10 | `SP_MFA_Validar` | ✅ SÍNC | Idéntico |
| 11 | `SP_Permisos_Usuario_Efectivos` | ✅ SÍNC | Idéntico |
| 12 | `SP_ProvIden_ActualizarPerfil` | ✅ SÍNC | Idéntico |
| 13 | `SP_ProvIden_BuscarUsuario` | ✅ SÍNC | Idéntico |
| 14 | `SP_ProvIden_RegistrarAuditoria` | ✅ SÍNC | Idéntico |
| 15 | `SP_ProvIden_VincularUsuario` | ⚠️ BD OK / archivo con mojibake | Ver §4 |
| 16 | `SP_Purge_DatosAntiguos` | ✅ SÍNC | Idéntico |
| 17 | `SP_Pwd_Cambiar` | ✅ SÍNC | Idéntico |
| 18 | `SP_Rol_Crear` | ✅ SÍNC | Idéntico |
| 19 | `SP_Sesiones_Crear` | ✅ SÍNC | Idéntico |
| 20 | `SP_TokensRest_Generar` | ✅ SÍNC | Idéntico |
| 21 | `SP_TokensRest_Validar` | ✅ SÍNC | Idéntico |
| 22 | `SP_Usuario_Crear` | ⚠️ BD OK / archivo con mojibake | Ver §4 |

**Resultado global**: **22/22 SPs presentes en BD · 20/22 byte-iguales tras normalización ·
2/22 con defecto de encoding en el archivo canónico (no en BD). 0 divergencias funcionales.**

## 4. Defecto de encoding detectado (no bloqueante)

Los SP `SP_ProvIden_VincularUsuario` y `SP_Usuario_Crear` muestran diferencias únicamente en
los **literales de mensaje** (RAISERROR/SELECT) y **solo en el archivo `PASSWORDS SP.sql`**:

| Mensaje | BD (correcto) | Archivo canónico (mojibake) |
|---|---|---|
| `SP_ProvIden_VincularUsuario` | `El usuario ya está vinculado a este proveedor` | `El usuario ya estÃƒÆ'Ã†â€™Ãƒâ€šÃ‚Â¡ vinculado...` |
| `SP_ProvIden_VincularUsuario` | `El subexterno ya está vinculado a otro usuario` | doble-codificación UTF-8 |
| `SP_Usuario_Crear` | `El correo electrónico ya está registrado` | `El correo electrÃ³nico ya estÃ¡ registrado` |
| `SP_Usuario_Crear` | `El nombre de usuario ya está registrado` | `El nombre de usuario ya estÃ¡ registrado` |
| `SP_Usuario_Crear` | `contraseña` / `vacío` / `política` | `contraseÃ±a` / `vacÃ­o` / `polÃ­tica` |

**Conclusión**: la BD es la fuente correcta (Unicode bien almacenado). El archivo
`PASSWORDS SP.sql` contiene texto doble-codificado en UTF-8 en esos literales. Este defecto
**no afecta al runtime** (la BD ejecuta su definición correcta) y **no requiere acción** en el
ámbito S13 (F15 es read-only). Se recomienda re-generar el archivo desde la BD
(`SCRIPT AS CREATE`) en un sprint de mantenimiento para corregir el encoding.

**Causa raíz probable**: ediciones previas del `.sql` con codificaciones mezcladas
(UTF-8 → Latin-1 → UTF-8) durante las Fases 7.9 (restauración de acentos por grupos).
Solo 2 de 22 SPs resultaron afectados.

## 5. Evidencia

- Dump BD: `object_definition` de `sys.procedures` (22 SPs, ancho ilimitado).
- Canónico: bloques extraídos de `D:\CODIGOS\BBDD\PASSWORDS SP.sql` (22 bloques).
- Comparación: normalización + `git diff --no-index` por SP.
- Trabajo temporal: `%TEMP%\s13-f15\` (dump DB_*, canónico CAN_*, y comparación cmp/).

## 6. Checklist F15

- [x] 22/22 SPs presentes en BD (nombres coinciden con canónico)
- [x] Comparación cuerpo normalizado BD vs canónico por SP
- [x] 6 SP vigilados analizados individualmente (Auth_Login, DashboardEnterprise_GetAll,
      IdenExt_Desvincular, IdentidadExterna_Desvincular, ProvIden_VincularUsuario, Usuario_Crear)
- [x] 0 divergencias funcionales — runtime usa definiciones correctas
- [x] Defecto de encoding documentado (no bloqueante, requiere sprint de mantenimiento)
- [x] Sin cambios de código ni de BD (F15 read-only)
- [x] Sin regresión (F14.3/F14.4/F14.5 verdes antes de F15)

---

*Sprint S13 — FASE 15 de 16. Siguiente: F16 (documentación + reporte final).*
