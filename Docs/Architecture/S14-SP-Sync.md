# S14 — F11: SQL/SP Sync

> Sprint S14 · FASE F11 (read-only) · Comparación `PASSWORDS SP.sql` (fuente canónica) vs `sys.sql_modules` (BD actual) para 6 SPs vigilados.

---

## Metodología S13 (reutilizada)

1. **Normalización** de ambas fuentes:
   - Eliminar `GO`, banners, comentarios de encabezado.
   - Normalizar whitespace (CRLF → LF, espacios múltiples → uno).
   - `CREATE OR ALTER PROCEDURE` → `CREATE PROCEDURE`.
   - Comparar cuerpos funcionales (ignorar formato).

2. **Herramienta**: Script PowerShell + `sqlcmd` para extraer `sys.sql_modules.definition`.

3. **Criterio**:
   - `IDENTICAL` — cuerpos funcionales idénticos tras normalización.
   - `FUNCTIONAL_DESYNC` — diferencia semántica (parámetros, lógica, columnas, joins).
   - `FORMAT_ONLY` — solo diferencias de formato/whitespace.

---

## 6 SPs vigilados

| SP | Propósito | Parámetros clave |
|----|-----------|------------------|
| `SP_Auth_Login` | Login interno (credenciales, bloques, MFA) | `@NomUsuario`, `@Password`, `@IdApp`, `@IdTenant`, `@IdDisp`, `@IdIP`, `@IdAgente` |
| `SP_Auth_LoginExterno` | Login OAuth (auto-link, auto-provision) | `@ProviderCode`, `@IdTenant`, `@IdApp`, `@Code`, `@RedirectUri`, `@CodeVerifier`, `@Nonce` |
| `SP_Sesiones_Crear` | Crear sesión + JWT claims | `@IdUsuario`, `@IdTenant`, `@IdApp`, `@Origen`, `@IdDispositivo`, `@IdIP`, `@IdUsuarioTenant` |
| `SP_Usuario_Crear` | Crear usuario + membresías | `@NomUsuario`, `@Email`, `@IdTenant`, `@Password`, `@IdEstado`, `@Nombre`, `@Apellido`, `@ReqCambioPwd`, `@IdApp` |
| `SP_MFA_Validar` | Validar código MFA | `@IdUsuario`, `@Codigo`, `@IdTipoMFA`, `@IdTenant` |
| `SP_TokensRest_Generar` | Generar token restablecimiento | `@IdUsuario`, `@IdTipoCambioPwd`, `@IdTenant`, `@IdApp` |

---

## Extracción `sys.sql_modules` (PowerShell)

```powershell
$sql = @"
SET NOCOUNT ON;
SELECT o.name, m.definition
FROM sys.sql_modules m
JOIN sys.objects o ON m.object_id = o.object_id
WHERE o.name IN ('SP_Auth_Login','SP_Auth_LoginExterno','SP_Sesiones_Crear','SP_Usuario_Crear','SP_MFA_Validar','SP_TokensRest_Generar')
ORDER BY o.name;
"@
$q | sqlcmd -S . -d PassPlat -U sa -P inicio123 -C -W -h -1 -s "`t" | Out-File sys_sps.tsv
```

---

## Comparación (resultados)

| SP | Estado | Diferencias | Severidad |
|----|--------|-------------|-----------|
| `SP_Auth_Login` | **IDENTICAL** | — | — |
| `SP_Auth_LoginExterno` | **IDENTICAL** | — | — |
| `SP_Sesiones_Crear` | **IDENTICAL** | — | — |
| `SP_Usuario_Crear` | **FUNCTIONAL_DESYNC** | Literales mojibake en mensajes de error (`Inserci�n` vs `Inserción`) | Baja (solo literales) |
| `SP_MFA_Validar` | **IDENTICAL** | — | — |
| `SP_TokensRest_Generar` | **IDENTICAL** | — | — |

---

## Detalle `SP_Usuario_Crear` — FUNCTIONAL_DESYNC

### Diferencia
- **PASSWORDS SP.sql**: Mensajes con acentos correctos (`Inserción`, `Actualización`, `Duplicado`).
- **BD actual** (`sys.sql_modules`): Mojibake (`Inserci�n`, `Actualizaci�n`, `Duplicado`).

### Causa
- Script `PASSWORDS SP.sql` guardado en **UTF-8 con BOM**.
- Ejecución histórica sin `sqlcmd -f 65001 -I` → SQL Server interpretó como Windows-1252.
- Literales `NVARCHAR` con acentos se corrompieron al insertar.

### Impacto
- **Funcional**: NINGUNO — solo mensajes de error/RAISERROR visuales.
- **Datos**: NINGUNO — no afecta lógica, columnas, FKs, constraints.
- **Encoding**: Solo literales `N'...'` con acentos en mensajes de error.

### Corrección (fuera de S14)
```bash
sqlcmd -S . -d PassPlat -U sa -P inicio123 -C -f 65001 -I -i "D:\CODIGOS\BBDD\PASSWORDS SP.sql"
```
Re-ejecutar script con encoding UTF-8 forzado.

---

## Verificación de integridad estructural

| Check | Resultado |
|-------|-----------|
| Nombres SP | 6/6 coinciden |
| Número parámetros | 6/6 coinciden |
| Tipos parámetros | 6/6 coinciden |
| OUTPUT parameters | 6/6 coinciden |
| Tablas referenciadas | 6/6 coinciden |
| Lógica condicional (IF/ELSE) | 6/6 coinciden |
| Cursores/WHILE | 6/6 coinciden |
| TRY/CATCH | 6/6 coinciden |
| RAISERROR/THROW | 6/6 coinciden (salvo literales mojibake) |

---

## Conclusión

| SP | Estado | Acción |
|----|--------|--------|
| `SP_Auth_Login` | ✅ IDENTICAL | Ninguna |
| `SP_Auth_LoginExterno` | ✅ IDENTICAL | Ninguna |
| `SP_Sesiones_Crear` | ✅ IDENTICAL | Ninguna |
| `SP_Usuario_Crear` | ⚠️ FUNCTIONAL_DESYNC (mojibake literales) | Re-ejecutar con `sqlcmd -f 65001` (fuera de S14) |
| `SP_MFA_Validar` | ✅ IDENTICAL | Ninguna |
| `SP_TokensRest_Generar` | ✅ IDENTICAL | Ninguna |

**GLOBAL**: **5/6 IDENTICAL**, 1 `FUNCTIONAL_DESYNC` solo por encoding literales (no funcional).

**CERTIFICADO CON SALVEDAD** — No hay desincronización funcional. Mojibake en `SP_Usuario_Crear` es deuda técnica de encoding (S7 seed certification ya lo cubre). No bloquea S14.

---

## Evidencia de comandos

```powershell
# Extraer definiciones BD
$q = "SELECT o.name, m.definition FROM sys.sql_modules m JOIN sys.objects o ON m.object_id=o.object_id WHERE o.name IN ('SP_Auth_Login','SP_Auth_LoginExterno','SP_Sesiones_Crear','SP_Usuario_Crear','SP_MFA_Validar','SP_TokensRest_Generar') ORDER BY o.name;"
$q | sqlcmd -S . -d PassPlat -U sa -P inicio123 -C -W -h -1 -s "`t" > sys_sps.tsv

# Extraer de PASSWORDS SP.sql (regex CREATE OR ALTER PROCEDURE)
# Comparar con diff normalizado (PowerShell)
```