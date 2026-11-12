using System.Diagnostics;
using GpOne.Core;
using GpOne.Motore;
using Microsoft.Data.SqlClient;

namespace GpOne.Desktop;

/// <summary>
/// Equivalente desktop di DomandeService dell'API: stessa logica (<see cref="CicloDomanda"/>), ma la SELECT si esegue
/// direttamente sul database del profilo e struttura/storico stanno nell'archivio locale.
/// All'AI vanno solo struttura abilitata, domanda, query sbagliate con i commenti ed errori ripuliti; i dati solo anonimizzati
/// (se il profilo lo consente), mai in chiaro.
/// </summary>
public sealed class ServizioDomande(Profilo profilo)
{
    /// <summary>Quante risposte sbagliate precedenti (della stessa catena di riprova) si mostrano all'AI.</summary>
    private const int MaxRisposteSbagliate = 5;

    /// <summary>Client HTTP condiviso per Ollama e Gemini (la libreria Anthropic usa il suo).</summary>
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private static readonly IGeneratoreSql Generatore =
        new GeneratoreSql(new ClaudeGeneratore(), new OllamaGeneratore(() => Http), new GeminiGeneratore(() => Http));
    private static readonly ElencoModelli Modelli = new(() => Http);

    /// <summary>Modelli utilizzabili con il motore e le credenziali indicati (in <see cref="ConfigurazioneAi.ApiKey"/> il segreto in chiaro).</summary>
    public static Task<IReadOnlyList<string>> CaricaModelliAsync(ConfigurazioneAi configurazione, CancellationToken cancellationToken = default) =>
        Modelli.CaricaAsync(configurazione, cancellationToken);

    private readonly object blocco = new();

    public Profilo Profilo { get; } = profilo;

    public Task<RispostaDomanda> ChiediAsync(string domanda, CancellationToken cancellationToken) =>
        EseguiAsync(domanda, sbagliate: null, origineId: null, cancellationToken);

    /// <summary>
    /// La risposta <paramref name="domandaId"/> è sbagliata: la si segna con il commento e si chiede all'AI una nuova query,
    /// mostrandole le query già giudicate sbagliate (con i commenti) della stessa catena. Mai i risultati.
    /// </summary>
    public Task<RispostaDomanda> RiprovaAsync(long domandaId, string? commento, CancellationToken cancellationToken)
    {
        string testo;
        List<RispostaSbagliata> sbagliate;
        lock (blocco)
        {
            var storico = Archivio.Storico(Profilo.Id);
            var domanda = storico.FirstOrDefault(d => d.Id == domandaId)
                ?? throw new InvalidOperationException("Domanda non trovata nello storico.");
            domanda.Valutazione = Valutazioni.Sbagliata;
            domanda.CommentoValutazione = Tronca(string.IsNullOrWhiteSpace(commento) ? null : commento.Trim(), 1000);
            Archivio.SalvaStorico(Profilo.Id, storico);
            testo = domanda.Testo;

            // Risale la catena delle riprove e prende le risposte giudicate sbagliate, dalla più vecchia
            var perId = storico.ToDictionary(d => d.Id);
            var catena = new List<DomandaSalvata>();
            for (var d = domanda; d is not null && catena.Count < 20; d = d.DomandaOrigineId is { } o ? perId.GetValueOrDefault(o) : null)
                catena.Add(d);
            sbagliate = [.. catena.Where(d => d.Valutazione == Valutazioni.Sbagliata)
                .Take(MaxRisposteSbagliate)
                .Reverse()
                .Select(d => new RispostaSbagliata(d.Sql, d.CommentoValutazione))];
        }
        return EseguiAsync(testo, sbagliate, domandaId, cancellationToken);
    }

    /// <summary>Conferma (o annulla la conferma): le risposte corrette diventano esempi per l'AI.</summary>
    public void Valuta(long domandaId, bool corretta)
    {
        lock (blocco)
        {
            var storico = Archivio.Storico(Profilo.Id);
            var domanda = storico.FirstOrDefault(d => d.Id == domandaId)
                ?? throw new InvalidOperationException("Domanda non trovata nello storico.");
            if (corretta && (domanda.Esito != Esiti.Ok || domanda.Sql is null))
                throw new InvalidOperationException("Si possono confermare solo le risposte eseguite con successo.");
            domanda.Valutazione = corretta ? Valutazioni.Corretta : null;
            domanda.CommentoValutazione = null;
            Archivio.SalvaStorico(Profilo.Id, storico);
        }
    }

    public List<DomandaSalvata> Storico(int limite = 200)
    {
        lock (blocco)
            return [.. Archivio.Storico(Profilo.Id).OrderByDescending(d => d.Id).Take(limite)];
    }

    /// <summary>Riapre una domanda dello storico rieseguendo la query salvata (senza chiamare l'AI).</summary>
    public async Task<RispostaDomanda> ApriAsync(long domandaId, CancellationToken cancellationToken)
    {
        DomandaSalvata d;
        lock (blocco)
            d = Archivio.Storico(Profilo.Id).FirstOrDefault(x => x.Id == domandaId)
                ?? throw new InvalidOperationException("Domanda non trovata nello storico.");

        RispostaDomanda Risposta(RisultatoQuery? r, int durata, string? errore) =>
            new(d.Id, d.Testo, d.Sql, d.Spiegazione ?? "", d.Grafico, r, durata, errore, d.Valutazione, d.Chiarimento, d.DatiInviati);

        if (d.Sql is null || d.Esito != Esiti.Ok)
            return Risposta(null, 0, d.Errore);

        // Ri-validata sulla struttura attuale: una tabella o colonna disabilitata dopo non deve rientrare
        var catena = await CatenaSql.EseguiAsync(d.Sql, Motore.Struttura.Consentiti(Abilitati()), Profilo.Ai.MaxRighe, Profilo.Ai.TimeoutSecondi,
            EseguiSqlAsync, cancellationToken);
        return catena.Risultato is { } risultato ? Risposta(risultato, risultato.DurataMs, null) : Risposta(null, 0, catena.Errore);
    }

    private async Task<RispostaDomanda> EseguiAsync(string domanda, IReadOnlyList<RispostaSbagliata>? sbagliate, long? origineId,
        CancellationToken cancellationToken)
    {
        var cronometro = Stopwatch.StartNew();
        var configurazione = Profilo.AiConChiave();
        if (configurazione.Verifica() is { } erroreConfigurazione)
            throw new InvalidOperationException($"Motore AI non configurato: {erroreConfigurazione}");
        if (ProviderAi.RichiedeChiave(configurazione.Provider) && string.IsNullOrWhiteSpace(configurazione.ApiKey))
            throw new InvalidOperationException(configurazione.Provider == ProviderAi.GeminiVertex
                ? "Account di servizio di Vertex AI non configurato: aprire Profilo › Connessione e motore AI."
                : $"Chiave API di {configurazione.Provider} non configurata: aprire Profilo › Connessione e motore AI.");

        var abilitati = Abilitati();
        if (abilitati.Count == 0)
            throw new InvalidOperationException("Nessuna tabella abilitata: configurare la struttura in Profilo › Struttura del database.");

        IEnumerable<EsempioValidato> esempi;
        lock (blocco)
            esempi = [.. Archivio.Storico(Profilo.Id)
                .Where(d => d.Valutazione == Valutazioni.Corretta && d.Sql is not null)
                .OrderByDescending(d => d.Id)
                .Take(PromptBuilder.MaxEsempi * 3)
                .Select(d => new EsempioValidato(d.Testo, d.Sql!))];

        var esitoDomanda = await CicloDomanda.EseguiAsync(Generatore, configurazione, abilitati, esempi,
            domanda, sbagliate, EseguiSqlAsync, logger: null, cancellationToken);
        var (proposta, esito, errore, risultato) = esitoDomanda;

        var durata = (int)cronometro.ElapsedMilliseconds;
        DomandaSalvata salvata;
        lock (blocco)
        {
            var storico = Archivio.Storico(Profilo.Id);
            salvata = new DomandaSalvata
            {
                Id = storico.Count == 0 ? 1 : storico.Max(d => d.Id) + 1,
                DataOra = DateTime.Now,
                Testo = Tronca(domanda, 2000)!,
                Sql = proposta?.Sql is { Length: > 0 } s ? s : null,
                Spiegazione = Tronca(proposta?.Spiegazione, 2000),
                Grafico = proposta?.Grafico,
                Esito = esito,
                Errore = Tronca(errore, 2000),
                Righe = risultato?.Righe.Count,
                DurataMs = durata,
                DomandaOrigineId = origineId,
                Chiarimento = Tronca(proposta?.Chiarimento, 2000),
                DatiInviati = esitoDomanda.DatiInviati
            };
            storico.Add(salvata);
            Archivio.SalvaStorico(Profilo.Id, storico);
        }

        return new RispostaDomanda(salvata.Id, domanda, salvata.Sql, proposta?.Spiegazione ?? "", proposta?.Grafico,
            risultato, durata, errore, null, salvata.Chiarimento, salvata.DatiInviati);
    }

    /// <summary>
    /// Risposta dell'utente al chiarimento chiesto dall'AI per la domanda <paramref name="domandaId"/>: si richiama il ciclo
    /// con la domanda originale, il chiarimento e la risposta.
    /// </summary>
    public Task<RispostaDomanda> RispondiChiarimentoAsync(long domandaId, string risposta, CancellationToken cancellationToken)
    {
        DomandaSalvata domanda;
        lock (blocco)
            domanda = Archivio.Storico(Profilo.Id).FirstOrDefault(d => d.Id == domandaId)
                ?? throw new InvalidOperationException("Domanda non trovata nello storico.");
        if (domanda.Chiarimento is null)
            throw new InvalidOperationException("Per questa domanda l'AI non ha chiesto chiarimenti.");
        return EseguiAsync(PromptBuilder.DomandaConChiarimento(domanda.Testo, domanda.Chiarimento, domanda.DatiInviati, risposta), sbagliate: null, domandaId, cancellationToken);
    }

    private List<OggettoConfigurato> Abilitati()
    {
        lock (blocco)
            return Motore.Struttura.SoloAbilitati(Archivio.Struttura(Profilo.Id));
    }

    private Task<RisultatoQuery> EseguiSqlAsync(RichiestaQuery richiesta, CancellationToken cancellationToken) =>
        EsecutoreSql.EseguiQueryAsync(Profilo.StringaConnessione(), richiesta, cancellationToken);

    // ---- Struttura e collegamento ----

    /// <summary>
    /// Rilegge la struttura dal database (solo INFORMATION_SCHEMA) e la allinea a quella salvata: aggiunge oggetti
    /// (disabilitati) e colonne (abilitate) nuovi, aggiorna i tipi, toglie ciò che non esiste più. Abilitazioni e descrizioni restano.
    /// </summary>
    public static async Task<List<OggettoConfigurato>> AllineaStrutturaAsync(Profilo profilo, List<OggettoConfigurato> attuali,
        CancellationToken cancellationToken = default)
    {
        var letti = await EsecutoreSql.LeggiSchemaAsync(profilo.StringaConnessione(), cancellationToken);
        var esistenti = attuali.ToDictionary(o => (o.SchemaNome.ToLowerInvariant(), o.Nome.ToLowerInvariant()));
        var prossimoId = attuali.Select(o => o.Id).DefaultIfEmpty(0).Max() + 1;

        var risultato = new List<OggettoConfigurato>();
        foreach (var letto in letti)
        {
            var oggetto = esistenti.GetValueOrDefault((letto.Schema.ToLowerInvariant(), letto.Nome.ToLowerInvariant()))
                ?? new OggettoConfigurato { Id = prossimoId++, SchemaNome = letto.Schema, Nome = letto.Nome };
            oggetto.Tipo = letto.Tipo;

            var colonneEsistenti = oggetto.Colonne.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);
            oggetto.Colonne = [.. letto.Colonne.Select(c =>
            {
                var colonna = colonneEsistenti.GetValueOrDefault(c.Nome)
                    ?? new ColonnaConfigurata { OggettoId = oggetto.Id, Nome = c.Nome, Abilitata = true };
                colonna.TipoDati = c.TipoDati;
                colonna.Ordine = c.Ordine;
                return colonna;
            })];
            risultato.Add(oggetto);
        }
        return [.. risultato.OrderBy(o => o.SchemaNome).ThenBy(o => o.Nome)];
    }

    /// <summary>Prova il collegamento e controlla i permessi dell'utente SQL: restituisce un riepilogo da mostrare.</summary>
    public static async Task<string> ProvaConnessioneAsync(Profilo profilo, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(profilo.StringaConnessione());
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("""
                SELECT DB_NAME(), SUSER_SNAME(),
                       IS_SRVROLEMEMBER('sysadmin'), IS_ROLEMEMBER('db_owner'), IS_ROLEMEMBER('db_datawriter'), IS_ROLEMEMBER('db_ddladmin')
                """, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);

            var testo = $"Collegamento riuscito: database '{reader.GetString(0)}' come '{reader.GetString(1)}'.";
            var permessiAmpi = new[] { (2, "sysadmin"), (3, "db_owner"), (4, "db_datawriter"), (5, "db_ddladmin") }
                .Where(p => !reader.IsDBNull(p.Item1) && reader.GetInt32(p.Item1) == 1)
                .Select(p => p.Item2)
                .ToList();
            if (permessiAmpi.Count > 0)
                testo += $"\n\nAttenzione: l'utente ha permessi più ampi della sola lettura ({string.Join(", ", permessiAmpi)}). " +
                         "Le query sono comunque controllate ed eseguite in una transazione annullata, ma è consigliato un utente con il solo ruolo db_datareader.";
            return testo;
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException($"Collegamento non riuscito. {DiagnosticaSql.Suggerimento(ex.Number, ex.Message)}\n\nErrore SQL {ex.Number}: {ex.Message}", ex);
        }
    }

    private static string? Tronca(string? testo, int max) => testo is null || testo.Length <= max ? testo : testo[..max];
}
