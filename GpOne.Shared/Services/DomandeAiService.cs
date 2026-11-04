using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace GpOne.Services;

/// <summary>Tabella/vista da cui leggere i dati per una domanda preparata.</summary>
public class FonteDati
{
    /// <summary>Nome completo schema.nome della tabella o vista.</summary>
    public string Oggetto { get; set; } = string.Empty;

    /// <summary>Colonne da inviare; vuoto = tutte.</summary>
    public List<string> Colonne { get; set; } = [];

    /// <summary>Condizione WHERE, senza la parola WHERE.</summary>
    public string? Filtro { get; set; }

    /// <summary>Ordinamento, senza le parole ORDER BY.</summary>
    public string? Ordinamento { get; set; }

    /// <summary>Numero massimo di righe da inviare; null = tutte.</summary>
    public int? MaxRighe { get; set; }

    /// <summary>Colonne di raggruppamento (GROUP BY).</summary>
    public List<string> Raggruppamento { get; set; } = [];

    /// <summary>Totali calcolati dal database (SUM, COUNT, AVG, MIN, MAX).</summary>
    public List<Aggregato> Aggregati { get; set; } = [];
}

/// <summary>Funzione di aggregazione su una colonna; per COUNT la colonna vuota significa COUNT(*).</summary>
public class Aggregato
{
    public static readonly string[] Funzioni = ["SUM", "COUNT", "AVG", "MIN", "MAX"];

    public string Funzione { get; set; } = "SUM";
    public string Colonna { get; set; } = string.Empty;
    public string? Alias { get; set; }
}

public class DomandaAi
{
    public int Id { get; set; }
    public string Titolo { get; set; } = string.Empty;
    public string Testo { get; set; } = string.Empty;
    public bool Attiva { get; set; } = true;
    public List<FonteDati> Fonti { get; set; } = [];

    /// <summary>Codici dei ruoli che vedono la domanda; vuoto = tutti.</summary>
    public List<string> Ruoli { get; set; } = [];
}

/// <summary>
/// Domande preparate dall'amministratore: testo per l'AI più le tabelle, le colonne e i filtri dei dati da inviare.
/// Le query sono costruite da GpOne (non dall'AI) e verificate come quelle di <see cref="DatiAiService"/>.
/// Le tabelle scelte nella domanda sono autorizzate dall'amministratore stesso: non serve abilitarle in "Dati per AI"
/// (restano comunque escluse le tabelle di sistema di GpOne).
/// </summary>
public class DomandeAiService(UserSessionService sessione, DatiAiService dati)
{
    /// <summary>Righe mostrate al massimo nell'anteprima.</summary>
    public const int RigheAnteprima = 200;

    public async Task<IReadOnlyList<DomandaAi>> ElencoAsync(bool soloAttive, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var domande = new Dictionary<int, DomandaAi>();

        await using (var command = new SqlCommand(
            $"SELECT Id, Titolo, Testo, Attiva FROM dbo.AiDomande {(soloAttive ? "WHERE Attiva = 1" : "")} ORDER BY Titolo", connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                domande[reader.GetInt32(0)] = new DomandaAi
                {
                    Id = reader.GetInt32(0),
                    Titolo = reader.GetString(1),
                    Testo = reader.GetString(2),
                    Attiva = reader.GetBoolean(3)
                };
        }

        await using (var command = new SqlCommand("""
            SELECT DomandaId, SchemaNome, OggettoNome, Colonne, Filtro, Ordinamento, MaxRighe, Raggruppamento, Aggregati
            FROM dbo.AiDomandeFonti
            ORDER BY DomandaId, Ordine, Id
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!domande.TryGetValue(reader.GetInt32(0), out var domanda))
                    continue;
                domanda.Fonti.Add(new FonteDati
                {
                    Oggetto = $"{reader.GetString(1)}.{reader.GetString(2)}",
                    Colonne = reader.IsDBNull(3) ? [] : [.. reader.GetString(3).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
                    Filtro = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Ordinamento = reader.IsDBNull(5) ? null : reader.GetString(5),
                    MaxRighe = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    Raggruppamento = reader.IsDBNull(7) ? [] : [.. reader.GetString(7).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
                    Aggregati = reader.IsDBNull(8) ? [] : JsonSerializer.Deserialize<List<Aggregato>>(reader.GetString(8)) ?? []
                });
            }
        }

        // Visibilità per ruolo (tabella creata dallo script 09: se manca, domande visibili a tutti)
        try
        {
            await using var command = new SqlCommand("""
                SELECT dr.DomandaId, r.Codice
                FROM dbo.AiDomandeRuoli dr
                JOIN dbo.Ruoli r ON r.Id = dr.RuoloId
                """, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                if (domande.TryGetValue(reader.GetInt32(0), out var domanda))
                    domanda.Ruoli.Add(reader.GetString(1));
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
        }

        return [.. domande.Values];
    }

    /// <summary>Domande attive visibili all'utente collegato in base ai suoi ruoli.</summary>
    public async Task<IReadOnlyList<DomandaAi>> ElencoPerUtenteAsync(CancellationToken cancellationToken = default) =>
        [.. (await ElencoAsync(soloAttive: true, cancellationToken)).Where(d => d.Ruoli.Count == 0 || d.Ruoli.Any(sessione.IsInRole))];

    public async Task<IReadOnlyList<string>> ElencoRuoliAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT Codice FROM dbo.Ruoli WHERE Attivo = 1 ORDER BY Codice", connection);
        var ruoli = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            ruoli.Add(reader.GetString(0));
        return ruoli;
    }

    // Parametri: {Nome} nel testo della domanda e nei filtri; nei filtri diventano parametri SQL @p_Nome
    private static readonly System.Text.RegularExpressions.Regex Segnaposto = new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}");

    /// <summary>Nomi dei parametri usati nel testo e nei filtri della domanda.</summary>
    public static IReadOnlyList<string> Parametri(DomandaAi domanda) =>
        [.. new[] { domanda.Testo }.Concat(domanda.Fonti.Select(f => f.Filtro ?? string.Empty))
            .SelectMany(t => Segnaposto.Matches(t).Select(m => m.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Sostituisce i parametri nel testo della domanda con i valori indicati dall'utente.</summary>
    public static string ApplicaParametri(string testo, IReadOnlyDictionary<string, string> valori) =>
        Segnaposto.Replace(testo, m => valori.TryGetValue(m.Groups[1].Value, out var valore) ? valore : m.Value);

    // I valori arrivano al database come parametri SQL, mai concatenati nella query
    private static Dictionary<string, object> ParametriSql(FonteDati fonte, IReadOnlyDictionary<string, string>? valori) =>
        Segnaposto.Matches(fonte.Filtro ?? string.Empty)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                nome => $"@p_{nome}",
                nome => valori is not null && valori.TryGetValue(nome, out var valore) && valore.Length > 0 ? (object)valore : DBNull.Value);

    public async Task SalvaAsync(DomandaAi domanda, CancellationToken cancellationToken = default)
    {
        // Le query devono superare la verifica prima del salvataggio
        foreach (var fonte in domanda.Fonti)
            await dati.VerificaAsync(CostruisciQuery(fonte), soloConsentiti: false, cancellationToken);
        var ruoli = domanda.Ruoli.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = new SqlCommand(domanda.Id == 0
            ? "INSERT INTO dbo.AiDomande (Titolo, Testo, Attiva) OUTPUT INSERTED.Id VALUES (@Titolo, @Testo, @Attiva)"
            : "UPDATE dbo.AiDomande SET Titolo = @Titolo, Testo = @Testo, Attiva = @Attiva, DataModifica = SYSUTCDATETIME() OUTPUT INSERTED.Id WHERE Id = @Id",
            connection, transaction))
        {
            command.Parameters.Add("@Id", SqlDbType.Int).Value = domanda.Id;
            command.Parameters.Add("@Titolo", SqlDbType.NVarChar, 200).Value = domanda.Titolo.Trim();
            command.Parameters.Add("@Testo", SqlDbType.NVarChar, -1).Value = domanda.Testo.Trim();
            command.Parameters.Add("@Attiva", SqlDbType.Bit).Value = domanda.Attiva;
            domanda.Id = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        }

        await using (var command = new SqlCommand("DELETE FROM dbo.AiDomandeFonti WHERE DomandaId = @Id", connection, transaction))
        {
            command.Parameters.Add("@Id", SqlDbType.Int).Value = domanda.Id;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        for (var i = 0; i < domanda.Fonti.Count; i++)
        {
            var fonte = domanda.Fonti[i];
            var (schema, nome) = Dividi(fonte.Oggetto);
            await using var command = new SqlCommand("""
                INSERT INTO dbo.AiDomandeFonti (DomandaId, Ordine, SchemaNome, OggettoNome, Colonne, Filtro, Ordinamento, MaxRighe, Raggruppamento, Aggregati)
                VALUES (@DomandaId, @Ordine, @Schema, @Nome, @Colonne, @Filtro, @Ordinamento, @MaxRighe, @Raggruppamento, @Aggregati)
                """, connection, transaction);
            command.Parameters.Add("@DomandaId", SqlDbType.Int).Value = domanda.Id;
            command.Parameters.Add("@Ordine", SqlDbType.Int).Value = i;
            command.Parameters.Add("@Schema", SqlDbType.NVarChar, 128).Value = schema;
            command.Parameters.Add("@Nome", SqlDbType.NVarChar, 128).Value = nome;
            command.Parameters.Add("@Colonne", SqlDbType.NVarChar, -1).Value = fonte.Colonne.Count == 0 ? DBNull.Value : string.Join(",", fonte.Colonne);
            command.Parameters.Add("@Filtro", SqlDbType.NVarChar, -1).Value = Vuoto(fonte.Filtro) ? DBNull.Value : fonte.Filtro!.Trim();
            command.Parameters.Add("@Ordinamento", SqlDbType.NVarChar, 500).Value = Vuoto(fonte.Ordinamento) ? DBNull.Value : fonte.Ordinamento!.Trim();
            command.Parameters.Add("@MaxRighe", SqlDbType.Int).Value = fonte.MaxRighe is { } max ? Math.Max(1, max) : DBNull.Value;
            command.Parameters.Add("@Raggruppamento", SqlDbType.NVarChar, -1).Value =
                fonte.Raggruppamento.Count == 0 ? DBNull.Value : string.Join(",", fonte.Raggruppamento);
            command.Parameters.Add("@Aggregati", SqlDbType.NVarChar, -1).Value =
                fonte.Aggregati.Count == 0 ? DBNull.Value : JsonSerializer.Serialize(fonte.Aggregati);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = new SqlCommand("""
            IF OBJECT_ID(N'dbo.AiDomandeRuoli', N'U') IS NOT NULL
                DELETE FROM dbo.AiDomandeRuoli WHERE DomandaId = @Id
            """, connection, transaction))
        {
            command.Parameters.Add("@Id", SqlDbType.Int).Value = domanda.Id;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var ruolo in ruoli)
        {
            await using var command = new SqlCommand("""
                INSERT INTO dbo.AiDomandeRuoli (DomandaId, RuoloId)
                SELECT @Id, Id FROM dbo.Ruoli WHERE Codice = @Codice
                """, connection, transaction);
            command.Parameters.Add("@Id", SqlDbType.Int).Value = domanda.Id;
            command.Parameters.Add("@Codice", SqlDbType.NVarChar, 50).Value = ruolo;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task EliminaAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand("DELETE FROM dbo.AiDomande WHERE Id = @Id", connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Dati che verrebbero inviati all'AI per questa fonte (anteprima per l'amministratore).</summary>
    public Task<string> AnteprimaAsync(FonteDati fonte, CancellationToken cancellationToken = default) =>
        dati.EseguiVerificataAsync(CostruisciQuery(fonte), Math.Min(fonte.MaxRighe ?? int.MaxValue, RigheAnteprima), soloConsentiti: false,
            cancellationToken, ParametriSql(fonte, null));

    /// <summary>Legge i dati delle fonti e li allega all'ultima domanda della conversazione.</summary>
    public async Task<ContestoDati> PreparaAsync(DomandaAi domanda, IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? valori = null)
    {
        var query = new List<string>();
        var testo = new System.Text.StringBuilder();
        var dettagli = new System.Text.StringBuilder();

        // Il limite di dimensione viene diviso tra le tabelle della domanda
        var maxCaratteriFonte = dati.MaxCaratteriDati / Math.Max(1, domanda.Fonti.Count);

        foreach (var fonte in domanda.Fonti)
        {
            var sql = CostruisciQuery(fonte);
            query.Add(sql);
            string risultato;
            try
            {
                risultato = await dati.EseguiVerificataAsync(sql, fonte.MaxRighe ?? int.MaxValue, soloConsentiti: false,
                    cancellationToken, ParametriSql(fonte, valori));
            }
            catch (Exception ex) when (ex is InvalidOperationException or SqlException)
            {
                risultato = $"(errore nella lettura: {ex.Message})";
            }

            var (blocco, omesse) = DatiAiService.BloccoDati(fonte.Oggetto, sql, risultato, maxCaratteriFonte);
            testo.AppendLine().AppendLine(blocco);
            dettagli.AppendLine(blocco);
            if (omesse > 0)
                dettagli.AppendLine($"Attenzione: {omesse} righe non inviate per limite di dimensione.");
            dettagli.AppendLine();
        }

        // Domanda in fondo: se il contesto del motore (es. Ollama) è stretto viene troncato l'inizio, non la domanda
        if (domanda.Fonti.Count > 0)
            testo.AppendLine().AppendLine(DatiAiService.IstruzioniDati).AppendLine().Append("Domanda: ");
        testo.Append(history[^1].Text);

        return new ContestoDati(
            [.. history.Take(history.Count - 1), history[^1] with { Text = testo.ToString().Trim() }],
            dettagli.Length > 0 ? dettagli.ToString().TrimEnd() : null,
            query.Count > 0 ? string.Join("\n", query) : null);
    }

    /// <summary>SELECT costruita dalle scelte dell'amministratore; filtro e ordinamento vengono verificati dal parser.</summary>
    public static string CostruisciQuery(FonteDati fonte)
    {
        var (schema, nome) = Dividi(fonte.Oggetto);
        var raggruppa = fonte.Raggruppamento.Count > 0 || fonte.Aggregati.Count > 0;

        // Con raggruppamenti o totali si selezionano solo le colonne raggruppate più i totali
        var colonne = raggruppa
            ? string.Join(", ", fonte.Raggruppamento.Select(Quota).Concat(fonte.Aggregati.Select(Espressione)))
            : fonte.Colonne.Count == 0 ? "*" : string.Join(", ", fonte.Colonne.Select(Quota));
        var top = fonte.MaxRighe is { } max ? $"TOP ({Math.Max(1, max)}) " : string.Empty;
        var sql = $"SELECT {top}{colonne} FROM {Quota(schema)}.{Quota(nome)}";
        if (!Vuoto(fonte.Filtro))
            sql += $" WHERE {Segnaposto.Replace(fonte.Filtro!.Trim(), m => $"@p_{m.Groups[1].Value}")}";
        if (fonte.Raggruppamento.Count > 0)
            sql += $" GROUP BY {string.Join(", ", fonte.Raggruppamento.Select(Quota))}";
        if (!Vuoto(fonte.Ordinamento))
            sql += $" ORDER BY {fonte.Ordinamento!.Trim()}";
        return sql;
    }

    private static string Espressione(Aggregato aggregato)
    {
        var funzione = aggregato.Funzione.ToUpperInvariant();
        if (!Aggregato.Funzioni.Contains(funzione))
            throw new InvalidOperationException($"Funzione di aggregazione non consentita: {aggregato.Funzione}.");

        var colonna = Vuoto(aggregato.Colonna) || aggregato.Colonna == "*" ? null : aggregato.Colonna.Trim();
        if (colonna is null && funzione != "COUNT")
            throw new InvalidOperationException($"Indicare la colonna per {funzione}.");

        var alias = Vuoto(aggregato.Alias) ? $"{funzione}_{colonna ?? "righe"}" : aggregato.Alias!.Trim();
        return $"{funzione}({(colonna is null ? "*" : Quota(colonna))}) AS {Quota(alias)}";
    }

    private static (string Schema, string Nome) Dividi(string oggetto)
    {
        var punto = oggetto.IndexOf('.');
        return punto < 0 ? ("dbo", oggetto) : (oggetto[..punto], oggetto[(punto + 1)..]);
    }

    private static string Quota(string nome) => $"[{nome.Replace("]", "]]")}]";

    private static bool Vuoto(string? testo) => string.IsNullOrWhiteSpace(testo);

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(sessione.ConnectionString
            ?? throw new InvalidOperationException("Nessuna connessione al database attiva."));
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
