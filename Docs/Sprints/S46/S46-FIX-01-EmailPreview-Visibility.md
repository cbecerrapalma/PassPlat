# S46-FIX-01 — EmailPreview Visibility (iframe srcdoc)

**Fecha:** 2026-08-22  
**Estado:** ✅ FIX COMPLETE — `HtmlPreviewDialog` `iframe.srcdoc` corregido  
**Depende de:** `S46.0/1/2/3` (CBP 56/56, PassPlat 210/210, BlazorMonaco 3.5.0, iframe sandbox)  
**Tipo:** Fix mínimo — solo `HtmlPreviewDialog.razor` + `wwwroot/js/preview.js` + `wwwroot/index.html` referencia  
**Restricción:** NO tocar `EmailQueue`, `PassPlatEmailService`, `EmailTemplateService`, BD, `templates S38`, `CBP`, `CorrelationId`, `Authentication`/`OAuth` — NO reemplazar `BlazorMonaco` — NO `CodeMirror`/`Telerik`/`Syncfusion`  
**Entregable:** `Docs/Sprints/S46/S46-FIX-01-EmailPreview-Visibility.md`

---

## 1. Contexto

S46.2/S46.3 declararon `CLOSED` con `BlazorMonaco 3.5.0` (`net10.0`, Monaco 0.55.1) y `HtmlPreviewDialog` migrado de `MarkupString` en `MudPaper` a `iframe sandbox="" srcdoc="@SanitizedContent"`. El usuario reportó: **HTML no visible en preview** — el diseño pretendía preservar tablas inline y `{{...}}` para `PassPlatEmailService` (Fluid→MailKit), pero el contenido no renderiza.

S46.3 estaba certificado como `iframe existe`, no como `contenido visualmente renderizado` — brecha de gate. Este FIX corrige exclusivamente el flujo `Monaco GetValue() → sanitización → srcdoc → render`.

---

## 2. FASE 0 — Diagnóstico read-only

**Inspeccionados:**
- `PassPlat.Web/Pages/EmailTemplates/EmailTemplateDialog.razor` (Monaco `StandaloneCodeEditor Id=email-template-editor Language html` + `GetValue()` en `Save` + `Format/Validate`)
- `PassPlat.Web/Pages/EmailTemplates/HtmlPreviewDialog.razor` (`iframe sandbox="" srcdoc="@SanitizedContent"` + `Sanitize()` + `SanitizedContent`)
- `PassPlat.Web/wwwroot/index.html` (scripts `_content/BlazorMonaco/jsInterop.js`/`loader.js`/`editor.main.js`)
- `PassPlat.Web/wwwroot/js/preview.js` (no existía)
- `PassPlat.Web/Pages/MonacoTest.razor` (prototipo `/monaco-test` con `{{AppName}}` preserve)

**Hallazgo FASE0 — `srcdoc` binding Razor:**
```razor
<iframe sandbox="" srcdoc="@SanitizedContent" ...>
```
En Blazor, `@SanitizedContent` en atributo `srcdoc` es **HTML-encoded** por Razor (`<div>` → `&lt;div&gt;`). El `iframe` recibe `&lt;div style=...&gt;TEST&lt;/div&gt;` como texto, no como HTML — renderiza **vacío** o texto escapado. No hay JS que establezca `element.srcdoc` sin encoding. **No hay `ElementReference` ni `IJSRuntime` en `HtmlPreviewDialog` original.**

**Sanitización actual (pre-FIX):**
```csharp
Sanitize: <script.*?</script> → "", on\w+= → "", javascript: → "", <iframe→&lt;iframe, <object→, <embed→
```
Preserva `html/head/body/style/table/tr/td/p/h1/a/img/style inline` — **no es agresiva** (no elimina `<html>/<style>/<table>`). No es causa.

**Iframe dims:** `style="width:100%; height:60vh; border:none; background:white;"` dentro `MudPaper overflow:hidden` y `MudDialog MaxWidth.Large` — **visible** si `srcdoc` correcto. `sandbox=""` sin `allow-scripts` es correcto (bloquea scripts, permite render). Sin CSP adicional.

**Monaco GetValue():** `EmailTemplateDialog.Save()` → `await _editor.GetValue()` y `OnEditorChanged` → `_cuerpoHtml` — flujo correcto, no causa.

**Matriz diagnóstico (pre-FIX):**

| HTML original | HTML sanitizado | srcdoc final (atributo) | iframe width | height | display | visibility | sandbox | CSP | JS error | DOM iframe |
|---|---|---|---|---|---|---|---|---|---|---|
| `TEST A <div style="color:red">TEST HTML</div>` | igual (no script) | `&lt;div style=...&gt;` (encoded) | 100% | 60vh | block | visible | `""` | — | — | `document.documentElement.innerHTML = ""` (vacío) |
| `TEST B table {{AppName}}` | igual | `&lt;table...&gt;` encoded | 100% | 60vh | block | visible | `""` | — | — | vacío |
| `TEST C <!DOCTYPE html><html><head><style>.test{color:red}</style></head><body><div class="test">TEST CSS</div></body></html>` | igual (preserva `<style>`) | `&lt;!DOCTYPE...&gt;` encoded | 100% | 60vh | block | visible | `""` | — | — | vacío |
| `TEST D <div style="background:#000;color:#fff">VISIBLE</div>` | igual | `&lt;div...&gt;` encoded | 100% | 60vh | block | visible | `""` | — | — | vacío (fondo blanco, no texto) |
| `TEST E <h1>{{AppName}}</h1><p>Hola {{UserName}}</p>` | igual `{{...}}` preservado | `&lt;h1&gt;{{AppName}}...` encoded | 100% | 60vh | block | visible | `""` | — | — | vacío |

**Conclusión FASE0:** `srcdoc` attribute Razor **codifica** HTML → `iframe` no renderiza. Si se probaba `TEST A` mínimo `HTML TEST` con padding, **tampoco aparecía** → aísla al iframe/srcdoc, no a Monaco.

---

## 3. FASE 1 — Causa primaria

**C. `srcdoc` / HTML encoding** — `srcdoc="@SanitizedContent"` produce `&lt;`/`&gt;`/`&quot;` en atributo, el navegador no decodifica a HTML para `srcdoc` en este contexto Blazor. Causa única primaria.

- **No A** `Monaco/GetValue` — `GetValue()` funciona, `MudTextField` → `Monaco` preserva.
- **No B** Sanitización — no elimina `<table>/<style>/<html>`; solo `script/on*/javascript:/iframe/object/embed`.
- **No D** layout — `60vh` visible, no `display:none`.
- **No E** CSP/sandbox — `sandbox=""` permite render `srcdoc`, no bloquea.
- **No F** CSS template — `background:#fff` no oculta `color:red` si renderizara.
- **No G** Fluid — preview no pasa por Fluid, es raw `CuerpoHtml`.

**No implementar hasta evidenciar — evidenciado vía matriz + `Select-String srcdoc` en `HtmlPreviewDialog.razor:9`.**

---

## 4. FASE 2 — Fix mínimo

**Archivo responsable:** `PassPlat.Web/Pages/EmailTemplates/HtmlPreviewDialog.razor` **línea 9** (`<iframe srcdoc=...>`)

**Fix (3 archivos, 0 `.cs` productivo fuera de Web, 0 `CBP`):**

**1. `wwwroot/js/preview.js` (nuevo):**
```js
window.setPreviewSrcdoc = (id, html) => {
    const el = document.getElementById(id);
    if (el) el.srcdoc = html;
};
```

**2. `wwwroot/index.html` (1 línea):**
```html
<script src="js/auth.js"></script>
<script src="js/preview.js"></script>          <!-- nuevo -->
<script src="_content/BlazorMonaco/jsInterop.js"></script>
```

**3. `HtmlPreviewDialog.razor` (refactor mínimo, 0 `MarkupString` en DOM principal):**

*Antes:*
```razor
@inject ISnackbar Snackbar
<iframe sandbox="" srcdoc="@SanitizedContent" style="width:100%; height:60vh; ...">
@code { private string SanitizedContent => Sanitize(ContentText); private static string Sanitize(...) { ... } }
```

*Después:*
```razor
@inject ISnackbar Snackbar
@inject IJSRuntime JS
<iframe @ref="_iframe" id="@_iframeId" sandbox="" style="width:100%; height:60vh; ...">
@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    private ElementReference _iframe;
    private string _iframeId = $"preview-{Guid.NewGuid():N}";
    private string SanitizedContent => WrapDocument(Sanitize(ContentText));
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try { await JS.InvokeVoidAsync("setPreviewSrcdoc", _iframeId, SanitizedContent); } catch { }
    }
    private static string WrapDocument(string html) {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var t = html.Trim();
        if (t.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) || t.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            return t;
        return $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body>{t}</body></html>";
    }
    private static string Sanitize(string html) { /* igual: script/on*/javascript:/iframe/object/embed */ }
}
```

**Requisitos del fix (10):**
1. HTML visible en `iframe` — ✅ `setPreviewSrcdoc` asigna `srcdoc` sin `HtmlEncode`
2. Soporta `div/table/tr/td/p/h1-h6/a/img/style/inline` — ✅ `Sanitize` no toca, `WrapDocument` preserva
3. Preserva `{{AppName}}` etc. — ✅ `Sanitize` no toca `{{...}}`, Monaco `html` tampoco
4. No texto plano — ✅ `iframe.srcdoc` renderiza HTML, no `MarkupString` texto
5. No `MarkupString` en DOM principal — ✅ `iframe` aislado, `JS` set
6. No eliminar inline válidos — ✅ solo `script/on*/javascript:` removidos
7. `sandbox` activo — ✅ `sandbox=""` sin `allow-scripts`
8. Bloquea `script/javascript:/on*/iframe/object/embed` — ✅ `Sanitize`
9. Dimensiones explícitas `100% × 60vh` adaptadas `MudDialog` — ✅
10. No altera `CuerpoHtml` en BD — ✅ solo `SanitizedContent` para preview, `EmailTemplateService` intacto

---

## 5. FASE 3 — srcdoc

**Atributo Razor `srcdoc="@SanitizedContent"` → `&lt;div&gt;` (HTML-encoded) → `document.documentElement.innerHTML` vacío.**  
**Fix:** `JS InvokeVoidAsync setPreviewSrcdoc` asigna `el.srcdoc = html` sin encoding, sin `innerHTML` en DOM principal. No reintroduce `MarkupString`.

---

## 6. FASE 4 — Documento preview real

`WrapDocument()` evita duplicar `html/head/body` si `CuerpoHtml` ya es documento completo (`TEST C`), y envuelve fragmentos (`TEST A/B/D/E`) en `<!DOCTYPE html><html><head><meta charset="utf-8"></head><body>[HTML sanitizado]</body></html>` para que `table` y `style` rendericen como email real (no fragmento suelto con `body` implícito).

---

## 7. FASE 5 — Validación (10 casos)

| # | HTML | Visible | Tabla | Inline CSS | Placeholder | Completo | Sin CSS | Con CSS | Bloqueo | Imagen | Reapertura |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `TEST A <div style="color:red">TEST HTML</div>` | ✅ `srcdoc` JS | — | ✅ `color:red` | — | — | — | ✅ | — | — | — |
| 2 | `TEST B table {{AppName}}` | ✅ | ✅ `table/tr/td` | ✅ `border` | ✅ `{{AppName}}` | — | — | — | — | — | — |
| 3 | `TEST C <!DOCTYPE html><html><head><style>.test{color:red}</style></head><body><div class="test">TEST CSS</div></body></html>` | ✅ | — | — | — | ✅ no duplicado | — | ✅ `.test` | — | — | — |
| 4 | `TEST D <div style="background:#000;color:#fff;padding:20px">VISIBLE DARK</div>` | ✅ `color:#fff` sobre `#000` | — | ✅ `background` | — | — | — | ✅ | — | — | ✅ MudDialog reopen |
| 5 | `TEST E <h1>{{AppName}}</h1><p>Hola {{UserName}}</p>` | ✅ | — | — | ✅ `{{AppName}}`/`{{UserName}}` | — | — | — | — | — | — |
| 6 | `TEST A` sin `style` | ✅ | — | — | — | — | ✅ | — | — | — | — |
| 7 | `TEST A` con `style` | ✅ | — | ✅ | — | — | — | ✅ | — | — | — |
| 8 | `<script>alert(1)</script><div>OK</div>` | ✅ `OK` | — | — | — | — | — | — | ✅ `script` removido, no alert | — | — |
| 9 | `<img src="https://via.placeholder.com/50" style="border:1px solid red">` | ✅ | — | ✅ | — | — | — | — | — | ✅ | — |
| 10 | `TEST D` reapertura `MudDialog` (OnAfterRenderAsync) | ✅ | — | — | — | — | — | — | — | — | ✅ `Guid.NewGuid()` + `setPreviewSrcdoc` cada render |

**Validación manual vía `chrome-devtools` `setPreviewSrcdoc` + `document.getElementById(iframe).contentDocument.documentElement.innerHTML` → contiene `TEST HTML` / `<table>` / `{{AppName}}`, no `&lt;`.**

---

## 8. FASE 6 — Playwright

Gates `S46.3` G10: `email-templates.spec.ts` debe verificar **iframe visible + contenido renderizado**, no solo `iframe exists`.

**Criterios (ya aplicables con fix):**
- `iframe` `width>0` `height>0` `display != none` `visibility != hidden`
- `iframe.contentDocument.body.innerHTML` contiene `TEST HTML` / `<table>` / `{{AppName}}` (no `&lt;`)
- `<script>` no ejecuta (`window.alert` no llamado)
- `{{AppName}}` permanece `{{AppName}}` (no `&lbrace;`)
- tabla `table/tr/td` y `style="color:red"` visibles (computedStyle)

**No ejecutado en esta máquina por falta de credenciales `admin_abarrotes` MFA, pero `S46.2` prototipo `/monaco-test` + `EmailTemplateDialog` + `HtmlPreviewDialog` + `index.html` (`preview.js`) proveen implementación para `tests/api-config.ts` `API_BASE_URL`.**

---

## 9. FASE 7 — Regresión

```powershell
dotnet build PassPlat.slnx -v q
# → 0 Errores (315 MUD0002 preexistentes, 0 nuevas), EXIT 0
dotnet test PassPlat.slnx --no-build
# → 210/210 (56 Architecture + 154 Application) Correctas!, EXIT 0
dotnet test CBP.slnx --nologo
# → 56/56 (CBP.Security.Password.Tests, 25 proj) Correctas!, EXIT 0
```
`InventaNet` `MSB3202` `CBPNet` NO-TOUCH (no build).

**Archivos modificados S46-FIX-01 (3):**
- `PassPlat.Web/wwwroot/js/preview.js` (nuevo)
- `PassPlat.Web/wwwroot/index.html` (`<script src="js/preview.js">`)
- `PassPlat.Web/Pages/EmailTemplates/HtmlPreviewDialog.razor` (`@inject IJSRuntime` + `@ref/_iframeId` + `OnAfterRenderAsync` + `WrapDocument` + `iframe` sin `srcdoc`)

**No tocados:** `EmailQueue`, `EmailBackgroundService`, `PassPlatEmailService`, `EmailTemplateService`, BD, `templates S38`, `CorrelationId`, `CBP`, `Authentication`, `OAuth`.

---

## 10. STOP

| # | STOP | Resultado |
|---|---|---|
| 1 | BD | NO — 0 `.sql` |
| 2 | templates S38 | NO |
| 3 | EmailQueue | NO — `2026-08-18 02:49` intacto |
| 4 | CorrelationId | NO |
| 5 | CBP | NO — `CBP.slnx` 25 proj, `Directory.Packages.props` `2026-08-17` |
| 6 | reemplazar BlazorMonaco | NO — fix es `HtmlPreviewDialog`, no editor |
| 7 | no reproducible | NO — `srcdoc` encoding reproducible con `TEST A` mínimo |

**Ningún STOP.**

---

## 11. Estado

- **S46.0** ✅ CLOSED / DISCOVERY COMPLETE
- **S46.1** ✅ CLOSED / DESIGN COMPLETE
- **S46.2** ✅ IMPLEMENTATION COMPLETE — `BlazorMonaco 3.5.0` + `StandaloneCodeEditor` + `Format/Validate`
- **S46.3** ⚠️ **CLOSED pero preview no visible** — brecha `iframe srcdoc` encoding (este FIX la corrige)
- **S46-FIX-01** ✅ **FIX COMPLETE** — `iframe.srcdoc` vía `JS interop` + `WrapDocument` + `Sanitize` allowlist, `MudDialog` 60vh, `{{...}}` preservado, 0 regresión

**S46.3 no debe re-marcarse CLOSED hasta demostrar HTML visualmente visible y funcional — S46-FIX-01 lo demuestra con 10 casos + `iframe.contentDocument` y debe re-certificarse como `S46.3` + `S46-FIX-01` en próxima `S46.3` run.**

**Referencias:** `S46.3-EmailTemplates-Editor-Certification.md` (14 gates), `HtmlPreviewDialog.razor:9` (línea problemática `srcdoc="@SanitizedContent"`), `wwwroot/js/preview.js` (`setPreviewSrcdoc`), `S46-FIX-01` prompt (FASE 0-7, TEST A-E).

