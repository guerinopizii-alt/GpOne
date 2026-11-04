using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.DataProtection;

namespace GpOne.Services;

public enum AiProvider
{
    /// <summary>Risposte simulate, nessun motore AI.</summary>
    Simulato,
    /// <summary>Ollama in locale (API compatibile OpenAI).</summary>
    Ollama,
    /// <summary>Qualsiasi servizio con API compatibile OpenAI (LM Studio, vLLM, OpenAI, ...).</summary>
    OpenAiCompatibile,
    /// <summary>Anthropic Claude (online).</summary>
    Claude,
    /// <summary>Microsoft 365 Copilot ("Copilot lavoro") nel tenant aziendale.</summary>
    Microsoft365Copilot,
    /// <summary>Google Gemini (online), tramite l'API compatibile OpenAI di Google.</summary>
    Gemini,
    /// <summary>Google Gemini su Vertex AI (Google Cloud), elaborato nella regione UE scelta.</summary>
    VertexAi
}

/// <summary>Impostazioni AI esposte all'interfaccia: la chiave API non è mai inclusa.</summary>
public record AiImpostazioni(
    AiProvider Provider,
    string Endpoint,
    string Modello,
    string SystemPrompt,
    bool HaApiKey,
    string? TenantId = null,
    string? ClientId = null,
    bool DatiLiberi = false,
    string? Regione = null,
    string? ProgettoId = null,
    int MaxCaratteriDati = AiImpostazioni.MaxCaratteriDatiPredefinito,
    int MaxTokenMotore = 0)
{
    /// <summary>Dimensione massima predefinita dei dati inviati all'AI (circa 15.000 token).</summary>
    public const int MaxCaratteriDatiPredefinito = 60000;

    /// <summary>Regioni UE di Vertex AI proposte all'amministratore.</summary>
    public static readonly IReadOnlyDictionary<string, string> RegioniUe = new Dictionary<string, string>
    {
        ["europe-west8"] = "Milano (europe-west8)",
        ["europe-west1"] = "Belgio (europe-west1)",
        ["europe-west3"] = "Francoforte (europe-west3)",
        ["europe-west4"] = "Paesi Bassi (europe-west4)",
        ["europe-west9"] = "Parigi (europe-west9)",
    };

    public const string SystemPromptPredefinito =
        "Sei GpOne AI, l'assistente dell'applicazione GpOne. Rispondi in italiano, in modo chiaro e conciso.";

    public static string EndpointPredefinito(AiProvider provider) => provider switch
    {
        AiProvider.Ollama => "http://localhost:11434/v1",
        AiProvider.OpenAiCompatibile => "http://localhost:1234/v1",
        AiProvider.Gemini => "https://generativelanguage.googleapis.com/v1beta/openai",
        _ => string.Empty
    };

    /// <summary>
    /// Capacità indicativa del motore (finestra di contesto, in token) usata per il controllo prima dell'invio.
    /// Per Ollama dipende dal parametro num_ctx del modello.
    /// </summary>
    public static int TokenMotorePredefinito(AiProvider provider) => provider switch
    {
        AiProvider.Ollama => 8_192,
        AiProvider.OpenAiCompatibile => 32_768,
        AiProvider.Microsoft365Copilot => 16_000,
        AiProvider.Claude or AiProvider.Gemini or AiProvider.VertexAi => 1_000_000,
        _ => int.MaxValue
    };

    /// <summary>Capacità effettiva: quella indicata dall'amministratore oppure quella predefinita del motore.</summary>
    public int LimiteTokenMotore => MaxTokenMotore > 0 ? MaxTokenMotore : TokenMotorePredefinito(Provider);

    /// <summary>Valore che indica nessun limite alla dimensione dei dati inviati.</summary>
    public const int NessunLimite = 0;

    /// <summary>
    /// Limite predefinito dei dati inviati per motore (caratteri, circa 4 per token):
    /// basso per i modelli locali (finestra di contesto piccola), alto per Claude e Gemini.
    /// </summary>
    public static int MaxCaratteriPredefinito(AiProvider provider) => provider switch
    {
        AiProvider.Ollama => 16_000,
        AiProvider.OpenAiCompatibile => 60_000,
        AiProvider.Microsoft365Copilot => 30_000,
        AiProvider.Claude or AiProvider.Gemini or AiProvider.VertexAi => 2_000_000,
        _ => MaxCaratteriDatiPredefinito
    };

    public static string ModelloPredefinito(AiProvider provider) => provider switch
    {
        AiProvider.Ollama => "llama3.2",
        AiProvider.Claude => "claude-opus-5-5",
        AiProvider.Gemini => "gemini-3.1-pro-preview",
        AiProvider.VertexAi => "gemini-2.5-flash",
        _ => string.Empty
    };

    public string Descrizione => Provider switch
    {
        AiProvider.Simulato => "Simulato",
        AiProvider.Ollama => $"Ollama (locale) · {Modello}",
        AiProvider.OpenAiCompatibile => $"OpenAI compatibile · {Modello}",
        AiProvider.Claude => $"Claude (online) · {Modello}",
        AiProvider.Microsoft365Copilot => "Microsoft 365 Copilot (lavoro)",
        AiProvider.Gemini => $"Gemini (online) · {Modello}",
        AiProvider.VertexAi => $"Gemini su Vertex AI (UE, {Regione}) · {Modello}",
        _ => Provider.ToString()
    };
}

/// <summary>Configurazione AI con nome, salvata nel database.</summary>
public record ConfigurazioneAi(int Id, string Nome, bool Predefinita, AiImpostazioni Impostazioni);

/// <summary>Percorso del vecchio file ai.json, importato nel database al primo utilizzo.</summary>
public record AiSettingsOpzioni(string FileLegacy);

/// <summary>
/// Configurazioni AI salvate nel database (dbo.AiConfigurazioni) e legate agli utenti (dbo.Utenti.AiConfigurazioneId):
/// solo gli utenti con una configurazione assegnata possono usare GpOne AI.
/// La chiave API è cifrata con ASP.NET Core Data Protection e decifrata solo per le chiamate al servizio.
/// </summary>
public class AiSettingsStore(UserSessionService sessione, IDataProtectionProvider dataProtectionProvider, AiSettingsOpzioni opzioni)
{
    private const string Colonne =
        "Id, Nome, Predefinita, Provider, Endpoint, Modello, SystemPrompt, ApiKeyCifrata, TenantId, ClientId, " +
        "DatiLiberi, Regione, ProgettoId, MaxCaratteriDati, MaxTokenMotore";

    private static readonly AiImpostazioni NonAbilitato =
        new(AiProvider.Simulato, string.Empty, string.Empty, AiImpostazioni.SystemPromptPredefinito, false);

    // Formato del vecchio file ai.json (solo per l'importazione)
    private sealed record FileLegacy(
        AiProvider Provider, string Endpoint, string Modello, string SystemPrompt, string? ApiKeyCifrata,
        string? TenantId = null, string? ClientId = null, bool DatiLiberi = false, string? Regione = null,
        string? ProgettoId = null, int MaxCaratteriDati = AiImpostazioni.MaxCaratteriDatiPredefinito, int MaxTokenMotore = 0);

    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("GpOne.AiSettings.v1");
    private AiImpostazioni? correnti;
    private string? apiKeyCifrata;

    public event Action? Changed;

    /// <summary>true se l'utente collegato ha una configurazione AI assegnata.</summary>
    public bool Abilitato => correnti is not null;

    /// <summary>Configurazione dell'utente collegato (se non abilitato: "Simulato").</summary>
    public AiImpostazioni Correnti => correnti ?? NonAbilitato;

    /// <summary>Motivo per cui le configurazioni non sono disponibili (es. database da aggiornare).</summary>
    public string? Avviso { get; private set; }

    /// <summary>Chiave API della configurazione dell'utente: da usare solo per chiamare il servizio, mai da mostrare.</summary>
    public string? GetApiKey() => Decifra(apiKeyCifrata);

    /// <summary>Carica la configurazione assegnata all'utente collegato (da chiamare dopo il login).</summary>
    public async Task CaricaAsync(CancellationToken cancellationToken = default)
    {
        correnti = null;
        apiKeyCifrata = null;
        Avviso = null;
        if (sessione.Utente is { } utente && sessione.ConnectionString is not null)
        {
            try
            {
                await using var connection = await OpenAsync(cancellationToken);
                await ImportaFileAsync(connection, cancellationToken);

                await using var command = new SqlCommand($"""
                    SELECT {Colonne} FROM dbo.AiConfigurazioni
                    WHERE Id = (SELECT AiConfigurazioneId FROM dbo.Utenti WHERE Id = @Utente)
                    """, connection);
                command.Parameters.Add("@Utente", SqlDbType.Int).Value = utente.Id;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    (var configurazione, apiKeyCifrata) = Leggi(reader);
                    correnti = configurazione.Impostazioni;
                }
            }
            catch (SqlException ex) when (ex.Number is 207 or 208)
            {
                Avviso = "Configurazioni AI non disponibili: eseguire gli script da Sistema > Database.";
            }
        }
        Changed?.Invoke();
    }

    public async Task<IReadOnlyList<ConfigurazioneAi>> ElencoAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand($"SELECT {Colonne} FROM dbo.AiConfigurazioni ORDER BY Nome", connection);
        var elenco = new List<ConfigurazioneAi>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            elenco.Add(Leggi(reader).Configurazione);
        return elenco;
    }

    /// <summary>Chiave API in chiaro di una configurazione (per le prove dell'amministratore).</summary>
    public async Task<string?> ApiKeyAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken, soloAmministratore: true);
        await using var command = new SqlCommand("SELECT ApiKeyCifrata FROM dbo.AiConfigurazioni WHERE Id = @Id", connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        return Decifra(await command.ExecuteScalarAsync(cancellationToken) as string);
    }

    /// <summary>
    /// Crea (id = 0) o aggiorna una configurazione. Con <paramref name="apiKey"/> vuota mantiene quella salvata,
    /// salvo <paramref name="rimuoviApiKey"/>. Restituisce l'id.
    /// </summary>
    public async Task<int> SalvaAsync(int id, string nome, bool predefinita, AiImpostazioni impostazioni, string? apiKey, bool rimuoviApiKey,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken, soloAmministratore: true);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = new SqlCommand(id == 0
                ? """
                  INSERT INTO dbo.AiConfigurazioni (Nome, Predefinita, Provider, Endpoint, Modello, SystemPrompt, ApiKeyCifrata,
                      TenantId, ClientId, DatiLiberi, Regione, ProgettoId, MaxCaratteriDati, MaxTokenMotore)
                  OUTPUT INSERTED.Id
                  VALUES (@Nome, @Predefinita, @Provider, @Endpoint, @Modello, @SystemPrompt, @NuovaChiave,
                      @TenantId, @ClientId, @DatiLiberi, @Regione, @ProgettoId, @MaxCaratteriDati, @MaxTokenMotore)
                  """
                : """
                  UPDATE dbo.AiConfigurazioni
                  SET Nome = @Nome, Predefinita = @Predefinita, Provider = @Provider, Endpoint = @Endpoint, Modello = @Modello,
                      SystemPrompt = @SystemPrompt,
                      ApiKeyCifrata = CASE WHEN @Rimuovi = 1 THEN NULL WHEN @NuovaChiave IS NULL THEN ApiKeyCifrata ELSE @NuovaChiave END,
                      TenantId = @TenantId, ClientId = @ClientId, DatiLiberi = @DatiLiberi, Regione = @Regione, ProgettoId = @ProgettoId,
                      MaxCaratteriDati = @MaxCaratteriDati, MaxTokenMotore = @MaxTokenMotore, DataModifica = SYSUTCDATETIME()
                  OUTPUT INSERTED.Id
                  WHERE Id = @Id
                  """, connection, transaction);
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@Nome", SqlDbType.NVarChar, 100).Value = nome.Trim();
            command.Parameters.Add("@Predefinita", SqlDbType.Bit).Value = predefinita;
            command.Parameters.Add("@Rimuovi", SqlDbType.Bit).Value = rimuoviApiKey;
            command.Parameters.Add("@NuovaChiave", SqlDbType.NVarChar, -1).Value =
                string.IsNullOrEmpty(apiKey) ? DBNull.Value : protector.Protect(apiKey);
            AggiungiParametri(command, impostazioni);
            id = (int)(await command.ExecuteScalarAsync(cancellationToken))!;

            // Una sola configurazione predefinita
            if (predefinita)
            {
                await using var altre = new SqlCommand(
                    "UPDATE dbo.AiConfigurazioni SET Predefinita = 0 WHERE Id <> @Id AND Predefinita = 1", connection, transaction);
                altre.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                await altre.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return id;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new InvalidOperationException($"Esiste già una configurazione con nome '{nome}'.");
        }
    }

    /// <summary>Elimina una configurazione: gli utenti che la usavano non sono più abilitati all'AI.</summary>
    public async Task EliminaAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken, soloAmministratore: true);
        await using var command = new SqlCommand("DELETE FROM dbo.AiConfigurazioni WHERE Id = @Id", connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Importa il vecchio ai.json se il database non ha ancora configurazioni: diventa la predefinita
    /// ed è assegnata agli amministratori attivi; il file viene rinominato.
    /// </summary>
    private async Task ImportaFileAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        if (!File.Exists(opzioni.FileLegacy))
            return;

        await using (var conta = new SqlCommand("SELECT COUNT(*) FROM dbo.AiConfigurazioni", connection))
            if ((int)(await conta.ExecuteScalarAsync(cancellationToken))! > 0)
                return;

        var legacy = JsonSerializer.Deserialize<FileLegacy>(await File.ReadAllTextAsync(opzioni.FileLegacy, cancellationToken));
        if (legacy is null)
            return;

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new SqlCommand("""
            INSERT INTO dbo.AiConfigurazioni (Nome, Predefinita, Provider, Endpoint, Modello, SystemPrompt, ApiKeyCifrata,
                TenantId, ClientId, DatiLiberi, Regione, ProgettoId, MaxCaratteriDati, MaxTokenMotore)
            OUTPUT INSERTED.Id
            VALUES (N'Configurazione importata', 1, @Provider, @Endpoint, @Modello, @SystemPrompt, @NuovaChiave,
                @TenantId, @ClientId, @DatiLiberi, @Regione, @ProgettoId, @MaxCaratteriDati, @MaxTokenMotore)
            """, connection, transaction);
        command.Parameters.Add("@NuovaChiave", SqlDbType.NVarChar, -1).Value = (object?)legacy.ApiKeyCifrata ?? DBNull.Value;
        AggiungiParametri(command, new AiImpostazioni(
            legacy.Provider, legacy.Endpoint, legacy.Modello, legacy.SystemPrompt, legacy.ApiKeyCifrata is not null,
            legacy.TenantId, legacy.ClientId, legacy.DatiLiberi, legacy.Regione, legacy.ProgettoId,
            legacy.MaxCaratteriDati, legacy.MaxTokenMotore));
        var id = (int)(await command.ExecuteScalarAsync(cancellationToken))!;

        await using (var assegna = new SqlCommand("""
            UPDATE u SET AiConfigurazioneId = @Id
            FROM dbo.Utenti u
            WHERE u.Attivo = 1 AND EXISTS (
                SELECT 1 FROM dbo.UtentiRuoli ur JOIN dbo.Ruoli r ON r.Id = ur.RuoloId
                WHERE ur.UtenteId = u.Id AND r.Codice = @Amministratore)
            """, connection, transaction))
        {
            assegna.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            assegna.Parameters.Add("@Amministratore", SqlDbType.NVarChar, 50).Value = UserSessionService.RuoloAmministratore;
            await assegna.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        File.Move(opzioni.FileLegacy, opzioni.FileLegacy + ".importato", overwrite: true);
    }

    private static void AggiungiParametri(SqlCommand command, AiImpostazioni i)
    {
        command.Parameters.Add("@Provider", SqlDbType.NVarChar, 50).Value = i.Provider.ToString();
        command.Parameters.Add("@Endpoint", SqlDbType.NVarChar, 500).Value = Testo(i.Endpoint);
        command.Parameters.Add("@Modello", SqlDbType.NVarChar, 200).Value = Testo(i.Modello);
        command.Parameters.Add("@SystemPrompt", SqlDbType.NVarChar, -1).Value = Testo(i.SystemPrompt);
        command.Parameters.Add("@TenantId", SqlDbType.NVarChar, 100).Value = Testo(i.TenantId);
        command.Parameters.Add("@ClientId", SqlDbType.NVarChar, 100).Value = Testo(i.ClientId);
        command.Parameters.Add("@DatiLiberi", SqlDbType.Bit).Value = i.DatiLiberi;
        command.Parameters.Add("@Regione", SqlDbType.NVarChar, 50).Value = Testo(i.Regione);
        command.Parameters.Add("@ProgettoId", SqlDbType.NVarChar, 200).Value = Testo(i.ProgettoId);
        command.Parameters.Add("@MaxCaratteriDati", SqlDbType.Int).Value = i.MaxCaratteriDati;
        command.Parameters.Add("@MaxTokenMotore", SqlDbType.Int).Value = i.MaxTokenMotore;
    }

    private static (ConfigurazioneAi Configurazione, string? ApiKeyCifrata) Leggi(SqlDataReader r)
    {
        string? S(int i) => r.IsDBNull(i) ? null : r.GetString(i);
        var chiave = S(7);
        var impostazioni = new AiImpostazioni(
            Enum.TryParse<AiProvider>(r.GetString(3), out var provider) ? provider : AiProvider.Simulato,
            S(4) ?? string.Empty, S(5) ?? string.Empty, S(6) ?? AiImpostazioni.SystemPromptPredefinito, chiave is not null,
            S(8), S(9), r.GetBoolean(10), S(11), S(12), r.GetInt32(13), r.GetInt32(14));
        return (new ConfigurazioneAi(r.GetInt32(0), r.GetString(1), r.GetBoolean(2), impostazioni), chiave);
    }

    private string? Decifra(string? cifrata) => cifrata is null ? null : protector.Unprotect(cifrata);

    private static object Testo(string? valore) => string.IsNullOrWhiteSpace(valore) ? DBNull.Value : valore.Trim();

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken, bool soloAmministratore = false)
    {
        if (soloAmministratore && !sessione.IsInRole(UserSessionService.RuoloAmministratore))
            throw new InvalidOperationException("Operazione riservata agli amministratori.");

        var connection = new SqlConnection(sessione.ConnectionString
            ?? throw new InvalidOperationException("Nessuna connessione al database attiva."));
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
