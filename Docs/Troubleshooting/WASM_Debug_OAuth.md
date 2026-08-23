# WASM Debug + OAuth — Troubleshooting

## Síntomas

- **Release (Ctrl+F5)**: OAuth Google → Login → Dashboard. Funciona correctamente.
- **Debug (F5)**: OAuth Google → redirección → crash con stack `eval(eval(eval(...)))` — error en el runtime JS del depurador WASM.
- El crash ocurre **después** de la redirección externa (Google → callback local).
- El error no aparece en código C# ni en lógica de la aplicación.

## Versiones

| Componente | Versión |
|---|---|
| .NET SDK | 10.0.203 |
| Runtime | 10.0.7 |
| Blazor WASM DevServer | 10.0.9 |
| Blazor WASM packages | 10.0.9 |
| VS / Editor | (pendiente) |

## Configuración de launchSettings

Archivo: `PassPlat.Web/Properties/launchSettings.json`

### Perfiles disponibles

| Perfil | inspectUri | Uso |
|--------|-----------|-----|
| `http` | ✅ | Desarrollo HTTP |
| `https` | ✅ | Desarrollo HTTPS normal |
| `https-oauth` | ✅ | Depuración completa OAuth |
| `https-oauth-no-wasm-debug` | ❌ | Pruebas OAuth sin WASM Debug Proxy |

## Pruebas realizadas

### Fase 17.6.1 — Sin modificar código

| # | Prueba | Navegador | Perfil | Hot Reload | Breakpoints | DevTools abierto | Resultado |
|---|--------|-----------|--------|------------|-------------|------------------|-----------|
| 1 | Desactivar Hot Reload + F5 | - | https | OFF | NO | - | (pendiente) |
| 2 | F5 sin breakpoints | - | https | - | NO | - | (pendiente) |
| 3 | Login desde Edge | Edge | https | - | - | - | (pendiente) |
| 4 | Login desde Chrome | Chrome | https | - | - | - | (pendiente) |
| 5 | Ventana InPrivate/Incógnito | - | https | - | - | - | (pendiente) |

### Fase 17.6.2 — Con cambios de configuración

| # | Prueba | Perfil | Cambio | Resultado |
|---|--------|--------|--------|-----------|
| 6 | `BlazorWebAssemblyJSDebugging: false` | https-oauth | Variable entorno | (pendiente) |
| 7 | Perfil sin inspectUri | https-oauth-no-wasm-debug | Sin WASM proxy | (pendiente) |

## Resultado

(Completar tras cada prueba)

- **Solución adoptada**: (pendiente)
- **¿Es limitación del runtime?**: (pendiente — verificar issues dotnet/aspnetcore)
- **Referencias**: (pendiente — enlazar issues relevantes)

## Issues relacionados (dotnet/aspnetcore)

- (pendiente de búsqueda — buscar "OAuth redirect WASM debug proxy .NET 10")

## Conclusión

(Completar al finalizar el sprint)
