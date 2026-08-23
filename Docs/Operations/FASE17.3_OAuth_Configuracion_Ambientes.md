# FASE 17.3 — Configuración OAuth por Ambiente

## Fuente de datos

Datos extraídos exclusivamente de la configuración oficial del proyecto:

| Archivo | Contenido |
|---------|-----------|
| `PassPlat.WebAPI/Properties/launchSettings.json` | `"https://localhost:5001;http://localhost:5259"` |
| `PassPlat.Web/Properties/launchSettings.json` | `"https://localhost:7275;http://localhost:5273"` |
| `PassPlat.Web/wwwroot/appsettings.json` | `"ApiBaseUrl": "https://localhost:5001"` |
| `PassPlat.WebAPI/Controllers/ExternalAuthController.cs` | `[Route("api/auth/externo")]`, callback `GET {provider}/callback` |
| `PassPlat.Web/wwwroot/appsettings.json` | ApiBaseUrl: `https://localhost:5001` |

---

## Development

### Puertos oficiales (inmutables)

| Componente | Protocolo | URL |
|------------|----------|-----|
| Web (Blazor) | HTTPS | `https://localhost:7275` |
| Web (Blazor) | HTTP | `http://localhost:5273` |
| WebAPI | HTTPS | `https://localhost:5001` |
| WebAPI | HTTP | `http://localhost:5259` |

### OAuth Endpoints

| Parámetro | Valor |
|-----------|-------|
| Authorized JavaScript Origins | `https://localhost:7275` |
| Redirect URI (Google) | `https://localhost:5001/api/auth/externo/GOOGLE/callback` |
| Redirect URI (genérico) | `https://localhost:5001/api/auth/externo/{PROVIDER}/callback` |
| Frontend Callback | `https://localhost:7275/signin-callback` |
| API Authorize endpoint | `https://localhost:5001/api/auth/externo/{provider}/authorize` |
| Cookie Domain | `localhost` |

### Google Cloud Console — Configuración Development

| Campo | Valor |
|-------|-------|
| Application type | Web application |
| Authorized JavaScript Origins | `https://localhost:7275` |
| Authorized Redirect URIs | `https://localhost:5001/api/auth/externo/GOOGLE/callback` |
| Scopes | `openid`, `email`, `profile` |
| OAuth consent screen | External (testing) |

> **Importante**: El Redirect URI debe coincidir **exactamente** entre Google Cloud Console y `ConfProvIden.Callback`. Mayúsculas, barras, puertos — todo debe ser idéntico.

---

## Testing (propuesta)

| Componente | URL |
|------------|-----|
| Web | `https://test.passplat.example.com` |
| WebAPI | `https://api-test.passplat.example.com` |
| OAuth Redirect URI | `https://api-test.passplat.example.com/api/auth/externo/{PROVIDER}/callback` |
| JavaScript Origin | `https://test.passplat.example.com` |
| Cookie Domain | `.passplat.example.com` |

---

## Staging (propuesta)

| Componente | URL |
|------------|-----|
| Web | `https://staging.passplat.example.com` |
| WebAPI | `https://api-staging.passplat.example.com` |
| OAuth Redirect URI | `https://api-staging.passplat.example.com/api/auth/externo/{PROVIDER}/callback` |
| JavaScript Origin | `https://staging.passplat.example.com` |
| Cookie Domain | `.passplat.example.com` |

---

## Production (temporal — reemplazar al definir dominio real)

| Componente | URL |
|------------|-----|
| Web | `https://passplat.example.com` |
| WebAPI | `https://api.passplat.example.com` |
| OAuth Redirect URI | `https://api.passplat.example.com/api/auth/externo/{PROVIDER}/callback` |
| JavaScript Origin | `https://passplat.example.com` |
| Cookie Domain | `.passplat.example.com` |
| Callback Blazor | `https://passplat.example.com/signin-callback` |

> Los dominios `*.example.com` están reservados por RFC 2606 para documentación. Reemplazar cuando se definan los dominios reales.

---

## Reglas Permanentes (AGENTS.md)

### 22. Puertos OAuth Development inmutables

Los puertos de desarrollo para Web (`7275`, `5273`) y WebAPI (`5001`, `5259`) forman parte del contrato de integración OAuth. Está prohibido modificarlos sin actualizar simultáneamente:

- `PassPlat.Web/Properties/launchSettings.json`
- `PassPlat.WebAPI/Properties/launchSettings.json`
- `PassPlat.Web/wwwroot/appsettings.json` (ApiBaseUrl)
- `ConfProvIden.Callback` (BD)
- Documentación OAuth (`Docs/FASE17.3_OAuth_Configuracion_Ambientes.md`)
- Google Cloud Console (o cualquier proveedor OAuth registrado)
- Cualquier otro proveedor OAuth configurado que use Redirect URIs

### 23. RedirectUri desde BD, nunca desde HttpContext

Todas las RedirectUri utilizadas por proveedores OAuth deben provenir exclusivamente de `ConfProvIden.Callback`. Está prohibido construir RedirectUri utilizando `Request.Host`, `Request.Scheme`, `Request.PathBase` o cualquier miembro de `HttpContext`.

### 24. Callback único server-side

Todo callback OAuth debe ser manejado por la WebAPI (`GET /api/auth/externo/{provider}/callback`). No crear callbacks en Blazor. El flujo final es: Blazor → API authorize → Proveedor → API callback → JWT interno → Blazor.

---

## Tabla resumen multi-ambiente

| Ambiente | Web | WebAPI | JavaScript Origin | Redirect URI (Google) |
|----------|-----|--------|-------------------|----------------------|
| **Development** | `https://localhost:7275` | `https://localhost:5001` | `https://localhost:7275` | `https://localhost:5001/api/auth/externo/GOOGLE/callback` |
| **Testing** | `https://test.passplat.example.com` | `https://api-test.passplat.example.com` | `https://test.passplat.example.com` | `https://api-test.passplat.example.com/api/auth/externo/GOOGLE/callback` |
| **Staging** | `https://staging.passplat.example.com` | `https://api-staging.passplat.example.com` | `https://staging.passplat.example.com` | `https://api-staging.passplat.example.com/api/auth/externo/GOOGLE/callback` |
| **Production** | `https://passplat.example.com` | `https://api.passplat.example.com` | `https://passplat.example.com` | `https://api.passplat.example.com/api/auth/externo/GOOGLE/callback` |
