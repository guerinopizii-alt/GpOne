using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace GpOne.Services;

/// <summary>
/// Implementazione di <see cref="IAiChatService"/> che usa il motore configurato in <see cref="AiSettingsStore"/>:
/// simulato, Ollama / servizi compatibili OpenAI (HTTP + SSE) oppure Anthropic Claude (SDK ufficiale).
/// </summary>
public class GpOneAiService(AiSettingsStore store, IHttpClientFactory httpClientFactory, Microsoft365CopilotService microsoft365) : IAiChatService
{
    public const string HttpClientName = "GpOne.AI";

    public IAsyncEnumerable<string> GetResponseStreamAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default) =>
        store.Abilitato
            ? GetResponseStreamAsync(store.Correnti, store.GetApiKey(), history, cancellationToken)
            : throw new InvalidOperationException("Non sei abilitato all'uso di GpOne AI.");

    /// <summary>Usa impostazioni esplicite (es. per provare la configurazione prima di salvarla).</summary>
    public IAsyncEnumerable<string> GetResponseStreamAsync(AiImpostazioni impostazioni, string? apiKey, IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default) =>
        impostazioni.Provider switch
        {
            AiProvider.Simulato => new SimulatoAiService().GetResponseStreamAsync(history, cancellationToken),
            AiProvider.Ollama or AiProvider.OpenAiCompatibile => OpenAiCompatibileAsync(impostazioni, apiKey, history, cancellationToken),
            // Gemini: stessa API compatibile OpenAI, con l'endpoint fisso di Google
            AiProvider.Gemini => OpenAiCompatibileAsync(
                impostazioni with { Endpoint = AiImpostazioni.EndpointPredefinito(AiProvider.Gemini) }, apiKey, history, cancellationToken),
            AiProvider.Claude => ClaudeAsync(impostazioni, apiKey, history, cancellationToken),
            AiProvider.Microsoft365Copilot => microsoft365.ChatAsync(history, cancellationToken),
            AiProvider.VertexAi => VertexAiAsync(impostazioni, apiKey, history, cancellationToken),
            _ => throw new InvalidOperationException($"Motore AI non supportato: {impostazioni.Provider}.")
        };

    /// <summary>Elenco dei modelli esposti da un endpoint compatibile OpenAI (GET /models).</summary>
    public async Task<IReadOnlyList<string>> ElencoModelliAsync(string endpoint, string? apiKey, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url(endpoint, "models"));
        Autorizza(request, apiKey);

        using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        await VerificaRispostaAsync(response, cancellationToken);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return
        [
            .. doc.RootElement.GetProperty("data").EnumerateArray()
                .Select(m => m.GetProperty("id").GetString())
                // Gemini restituisce gli id nella forma "models/nome"
                .Select(id => id is not null && id.StartsWith("models/", StringComparison.Ordinal) ? id["models/".Length..] : id)
                .OfType<string>()
                .Order()
        ];
    }

    private async IAsyncEnumerable<string> OpenAiCompatibileAsync(
        AiImpostazioni impostazioni, string? apiKey, IReadOnlyList<ChatMessage> history,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(impostazioni.SystemPrompt))
            messages.Add(new { role = "system", content = impostazioni.SystemPrompt });
        messages.AddRange(history.Select(m => new { role = m.Role == ChatRole.User ? "user" : "assistant", content = m.Text }));

        using var request = new HttpRequestMessage(HttpMethod.Post, Url(impostazioni.Endpoint, "chat/completions"))
        {
            Content = JsonContent.Create(new { model = impostazioni.Modello, messages, stream = true })
        };
        Autorizza(request, apiKey);

        using var response = await httpClientFactory.CreateClient(HttpClientName)
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await VerificaRispostaAsync(response, cancellationToken);

        // Server-Sent Events: righe "data: {json}", terminate da "data: [DONE]"
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var data = line[5..].Trim();
            if (data == "[DONE]")
                yield break;

            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("choices", out var choices)
                && choices.GetArrayLength() > 0
                && choices[0].TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
            {
                yield return content.GetString()!;
            }
        }
    }

    private static IAsyncEnumerable<string> ClaudeAsync(
        AiImpostazioni impostazioni, string? apiKey, IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(apiKey)
            ? throw new InvalidOperationException("Chiave API di Anthropic non configurata.")
            : ClaudeChat.StreamAsync(apiKey, impostazioni, history, cancellationToken);

    /// <summary>
    /// Gemini su Vertex AI: endpoint compatibile OpenAI nella regione scelta, autenticato con il token
    /// del service account (la chiave JSON è conservata cifrata come chiave API).
    /// </summary>
    private async IAsyncEnumerable<string> VertexAiAsync(
        AiImpostazioni impostazioni, string? chiaveJson, IReadOnlyList<ChatMessage> history,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chiaveJson))
            throw new InvalidOperationException("Chiave del service account Google Cloud non configurata.");
        var regione = impostazioni.Regione ?? throw new InvalidOperationException("Regione di Vertex AI non configurata.");
        var progetto = impostazioni.ProgettoId ?? VertexAiAuth.ProgettoId(chiaveJson)
            ?? throw new InvalidOperationException("ID del progetto Google Cloud non trovato nella chiave.");

        var token = await VertexAiAuth.AccessTokenAsync(chiaveJson, cancellationToken);
        var vertex = impostazioni with
        {
            Endpoint = $"https://{regione}-aiplatform.googleapis.com/v1/projects/{progetto}/locations/{regione}/endpoints/openapi",
            // Sull'endpoint OpenAI di Vertex i modelli Google si indicano come "google/<modello>"
            Modello = impostazioni.Modello.Contains('/') ? impostazioni.Modello : $"google/{impostazioni.Modello}"
        };

        await foreach (var parte in OpenAiCompatibileAsync(vertex, token, history, cancellationToken))
            yield return parte;
    }

    private static Uri Url(string endpoint, string percorso) => new($"{endpoint.TrimEnd('/')}/{percorso}");

    private static void Autorizza(HttpRequestMessage request, string? apiKey)
    {
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    private static async Task VerificaRispostaAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var dettaglio = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Il servizio AI ha risposto {(int)response.StatusCode} {response.ReasonPhrase}. {dettaglio}".Trim());
    }
}
