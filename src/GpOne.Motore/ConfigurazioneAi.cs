namespace GpOne.Motore;

public static class ProviderAi
{
    public const string Claude = "Claude";
    public const string Ollama = "Ollama";
    /// <summary>Gemini API (Google AI Studio), con chiave API.</summary>
    public const string Gemini = "Gemini";
    /// <summary>Gemini su Vertex AI (Google Cloud): account di servizio JSON nella chiave, regione in Endpoint.</summary>
    public const string GeminiVertex = "GeminiVertex";

    public static readonly string[] Tutti = [Claude, Gemini, GeminiVertex, Ollama];

    /// <summary>Motori che richiedono un segreto (chiave API o account di servizio).</summary>
    public static bool RichiedeChiave(string provider) => provider is Claude or Gemini or GeminiVertex;
}

/// <summary>
/// Motore AI di un cliente. <see cref="ApiKey"/>: in scrittura il nuovo segreto (vuoto = invariato), mai restituito.
/// Per Vertex AI il segreto è il JSON dell'account di servizio ed <see cref="Endpoint"/> la regione.
/// </summary>
public class ConfigurazioneAi
{
    public int ClienteId { get; set; }
    public string Provider { get; set; } = ProviderAi.Claude;
    public string Modello { get; set; } = "claude-opus-5-5";
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public bool ApiKeyImpostata { get; set; }
    /// <summary>Solo in lettura, per Vertex AI: account di servizio e progetto salvati (mai la chiave privata).</summary>
    public string? AccountServizio { get; set; }
    public string? Effort { get; set; } = "medium";
    public string? Istruzioni { get; set; }
    public int MaxRighe { get; set; } = 1000;
    public int TimeoutSecondi { get; set; } = 30;
    /// <summary>Chiamate all'AI al massimo per una domanda (correzioni, risultati anonimizzati): oltre ci si ferma.</summary>
    public int MaxChiamateAi { get; set; } = 5;
    /// <summary>
    /// Se la domanda non si risolve con query e segnaposto, l'AI può chiedere di vedere un risultato: con true glielo si manda
    /// ANONIMIZZATO (<see cref="Anonimizzatore"/>), con false la domanda risulta non risolvibile.
    /// </summary>
    public bool InvioDatiAnonimizzati { get; set; } = true;
    /// <summary>Righe anonimizzate mandate all'AI al massimo; oltre si mandano le prime più un riepilogo.</summary>
    public int MaxRigheAnonimizzate { get; set; } = 50;

    public const int MinMaxRighe = 1, MaxMaxRighe = 100_000;
    public const int MinTimeout = 5, MaxTimeout = 600;
    public const int MinChiamateAi = 1, MaxMaxChiamateAi = 10;
    public const int MinRigheAnonimizzate = 1, MaxMaxRigheAnonimizzate = 500;

    /// <summary>Messaggio d'errore se la configurazione non è utilizzabile, altrimenti null (il segreto si controlla a parte).</summary>
    public string? Verifica()
    {
        if (!ProviderAi.Tutti.Contains(Provider))
            return "Motore AI non valido: Claude, Gemini, GeminiVertex oppure Ollama.";
        if (string.IsNullOrWhiteSpace(Modello))
            return "Indicare il modello.";
        if (Provider == ProviderAi.Ollama && string.IsNullOrWhiteSpace(Endpoint))
            return "Indicare l'indirizzo di Ollama (es. http://localhost:11434).";
        if (Provider == ProviderAi.GeminiVertex && !string.IsNullOrWhiteSpace(Endpoint)
            && !System.Text.RegularExpressions.Regex.IsMatch(Endpoint.Trim(), "^[a-z0-9-]+$"))
            return "Regione di Vertex AI non valida (es. europe-west8, us-central1 oppure global).";
        return null;
    }
}
