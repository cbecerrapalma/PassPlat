// S83 · D8 — atajo Ctrl+K (Cmd+K) del buscador de MainLayout.
// Escucha en document, en fase de burbuja: respeta un preventDefault previo y no actúa dentro de Monaco,
// que usa acordes Ctrl+K propios.
let manejador = null;

export function registrarAtajoBuscar(dotnetRef) {
    quitarAtajoBuscar();
    manejador = (e) => {
        if (!(e.ctrlKey || e.metaKey) || e.shiftKey || e.altKey) return;
        if (typeof e.key !== 'string' || e.key.toLowerCase() !== 'k') return;
        if (e.defaultPrevented) return;
        if (e.target instanceof Element && e.target.closest('.monaco-editor')) return;
        e.preventDefault();
        dotnetRef.invokeMethodAsync('AbrirBuscador');
    };
    document.addEventListener('keydown', manejador);
}

// S89 · D3 — funciones del layout que antes iban por eval.
export function esMovil() {
    return window.innerWidth < 960;
}

export function alternarPantallaCompleta() {
    if (document.fullscreenElement) {
        return document.exitFullscreen();
    }
    return document.documentElement.requestFullscreen();
}

export function quitarAtajoBuscar() {
    if (manejador) {
        document.removeEventListener('keydown', manejador);
        manejador = null;
    }
}
