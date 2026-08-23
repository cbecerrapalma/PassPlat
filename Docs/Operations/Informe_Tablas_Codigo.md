# Informe: Tablas con columna `Codigo` en PassPlat

> Generado: Julio 2026

## Resultado: No se ejecutaron ALTER TABLE ADD Codigo en la sesión

No encontré comandos `ALTER TABLE ... ADD Codigo` ejecutados manualmente durante la sesión.
Lo que sucedió fue lo **opuesto**: los scripts seed hacían referencia a `Codigo` en tablas
que **no** tienen esa columna, y yo **corregí los scripts** para eliminarla de los INSERTs.

## 1. Tablas con Codigo en la BD actual (14)

| Tabla | Tipo | Longitud | Nulable | ¿Origen? |
|-------|------|----------|---------|----------|
| Apps | varchar | 50 | NO | PASSWORDS.sql (CREATE TABLE) |
| AudIdenExt | nvarchar | 100 | YES | PASSWORDS.sql (CREATE TABLE) |
| EmailProviders | varchar | 20 | NO | PASSWORDS.sql (CREATE TABLE) |
| EstadosMFA | varchar | 20 | NO | PASSWORDS.sql (CREATE TABLE) |
| EstadosUsr | varchar | 20 | NO | PASSWORDS.sql (CREATE TABLE) |
| Grupos | varchar | 50 | NO | PASSWORDS.sql (CREATE TABLE) |
| Modulos | varchar | 50 | NO | PASSWORDS.sql (CREATE TABLE) |
| Permisos | varchar | 50 | NO | PASSWORDS.sql (CREATE TABLE) |
| PoliticasPwd | varchar | 20 | NO | PASSWORDS.sql (CREATE TABLE) |
| ProvIden | nvarchar | 50 | NO | PASSWORDS.sql (CREATE TABLE) |
| Roles | varchar | 20 | NO | PASSWORDS.sql (CREATE TABLE) |
| Tenants | varchar | 20 | NO | PASSWORDS.sql (CREATE TABLE) |
| TiposCambioPwd | varchar | 20 | NO | PASSWORDS.sql (CREATE TABLE) |
| TiposModulo | varchar | 20 | NO | PASSWORDS.sql (CREATE TABLE) |

**Todas las 14 tablas ya tenían `Codigo` definido dentro del `CREATE TABLE` en PASSWORDS.sql.
Ninguna lo obtuvo mediante ALTER TABLE.**

## 2. Tablas que NO tienen Codigo (y el seed lo asumía incorrectamente)

| Tabla | ¿Seed hacía INSERT con Codigo? | Acción |
|-------|-------------------------------|--------|
| EstIdenExt | SI | Seed corregido: se eliminó Codigo del INSERT |
| ConfProvIden | SI (Nombre, EsConfiguracionCompleta) | Seed corregido: columnas inexistentes eliminadas |

### 2a. EstIdenExt

- **PASSWORDS.sql** define la tabla SIN columna `Codigo` (columnas: Id, Nombre, Descripcion, Color, Orden, Activo, FecCrea, FecMod)
- El seed original `01_Estados.sql` insertaba con `(Codigo, Nombre, Descripcion, Orden, Activo)`
- **Fix**: Se eliminó `Codigo` del INSERT y se ajustó a `(Nombre, Descripcion, Orden, Activo)`

### 2b. ConfProvIden

- **No tiene** columna `Nombre` ni `EsConfiguracionCompleta`
- El seed `05_OAuth.sql` original insertaba con esas columnas
- **Fix**: Reescribir usando columnas reales del schema actual (ver seed corregido)

## 3. Migraciones con ALTER TABLE (todas las columnas)

La única migración que agrega `Codigo` es:

| Migración | Tabla | Columna | Tipo |
|-----------|-------|---------|------|
| FASE16_Etapa12_Auditoria_Extendida.sql | AudIdenExt | Codigo | nvarchar(100) NULL |

Pero PASSWORDS.sql ya la define con Codigo, por lo que al ejecutarse sobre DB nueva es no-op.

Otras migraciones agregan columnas **no-Codigo**:

| Migración | Tabla | Columna |
|-----------|-------|---------|
| FASE14_V3_Columnas_Faltantes.sql | ProvIden | Protocolo |
| FASE15_HybridUser_SecurityFixes.sql | Usuarios | TienePasswordLocal |
| FASE16_Etapa12_Auditoria_Extendida.sql | AudIdenExt | TraceId |
| FASE16_Etapa6_Dispositivos.sql | Disp | CantidadLogins |
| FASE16_Identity_Enterprise.sql | IdenExt | IdEstado |
| FASE16_ModelImprovement_Providers.sql | HistorialPwd | OrigenRegistro |
| FASE17_OAuth2_Certification.sql | ConfProvIden | AuthorizationEndpoint, TokenEndpoint, JwksUri, Issuer |
| FASE17.3.3_ConfProvIden_Missing_Columns.sql | ConfProvIden | AllowLoginWithoutRefreshToken, etc. |
| FASE17.3.3_Version_Metadata.sql | ProvIden | Version |

## 4. Conclusión

- **No se ejecutó ningún ALTER TABLE ADD Codigo manual.** El esquema actual es el resultante de ejecutar PASSWORDS.sql (con Codigo ya definido en 14 tablas) + migraciones incrementales (ninguna que añada Codigo a tablas nuevas).
- **Las correcciones fueron sobre los scripts seed**, no sobre la base de datos. Los scripts se adaptaron al schema real de la BD, no al revés.
