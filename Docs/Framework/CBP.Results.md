# CBP.Results Reference

**Location**: `D:\CODIGOS\CBP\CBP.Core\CBP.Results\`

## Overview

Fluent error-handling framework avoiding exceptions for control flow. All data access operations return `Result<T>`.

## Core Types

### `Result<T>`

| Member | Description |
|--------|-------------|
| `IsSuccess` | True if operation succeeded |
| `IsFailure` | True if operation failed |
| `Value` | The result value (access only if IsSuccess) |
| `Error` | The error object (access only if IsFailure) |

### Factory Methods

```csharp
Result<T>.Success(value)                 // value cannot be null for reference types
Result<T>.Success(value, allowNull: true) // allows null value
Result<T>.Failure(Error error)           // with Error object
Result<T>.Failure(string code, string message)  // creates Error internally
Result.Success()                         // void success
Result.Failure(string code, string message)     // void failure
```

### `Error`

```csharp
// Properties
string Code { get; }
string Message { get; }
ErrorType Type { get; }
int HttpStatus { get; }

// Fluent creation
Error.FromException(ex, "CODE")
    .WithDetail("Operation", "MethodName")
    .WithDetail("EntityType", "EntityName")
    .WithType(ErrorType.Validation)
    .WithHttpStatus(400);
```

## Common Error Codes

- `"DATABASE_ERROR"` — Database exception
- `"SP_EXECUTION_ERROR"` — Stored procedure exception
- `"SP_NO_RESULT"` — SP returned no results
- `"NOT_FOUND"` — Entity not found
- `"VALIDATION_ERROR"` — Validation failure

## Null Handling

When passing `result.Error` to `Result<T>.Failure()`:
```csharp
return Result<T>.Failure(result.Error!);  // null-forgiving is safe
// Only call when result.IsFailure is confirmed true
```

## Result Propagation Chain

El patrón `CBP.Results` se propaga a través de las 4 capas de la solución:

```
DB (EF/SP) → Repositorio (Result<T> + try-catch DB_ERROR)
           → Servicio     (IsFailure check + propagación)
           → Controlador  (FromResult/FromResultQuery → ProblemDetails RFC 7807)
           → UI Blazor    (ApiClient.LastError → Snackbar.Severity.Error)
```

### Reglas por capa:

| Capa | Regla |
|------|-------|
| **Datos** | Todo método público asíncrono retorna `Task<Result<T>>` (nunca tipos raw: `T?`, `List<T>`, `int`, `bool`). Las operaciones async se envuelven en try-catch con código `"DB_ERROR"`. |
| **Aplicación** | Todo método público retorna `Result<T>`. Antes de usar `result.Value` de un repositorio, se verifica `result.IsFailure` y se propaga con `return Result<T>.Failure(result.Error!)`. |
| **WebAPI** | Controladores heredan `BaseApiController`. Usan `FromResult(result)`, `FromResultQuery(result)` o `CreatedFromResult(action, routeValues, result)` para convertir `Result<T>` → `IActionResult` con `ProblemDetails` en caso de error. |
| **UI Web** | `ApiClient.SendAsync` captura el `detail` del `ProblemDetails` en `Api.LastError`. Todo `Snackbar.Add("Error al...", Severity.Error)` debe incluir `Api.LastError` como fallback: `Snackbar.Add(Api.LastError ?? "Error al crear", Severity.Error)`. |
