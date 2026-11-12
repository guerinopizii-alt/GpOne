using System.Net.Http.Headers;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Models;

namespace GpOne.Motore;

/// <summary>
/// Elenco dei modelli disponibili con le credenziali del motore scelto, già filtrato su quelli utilizzabili da GpOne
/// (risposta JSON vincolata allo schema, generazione di testo). Non invia alcun dato del cliente: solo le credenziali.
/// <paramref name="creaClient"/> fornisce l'HttpClient (non viene eliminato qui).
/// </summary>
public class ElencoModelli(Func<HttpClient> creaClient)
{
    /// <summary>Parti del nome dei modelli Gemini che non generano testo (embedding, immagini, audio, sessioni live...).</summary>
    private static readonly string[] EsclusiGemini =
        ["embedding", "image", "tts", "audio", "live", "computer-use", "robotics", "aqa", "veo", "imagen"];

    public async Task<IReadOnlyList<string>> CaricaAsync(ConfigurazioneAi c, CancellationToken cancellationToken = default)
    {
        if (ProviderAi.RichiedeChiave(c.Provider) && string.IsNullOrWhiteSpace(c.ApiKey))
            throw new InvalidOperationException(c.Provider == ProviderAi.GeminiVertex
                ? "Caricare prima il file JSON dell'account di servizio."
                : $"Indicare prima la chiave API di {c.Provider}.");

        return c.Provider switch
        {
            ProviderAi.Claude => await ClaudeAsync(c.ApiKey!.Trim(), cancellationToken),
            ProviderAi.Gemini => await GeminiAsync(c.ApiKey!.Trim(), cancellationToken),
            ProviderAi.GeminiVertex => await VertexAsync(c, cancellationToken),
            ProviderAi.Ollama => await OllamaAsync(c.Endpoint, cancellationToken),
            _ => throw new InvalidOperationException($"Motore AI '{c.Provider}' non supportato.")
        };
    }

    /// <summary>Modelli Claude con output strutturato ed effort (usati da <see cref="ClaudeGeneratore"/>), dal più recente.</summary>
    private static async Task<IReadOnlyList<string>> ClaudeAsync(string chiave, CancellationToken cancellationToken)
    {
        AnthropicClient client = new() { ApiKey = chiave };
        var modelli = new List<string>();
        var pagina = await client.Models.List(new ModelListParams { Limit = 1000 }, cancellationToken);
        while (true)
        {
            foreach (var m in pagina.Items)
            {
                var capacita = m.Capabilities;
                if (capacita is null || (capacita.StructuredOutputs?.Supported == true && capacita.Effort?.Supported == true))
                    modelli.Add(m.ID);
            }
            if (!pagina.HasNext())
                break;
            pagina = await pagina.Next(cancellationToken);
        }
        return modelli;
    }

    /// <summary>Modelli Gemini (Google AI Studio) che supportano generateContent.</summary>
    private async Task<IReadOnlyList<string>> GeminiAsync(string chiave, CancellationToken cancellationToken)
    {
        var modelli = new List<string>();
        string? token = null;
        do
        {
            var url = "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1000"
                      + (token is null ? "" : "&pageToken=" + Uri.EscapeDataString(token));
            using var richiesta = new HttpRequestMessage(HttpMethod.Get, url);
            richiesta.Headers.Add("x-goog-api-key", chiave);
            using var documento = await LeggiAsync(richiesta, "Gemini", cancellationToken);
            var radice = documento.RootElement;

            if (radice.TryGetProperty("models", out var elenco))
                foreach (var m in elenco.EnumerateArray())
                {
                    var nome = Testo(m, "name")?.Replace("models/", "") ?? "";
                    var genera = m.TryGetProperty("supportedGenerationMethods", out var metodi)
                                 && metodi.EnumerateArray().Any(x => x.GetString() == "generateContent");
                    if (genera && ValidoGemini(nome))
                        modelli.Add(nome);
                }
            token = Testo(radice, "nextPageToken");
        } while (!string.IsNullOrEmpty(token));
        return Ordina(modelli);
    }

    /// <summary>Modelli Gemini pubblicati da Google su Vertex AI (Model Garden) nella regione indicata.</summary>
    private async Task<IReadOnlyList<string>> VertexAsync(ConfigurazioneAi c, CancellationToken cancellationToken)
    {
        var account = AccountServizioGoogle.Leggi(c.ApiKey!);
        var accesso = await GeminiGeneratore.TokenVertexAsync(c.ApiKey!, cancellationToken);
        var (host, _) = GeminiGeneratore.HostVertex(c.Endpoint);

        var modelli = new List<string>();
        string? token = null;
        do
        {
            var url = $"https://{host}/v1beta1/publishers/google/models?pageSize=200"
                      + (token is null ? "" : "&pageToken=" + Uri.EscapeDataString(token));
            using var richiesta = new HttpRequestMessage(HttpMethod.Get, url);
            richiesta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accesso);
            richiesta.Headers.Add("x-goog-user-project", account.ProgettoId);
            using var documento = await LeggiAsync(richiesta, "Vertex AI", cancellationToken);
            var radice = documento.RootElement;

            if (radice.TryGetProperty("publisherModels", out var elenco))
                foreach (var m in elenco.EnumerateArray())
                {
                    var nome = Testo(m, "name") is { } n ? n[(n.LastIndexOf('/') + 1)..] : "";
                    if (ValidoGemini(nome))
                        modelli.Add(nome);
                }
            token = Testo(radice, "nextPageToken");
        } while (!string.IsNullOrEmpty(token));
        return Ordina(modelli);
    }

    /// <summary>Modelli installati sul server Ollama (esclusi quelli di solo embedding).</summary>
    private async Task<IReadOnlyList<string>> OllamaAsync(string? endpoint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException("Indicare prima l'indirizzo di Ollama.");
        using var richiesta = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(endpoint.Trim().TrimEnd('/') + "/"), "api/tags"));
        using var documento = await LeggiAsync(richiesta, "Ollama", cancellationToken);

        var modelli = new List<string>();
        if (documento.RootElement.TryGetProperty("models", out var elenco))
            foreach (var m in elenco.EnumerateArray())
                if (Testo(m, "name") is { Length: > 0 } nome && !nome.Contains("embed", StringComparison.OrdinalIgnoreCase))
                    modelli.Add(nome);
        modelli.Sort(StringComparer.OrdinalIgnoreCase);
        return modelli;
    }

    private async Task<JsonDocument> LeggiAsync(HttpRequestMessage richiesta, string nome, CancellationToken cancellationToken)
    {
        using var risposta = await creaClient().SendAsync(richiesta, cancellationToken);
        var corpo = await risposta.Content.ReadAsStringAsync(cancellationToken);
        if (!risposta.IsSuccessStatusCode)
            throw new InvalidOperationException($"{nome} ha risposto con errore {(int)risposta.StatusCode}: {GeminiGeneratore.MessaggioErrore(corpo)}");
        try
        {
            return JsonDocument.Parse(corpo);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Risposta di {nome} non valida.");
        }
    }

    private static bool ValidoGemini(string nome) =>
        nome.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase)
        && !EsclusiGemini.Any(e => nome.Contains(e, StringComparison.OrdinalIgnoreCase));

    /// <summary>Senza duplicati, dal più recente (le versioni più alte vengono prima in ordine decrescente).</summary>
    private static List<string> Ordina(List<string> modelli) =>
        modelli.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(m => m, StringComparer.OrdinalIgnoreCase).ToList();

    private static string? Testo(JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
