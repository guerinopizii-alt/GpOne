// Funzioni JavaScript di GpOne usate tramite JS interop

// Scarica un testo come file (es. CSV); il BOM UTF-8 permette a Excel di leggere correttamente gli accenti
export function scaricaFile(nomeFile, contenuto, tipo) {
    const blob = new Blob(["﻿" + contenuto], { type: tipo });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = nomeFile;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
}
