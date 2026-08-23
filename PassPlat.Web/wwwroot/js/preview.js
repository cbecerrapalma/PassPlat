window.setPreviewSrcdoc = (id, html) => {
    const el = document.getElementById(id);
    if (el) el.srcdoc = html;
};
