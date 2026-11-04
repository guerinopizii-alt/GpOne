namespace GpOne.Services;

public enum ChatRole
{
    User,
    Assistant
}

/// <param name="Dettagli">Informazioni aggiuntive mostrate all'utente (es. query e dati letti dal database).</param>
public record ChatMessage(ChatRole Role, string Text, DateTime Timestamp, string? Dettagli = null, TempiAi? Tempi = null);

/// <summary>Tempi di una risposta: lettura dati, attesa della prima parte di risposta e totale (millisecondi).</summary>
public record TempiAi(long MsDati, long? MsPrimaRisposta, long MsTotale, int CaratteriInviati)
{
    public string Descrizione =>
        $"Dati {Secondi(MsDati)} · prima risposta {(MsPrimaRisposta is { } ms ? Secondi(ms) : "–")} · totale {Secondi(MsTotale)} · {CaratteriInviati:N0} caratteri inviati";

    private static string Secondi(long ms) => $"{ms / 1000.0:0.0} s";
}

/// <summary>
/// Punto di integrazione di GpOne AI con il motore AI: l'implementazione (GpOneAiService) usa il motore
/// scelto in Sistema > Intelligenza AI (locale, Claude, Gemini, Microsoft 365 Copilot...), senza modificare la UI.
/// </summary>
public interface IAiChatService
{
    /// <summary>Restituisce la risposta in streaming, un frammento di testo alla volta.</summary>
    IAsyncEnumerable<string> GetResponseStreamAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default);
}
