using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;

namespace GpOne.Motore;

/// <summary>
/// Gemini di Google, in due varianti con la stessa richiesta generateContent:
/// <list type="bullet">
/// <item><see cref="ProviderAi.Gemini"/>: Gemini API (Google AI Studio), con chiave API.</item>
/// <item><see cref="ProviderAi.GeminiVertex"/>: Vertex AI su Google Cloud, con account di servizio (JSON) e regione
/// (<see cref="ConfigurazioneAi.Endpoint"/>, es. "europe-west8"; vuota = "global").</item>
/// </list>
/// Come per gli altri motori riceve solo le istruzioni (struttura abilitata), la domanda ed eventuali risultati anonimizzati;
/// risposta vincolata allo schema JSON.
/// </summary>
public class GeminiGeneratore(Func<HttpClient> creaClient)
{
    private const string ScopeCloud = "https://www.googleapis.com/auth/cloud-platform";

    /// <summary>Credenziali Vertex già create (la libreria tiene in cache il token), per impronta del JSON.</summary>
    private static readonly ConcurrentDictionary<string, GoogleCredential> Credenziali = new();

    public async Task<PropostaSql> GeneraAsync(ConfigurazioneAi configurazione, string istruzioni, string domanda, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configurazione.ApiKey))
            throw new InvalidOperationException(configurazione.Provider == ProviderAi.GeminiVertex
                ? "Account di servizio di Vertex AI non configurato."
                : "Chiave API di Gemini non configurata.");

        using var richiesta = configurazione.Provider == ProviderAi.GeminiVertex
            ? await RichiestaVertexAsync(configurazione, cancellationToken)
            : RichiestaGemini(configurazione);
        richiesta.Content = JsonContent.Create(new
        {
            systemInstruction = new { parts = new[] { new { text = istruzioni } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = domanda } } } },
            generationConfig = new
            {
                temperature = 0,
                responseMimeType = "application/json",
                responseSchema = SchemaRisposta
            }
        });

        using var risposta = await creaClient().SendAsync(richiesta, cancellationToken);
        var corpo = await risposta.Content.ReadAsStringAsync(cancellationToken);
        if (!risposta.IsSuccessStatusCode)
            throw new InvalidOperationException($"{Nome(configurazione)} ha risposto con errore {(int)risposta.StatusCode}: {MessaggioErrore(corpo)}");

        return GeneratoreSql.Interpreta(TestoRisposta(corpo, Nome(configurazione)));
    }

    private static string Nome(ConfigurazioneAi c) => c.Provider == ProviderAi.GeminiVertex ? "Vertex AI" : "Gemini";

    private static HttpRequestMessage RichiestaGemini(ConfigurazioneAi c)
    {
        var richiesta = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(c.Modello.Trim())}:generateContent");
        richiesta.Headers.Add("x-goog-api-key", c.ApiKey!.Trim());
        return richiesta;
    }

    private static async Task<HttpRequestMessage> RichiestaVertexAsync(ConfigurazioneAi c, CancellationToken cancellationToken)
    {
        var account = AccountServizioGoogle.Leggi(c.ApiKey!);
        var (host, regione) = HostVertex(c.Endpoint);
        var url = $"https://{host}/v1/projects/{Uri.EscapeDataString(account.ProgettoId)}/locations/{Uri.EscapeDataString(regione)}" +
                  $"/publishers/google/models/{Uri.EscapeDataString(c.Modello.Trim())}:generateContent";

        var richiesta = new HttpRequestMessage(HttpMethod.Post, url);
        richiesta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenVertexAsync(c.ApiKey!, cancellationToken));
        return richiesta;
    }

    /// <summary>Host di Vertex AI per la regione (vuota = global).</summary>
    internal static (string Host, string Regione) HostVertex(string? endpoint)
    {
        var regione = string.IsNullOrWhiteSpace(endpoint) ? "global" : endpoint.Trim();
        return (regione == "global" ? "aiplatform.googleapis.com" : $"{regione}-aiplatform.googleapis.com", regione);
    }

    /// <summary>Token OAuth dell'account di servizio (JSON) per Google Cloud.</summary>
    internal static async Task<string> TokenVertexAsync(string jsonAccount, CancellationToken cancellationToken)
    {
        var impronta = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(jsonAccount)));
        var credenziale = Credenziali.GetOrAdd(impronta, _ =>
            CredentialFactory.FromJson<ServiceAccountCredential>(jsonAccount).ToGoogleCredential().CreateScoped(ScopeCloud));
        return await ((ITokenAccess)credenziale).GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
    }

    /// <summary>Testo della prima risposta; errore chiaro se Gemini ha bloccato la richiesta o non ha risposto.</summary>
    private static string TestoRisposta(string corpo, string nome)
    {
        using var documento = JsonDocument.Parse(corpo);
        var radice = documento.RootElement;
        if (!radice.TryGetProperty("candidates", out var candidati) || candidati.GetArrayLength() == 0)
        {
            var motivo = radice.TryGetProperty("promptFeedback", out var feedback) && feedback.TryGetProperty("blockReason", out var blocco)
                ? blocco.GetString()
                : null;
            throw new InvalidOperationException(motivo is null ? $"Risposta di {nome} vuota." : $"{nome} ha bloccato la richiesta ({motivo}).");
        }

        var candidato = candidati[0];
        if (candidato.TryGetProperty("finishReason", out var motivoFine) && motivoFine.GetString() == "MAX_TOKENS")
            throw new RispostaTroncataException($"La risposta di {nome} è stata interrotta per il limite di token.");
        var testo = new StringBuilder();
        if (candidato.TryGetProperty("content", out var contenuto) && contenuto.TryGetProperty("parts", out var parti))
            foreach (var parte in parti.EnumerateArray())
                if (parte.TryGetProperty("text", out var t) && !(parte.TryGetProperty("thought", out var pensiero) && pensiero.GetBoolean()))
                    testo.Append(t.GetString());

        if (testo.Length == 0)
        {
            var fine = candidato.TryGetProperty("finishReason", out var f) ? f.GetString() : null;
            throw new InvalidOperationException($"{nome} non ha restituito una risposta{(fine is null ? "" : $" (motivo: {fine})")}.");
        }
        return testo.ToString();
    }

    internal static string MessaggioErrore(string corpo)
    {
        try
        {
            using var documento = JsonDocument.Parse(corpo);
            if (documento.RootElement.TryGetProperty("error", out var errore) && errore.TryGetProperty("message", out var messaggio))
                return messaggio.GetString() ?? corpo;
        }
        catch (JsonException)
        {
            // corpo non JSON
        }
        return corpo.Length > 300 ? corpo[..300] : corpo;
    }

    /// <summary>
    /// Stesso contenuto di <see cref="GeneratoreSql.SchemaRisposta"/> nel formato Schema di Gemini/Vertex
    /// (tipi OpenAPI, nessun additionalProperties).
    /// </summary>
    private static readonly object SchemaRisposta = new
    {
        type = "OBJECT",
        properties = new
        {
            stato = new { type = "STRING", @enum = StatiRisposta.Tutti, description = GeneratoreSql.DescrizioneStato },
            passi = new
            {
                type = "ARRAY",
                description = GeneratoreSql.DescrizionePassi,
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        nome = new { type = "STRING", description = GeneratoreSql.DescrizioneNome },
                        sql = new { type = "STRING", description = GeneratoreSql.DescrizioneSql }
                    },
                    required = new[] { "nome", "sql" },
                    propertyOrdering = new[] { "nome", "sql" }
                }
            },
            chiarimento = new { type = "STRING", description = GeneratoreSql.DescrizioneChiarimento },
            spiegazione = new { type = "STRING", description = GeneratoreSql.DescrizioneSpiegazione },
            grafico = new
            {
                type = "OBJECT",
                properties = new
                {
                    tipo = new { type = "STRING", @enum = new[] { "nessuno", "barre", "linee", "torta" } },
                    x = new { type = "STRING", description = "Alias della colonna per l'asse X o le etichette." },
                    y = new { type = "ARRAY", items = new { type = "STRING" }, description = "Alias delle colonne numeriche da rappresentare." }
                },
                required = new[] { "tipo", "x", "y" },
                propertyOrdering = new[] { "tipo", "x", "y" }
            }
        },
        required = new[] { "stato", "passi", "chiarimento", "spiegazione", "grafico" },
        propertyOrdering = new[] { "stato", "passi", "chiarimento", "spiegazione", "grafico" }
    };
}

/// <summary>Dati essenziali del file JSON di un account di servizio Google (mai la chiave privata).</summary>
public sealed record AccountServizioGoogle(string ProgettoId, string Email)
{
    /// <summary>Legge e controlla il JSON dell'account di servizio; eccezione con messaggio chiaro se non valido.</summary>
    public static AccountServizioGoogle Leggi(string json)
    {
        try
        {
            using var documento = JsonDocument.Parse(json);
            var r = documento.RootElement;
            string? Campo(string nome) => r.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            if (Campo("type") != "service_account")
                throw new InvalidOperationException("Il file non è la chiave di un account di servizio Google (\"type\": \"service_account\").");
            if (Campo("project_id") is not { Length: > 0 } progetto || Campo("client_email") is not { Length: > 0 } email
                || Campo("private_key") is not { Length: > 0 })
                throw new InvalidOperationException("Nel file dell'account di servizio mancano project_id, client_email o private_key.");
            return new AccountServizioGoogle(progetto, email);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Il file dell'account di servizio non è un JSON valido.");
        }
    }
}
