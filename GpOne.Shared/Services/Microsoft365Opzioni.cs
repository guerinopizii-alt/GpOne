namespace GpOne.Services;

/// <summary>Opzioni dell'host per Microsoft 365 Copilot.</summary>
/// <param name="FinestraWindows">
/// Handle della finestra principale (solo app desktop Windows): se presente, l'accesso usa l'account Windows del PC
/// tramite il broker WAM; altrimenti (web) si usa il flusso device code.
/// </param>
public record Microsoft365Opzioni(Func<IntPtr>? FinestraWindows = null);
