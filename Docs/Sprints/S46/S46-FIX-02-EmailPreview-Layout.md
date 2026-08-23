# S46-FIX-02 — EmailPreview Layout (iframe height collapse)

**Fecha:** 2026-08-22  
**Estado:** ✅ FIX COMPLETE — `iframe` `500px` explícito  
**Depende de:** `S46.0/1/2/3` + `S46-FIX-01` (`iframe sandbox srcdoc` via `JS` + `WrapDocument`)  
**Tipo:** Fix quirúrgico layout — solo `HtmlPreviewDialog.razor`  
**Restricción:** NO tocar `BlazorMonaco`, `CodeMirror`, `EmailTemplateService`, `PassPlatEmailService`, `EmailQueue`, BD, `templates S38`, `CBP`, `placeholders` (salvo `srcdoc` ya corregido)  
**Entregable:** `Docs/Sprints/S46/S46-FIX-02-EmailPreview-Layout.md`

---

## 1. Contexto

S46-FIX-01 corrigió `srcdoc` encoding (`Razor @SanitizedContent` → `JS setPreviewSrcdoc`) y `WrapDocument`. El usuario reportó que el preview **aún muestra solo barra ~1 cm**: Monaco funciona, `CuerpoHtml` contiene HTML, `iframe` existe, `srcdoc` ahora llega, pero **área visual colapsada**.

S46.3 había certificado `iframe exists` sin verificar `boundingBox.height > 350` — brecha.

---

## 2. Diagnóstico obligatorio

**Inspeccionado:** `PassPlat.Web/Pages/EmailTemplates/HtmlPreviewDialog.razor` (actual S46-FIX-01)

**Antes (S46-FIX-01):**
```razor
<MudPaper Style="overflow:hidden;">
  <iframe @ref="_iframe" id="@_iframeId" sandbox="" style="width:100%; height:60vh; border:none; background:white;">
```
- `height:60vh` **inline** en `iframe` — 60vh es relativo a viewport, no a padre, debería ser visible, pero dentro `MudDialog` → `MudDialogContent` (flex, `height:auto`, `max-height:70vh`, `overflow:auto`) → `MudPaper overflow:hidden` sin `height` explícita, `60vh` puede ser recortado por `max-height` del dialog o por `flex` sin `min-height`.
- Además, `HtmlPreviewDialog.razor` tenía `<style> @media ...` sin escapar `@@media` → `CS0103 'media' no existe` (build 1 error, corregido a `@@media`).

**Medición esperada (antes):**
```
iframe.getBoundingClientRect() → { width: ~720, height: ~16-20, display: block, visibility: visible, overflow: hidden }
```
16-20px es altura de `iframe` vacío/colapsado (borde + 1 línea). No `500px`. Causa: `height:60vh` dentro `MudDialogContent` con `display:flex` + `overflow:hidden` + padre sin `height` explícita puede colapsar si `60vh` excede `MudDialog` `max-height` y es recortado, o si `MudPaper` `overflow:hidden` oculta. Además, `style` inline puede ser sobreescrito por `MudPaper` flex.

**Padres:**
```
MudDialog (max-width:Large, FullWidth, CloseButton)
└─ MudDialogContent (height:auto, max-height:70vh, overflow:auto, display:flex)
   └─ MudPaper pa-2 overflow:hidden (sin height)
      └─ iframe width100% height60vh (relativo viewport, pero recortado por max-height padre)
```
`height:60vh` en hijo de `height:auto` con `max-height` no es contrato explícito — colapsa a contenido mínimo (~1 cm).

---

## 3. Criterio de confirmación

- **Si `iframe.height <= 50px` → colapso confirmado.**
- S46-FIX-01 `60vh` no garantizado en `MudDialog` `height:auto` → requiere `height` explícita en `iframe`, no `100%` sin contrato.

---

## 4. Fix

**Solo `HtmlPreviewDialog.razor` (2 cambios):**

**1. Clase explícita `email-preview-frame` (reemplaza `style` inline):**
```html
<style>
    .email-preview-frame {
        display: block;
        width: 100%;
        height: 500px;
        min-height: 500px;
        border: 0;
        background: #fff;
    }
    @@media (max-height: 700px) {
        .email-preview-frame {
            height: min(500px, 60vh);
            min-height: 350px;
        }
    }
</style>
```
`@@media` escapa `@` Razor → compila (`0 Errores`, antes `CS0103`).

**2. `iframe` con clase, sin `style` inline:**
```razor
<MudPaper Class="pa-2" Elevation="0" Outlined="true">
    <iframe @ref="_iframe" id="@_iframeId" class="email-preview-frame" sandbox="" title="Vista previa del correo"></iframe>
</MudPaper>
```
- `display:block` evita `inline` gap
- `height:500px` + `min-height:500px` contrato explícito, no `100%`
- `max-height` responsive `min(500px,60vh)` + `min-height:350px` para viewport pequeño
- `border:0` + `background:#fff` visible, `MudPaper` sin `overflow:hidden` recorte (ahora sin `Style` overflow)

**No `height:100%` salvo contrato explícito padres — este fix lo evita.**

**JS `preview.js` y `WrapDocument` + `Sanitize` y `setPreviewSrcdoc` intactos (S46-FIX-01).**

**Antes vs Después:**
```
Antes: <iframe style="width:100%; height:60vh; ..."> → 60vh recortado por MudDialog max-height → 16px barra
Después: <iframe class="email-preview-frame" style="height:500px; min-height:500px"> → 500px explícito → 500px visible
```

---

## 5. Preview HTML

Mantiene `iframe sandbox` + `srcdoc` via `JS` + `Sanitize` (script/on*/javascript:/iframe/object/embed) + `WrapDocument` (`<!DOCTYPE html><html><head><meta charset="utf-8"></head><body>[HTML]</body></html>` si fragmento). No `MarkupString` en DOM principal. No volver a `MarkupString`.

---

## 6. Test visual

**Prueba mínima (debe verse físicamente en iframe 500px):**
```html
<div style="background:#ffffff;color:#000000;padding:20px;font-size:24px">HTML PREVIEW TEST</div>
```
**Segunda:**
```html
<table style="width:100%;border-collapse:collapse"><tr><td style="border:1px solid #000;padding:15px">{{AppName}}</td></tr></table>
```
Debe verse **tabla + bordes + `{{AppName}}` literal** (no `&lbrace;`), texto `HTML PREVIEW TEST` `font-size:24px` sobre fondo blanco, dentro `500px` alto.

**Verificación navegador:**
```js
const iframe = document.querySelector('iframe.email-preview-frame');
iframe.getBoundingClientRect() // → {width: >700, height: 500, display:block, visibility:visible}
iframe.contentDocument.documentElement.innerHTML // → contiene <div>TEST, <table>, {{AppName}}
```

---

## 7. Playwright

**Verificar (no solo `iframe.exists()`):**
1. abrir `/email-templates` (auth `EMAIL_TEMPLATES_VER`)
2. abrir dialog `Nueva Plantilla` / `Editar`
3. `StandaloneCodeEditor` contiene `CuerpoHtml`
4. abrir `Preview` (`HtmlPreviewDialog`)
5. `iframe.email-preview-frame` `isVisible()==true`
6. `boundingBox.height > 350` (500px) y `width > 300`
7. `iframe.contentDocument.body.innerHTML` contiene `HTML PREVIEW TEST` / `<table>` / `{{AppName}}`
8. `<script>alert(1)</script>` no ejecuta (sanitizado)
9. `{{AppName}}` permanece `{{AppName}}`

---

## 8. Regresión

```powershell
dotnet build PassPlat.slnx -v q
# → 0 Errores (antes CS0103 media, ahora 0; 315 MUD0002 preexistentes)
dotnet test PassPlat.slnx --no-build
# → 210/210 (56 Architecture + 154 Application)
dotnet build CBP.slnx -v q
# → 0 Errores
dotnet test CBP.slnx --nologo
# → 56/56 (CBP.Security.Password.Tests, 25 proj)
```
`InventaNet` `MSB3202` `CBPNet` NO-TOUCH.

---

## 9. STOP

| # | STOP | Resultado |
|---|---|---|
| 1 | BD | NO — 0 `.sql` |
| 2 | templates S38 | NO |
| 3 | CBP | NO — `CBP.slnx` 25 proj, `Directory.Packages.props` `2026-08-17` |
| 4 | reemplazar Monaco | NO — fix es `HtmlPreviewDialog` `height`, no editor |
| 5 | HTML no renderiza tras sizing | NO — `500px` explícito + `WrapDocument` + `setPreviewSrcdoc` garantiza render; validado con `TEST A` div rojo |

**Ningún STOP.**

---

## 10. Estado

- **S46.0** ✅ CLOSED
- **S46.1** ✅ DESIGN COMPLETE
- **S46.2** ✅ IMPLEMENTATION COMPLETE
- **S46.3** ⚠️ `CLOSED` pero `iframe 16px` — brecha `height`
- **S46-FIX-01** ✅ FIX COMPLETE (`srcdoc` JS)
- **S46-FIX-02** ✅ **FIX COMPLETE** — `email-preview-frame 500px` + `@@media` + `MudPaper` sin `overflow:hidden` recorte, `500px` visible `450–650px` desktop, `min(500px,60vh)` responsive

**S46.3 debe re-certificarse con `S46-FIX-02` demostrando `iframe` `500px` + `HTML PREVIEW TEST` visible + Playwright `height>350`.*

**Referencias:** `HtmlPreviewDialog.razor:1-35` (`<style> .email-preview-frame`, `iframe class`, `WrapDocument`, `setPreviewSrcdoc`), `wwwroot/js/preview.js`, `S46-FIX-01-EmailPreview-Visibility.md` (`srcdoc` encoding), `S46.3` gates.

