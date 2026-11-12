using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Models.Messages;

namespace GpOne.Motore;

public record GraficoProposto(string Tipo, string X, IReadOnlyList<string> Y);

/// <summary>Stati della risposta dell'AI.</summary>
public static class StatiRisposta
{
    /// <summary>I passi rispondono alla domanda: si eseguono e l'ultimo va all'utente.</summary>
    public const string Fatto = "fatto";
    /// <summary>All'AI serve vedere (anonimizzato) il risultato dell'ultimo passo per proseguire.</summary>
    public const string ServeRisultato = "serveRisultato";
    /// <summary>Domanda ambigua: l'AI chiede un chiarimento all'utente.</summary>
    public const string Chiarimento = "chiarimento";
    /// <summary>Non si può rispondere con la struttura disponibile.</summary>
    public const string Impossibile = "impossibile";

    public static readonly string[] Tutti = [Fatto, ServeRisultato, Chiarimento, Impossibile];
}

/// <summary>
/// Risposta dell'AI: stato, passi SQL (con eventuali segnaposto {{NOME}}), chiarimento per l'utente e come presentare il risultato.
/// <see cref="Sql"/> è il testo dell'intera catena (una SELECT sola così com'è): è quello che si salva nello storico.
/// </summary>
public record PropostaSql(string Stato, IReadOnlyList<PassoSql> Passi, string? Chiarimento, string Spiegazione, GraficoProposto? Grafico)
{
    /// <summary>Una sola SELECT (vuota = non risolvibile).</summary>
    public PropostaSql(string sql, string spiegazione, GraficoProposto? grafico)
        : this(string.IsNullOrWhiteSpace(sql) ? StatiRisposta.Impossibile : StatiRisposta.Fatto,
            string.IsNullOrWhiteSpace(sql) ? [] : [new PassoSql(CatenaSql.NomeFinale, sql.Trim())], null, spiegazione, grafico)
    {
    }

    public string Sql => CatenaSql.ATesto(Passi);
}

/// <summary>Risposta interrotta per limite di token o di contesto: si riprova una volta con un prompt ridotto.</summary>
public class RispostaTroncataException(string message) : InvalidOperationException(message);

/// <summary>
/// Trasforma una domanda in SQL. Riceve istruzioni con la struttura del DB e la domanda; gli unici dati del cliente
/// ammessi sono quelli prodotti da <see cref="Anonimizzatore"/> (mai righe o valori in chiaro).
/// </summary>
public interface IGeneratoreSql
{
    Task<PropostaSql> GeneraAsync(ConfigurazioneAi configurazione, string istruzioni, string domanda, CancellationToken cancellationToken);
}

/// <summary>Sceglie il motore in base alla configurazione del cliente.</summary>
public class GeneratoreSql(ClaudeGeneratore claude, OllamaGeneratore ollama, GeminiGeneratore gemini) : IGeneratoreSql
{
    public Task<PropostaSql> GeneraAsync(ConfigurazioneAi configurazione, string istruzioni, string domanda, CancellationToken cancellationToken) =>
        configurazione.Provider switch
        {
            ProviderAi.Claude => claude.GeneraAsync(configurazione, istruzioni, domanda, cancellationToken),
            ProviderAi.Ollama => ollama.GeneraAsync(configurazione, istruzioni, domanda, cancellationToken),
            ProviderAi.Gemini or ProviderAi.GeminiVertex => gemini.GeneraAsync(configurazione, istruzioni, domanda, cancellationToken),
            _ => throw new InvalidOperationException($"Motore AI '{configurazione.Provider}' non supportato.")
        };

    /// <summary>Schema JSON della risposta, comune a Claude (structured outputs) e Ollama (format); per Gemini vedi <see cref="GeminiGeneratore"/>.</summary>
    public static readonly Dictionary<string, JsonElement> SchemaRisposta = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new
        {
            stato = new { type = "string", @enum = StatiRisposta.Tutti, description = DescrizioneStato },
            passi = new
            {
                type = "array",
                description = DescrizionePassi,
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        nome = new { type = "string", description = DescrizioneNome },
                        sql = new { type = "string", description = DescrizioneSql }
                    },
                    required = new[] { "nome", "sql" },
                    additionalProperties = false
                }
            },
            chiarimento = new { type = "string", description = DescrizioneChiarimento },
            spiegazione = new { type = "string", description = DescrizioneSpiegazione },
            grafico = new
            {
                type = "object",
                properties = new
                {
                    tipo = new { type = "string", @enum = new[] { "nessuno", "barre", "linee", "torta" } },
                    x = new { type = "string", description = "Alias della colonna per l'asse X o le etichette." },
                    y = new { type = "array", items = new { type = "string" }, description = "Alias delle colonne numeriche da rappresentare." }
                },
                required = new[] { "tipo", "x", "y" },
                additionalProperties = false
            }
        }),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "stato", "passi", "chiarimento", "spiegazione", "grafico" }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false)
    };

    internal const string DescrizioneStato = "fatto, serveRisultato, chiarimento oppure impossibile (vedi le istruzioni).";
    internal const string DescrizionePassi = "SELECT T-SQL da eseguire in ordine, di solito una sola. Vuoto con chiarimento o impossibile.";
    internal const string DescrizioneNome = "Nome del passo in MAIUSCOLO (es. CLIENTE_TOP): i passi successivi ne usano il valore con {{NOME}}.";
    internal const string DescrizioneSql = "Una sola SELECT T-SQL, con eventuali segnaposto {{NOME}} dei passi precedenti.";
    internal const string DescrizioneChiarimento = "Domanda per l'utente se lo stato è chiarimento, altrimenti stringa vuota.";
    internal const string DescrizioneSpiegazione = "Breve spiegazione in italiano di cosa calcola la query, o perché non è possibile.";

    public static PropostaSql Interpreta(string json)
    {
        var risposta = JsonSerializer.Deserialize<RispostaJson>(json, OpzioniJson)
            ?? throw new InvalidOperationException("Risposta dell'AI vuota.");
        var grafico = risposta.Grafico is { Tipo: not (null or "" or "nessuno") } g
            ? new GraficoProposto(g.Tipo, g.X ?? string.Empty, g.Y ?? [])
            : null;

        var passi = (risposta.Passi ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p.Sql))
            .Select((p, i) => new PassoSql(string.IsNullOrWhiteSpace(p.Nome) ? $"PASSO{i + 1}" : p.Nome.Trim(), p.Sql!.Trim()))
            .ToList();
        // Formato precedente: { "sql": "..." }
        if (passi.Count == 0 && !string.IsNullOrWhiteSpace(risposta.Sql))
            passi.Add(new PassoSql(CatenaSql.NomeFinale, risposta.Sql.Trim()));
        // Una sola query: nome fisso, così nello storico resta la SELECT senza intestazione
        if (passi.Count == 1)
            passi[0] = passi[0] with { Nome = CatenaSql.NomeFinale };

        var stato = StatiRisposta.Tutti.FirstOrDefault(s => string.Equals(s, risposta.Stato?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? (passi.Count > 0 ? StatiRisposta.Fatto : StatiRisposta.Impossibile);
        if (stato is StatiRisposta.Fatto or StatiRisposta.ServeRisultato && passi.Count == 0)
            stato = StatiRisposta.Impossibile;
        if (stato == StatiRisposta.Chiarimento && string.IsNullOrWhiteSpace(risposta.Chiarimento))
            stato = passi.Count > 0 ? StatiRisposta.Fatto : StatiRisposta.Impossibile;

        return new PropostaSql(stato, stato is StatiRisposta.Fatto or StatiRisposta.ServeRisultato ? passi : [],
            stato == StatiRisposta.Chiarimento ? risposta.Chiarimento!.Trim() : null, risposta.Spiegazione ?? string.Empty, grafico);
    }

    private static readonly JsonSerializerOptions OpzioniJson = new() { PropertyNameCaseInsensitive = true };

    private sealed record RispostaJson(string? Stato, List<PassoJson>? Passi, string? Sql, string? Chiarimento, string? Spiegazione, GraficoJson? Grafico);
    private sealed record PassoJson(string? Nome, string? Sql);
    private sealed record GraficoJson(string? Tipo, string? X, List<string>? Y);
}

/// <summary>Claude tramite l'SDK ufficiale Anthropic, con output strutturato (JSON schema).</summary>
public class ClaudeGeneratore
{
    public async Task<PropostaSql> GeneraAsync(ConfigurazioneAi configurazione, string istruzioni, string domanda, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configurazione.ApiKey))
            throw new InvalidOperationException("Chiave API di Claude non configurata per questo cliente.");

        AnthropicClient client = new() { ApiKey = configurazione.ApiKey };
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = configurazione.Modello,
            MaxTokens = 16000,
            // Istruzioni + struttura del DB: uguali per tutte le domande del cliente, quindi in cache
            System = new List<TextBlockParam>
            {
                new() { Text = istruzioni, CacheControl = new CacheControlEphemeral() }
            },
            Messages = [new UserMessageParam { Content = domanda }],
            OutputConfig = new OutputConfig
            {
                Effort = configurazione.Effort switch
                {
                    "low" => Effort.Low,
                    "high" => Effort.High,
                    "xhigh" => Effort.Xhigh,
                    "max" => Effort.Max,
                    _ => Effort.Medium
                },
                Format = new JsonOutputFormat { Schema = GeneratoreSql.SchemaRisposta }
            }
        }, cancellationToken: cancellationToken);

        if (response.StopReason == "refusal")
            throw new InvalidOperationException("Il motore AI ha rifiutato la richiesta.");
        if (response.StopReason == "max_tokens")
            throw new RispostaTroncataException("La risposta di Claude è stata interrotta per il limite di token.");

        var testo = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        return GeneratoreSql.Interpreta(testo);
    }
}

/// <summary>
/// Ollama (locale) tramite /api/chat, con la risposta vincolata allo schema JSON.
/// <paramref name="creaClient"/> fornisce l'HttpClient (non viene eliminato qui): IHttpClientFactory nell'API, uno condiviso nel desktop.
/// </summary>
public class OllamaGeneratore(Func<HttpClient> creaClient)
{
    public async Task<PropostaSql> GeneraAsync(ConfigurazioneAi configurazione, string istruzioni, string domanda, CancellationToken cancellationToken)
    {
        var http = creaClient();
        using var risposta = await http.PostAsJsonAsync(new Uri(new Uri(configurazione.Endpoint!.TrimEnd('/') + "/"), "api/chat"), new
        {
            model = configurazione.Modello,
            stream = false,
            format = GeneratoreSql.SchemaRisposta,
            options = new { temperature = 0 },
            messages = new[]
            {
                new { role = "system", content = istruzioni },
                new { role = "user", content = domanda }
            }
        }, cancellationToken);
        risposta.EnsureSuccessStatusCode();

        var corpo = await risposta.Content.ReadFromJsonAsync<OllamaRisposta>(cancellationToken)
            ?? throw new InvalidOperationException("Risposta di Ollama vuota.");
        if (corpo.DoneReason == "length")
            throw new RispostaTroncataException("La risposta di Ollama è stata interrotta per il limite di contesto.");
        return GeneratoreSql.Interpreta(corpo.Message?.Content ?? string.Empty);
    }

    private sealed record OllamaRisposta(
        [property: JsonPropertyName("message")] OllamaMessaggio? Message,
        [property: JsonPropertyName("done_reason")] string? DoneReason);
    private sealed record OllamaMessaggio([property: JsonPropertyName("content")] string? Content);
}
