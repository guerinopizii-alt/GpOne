using System.Diagnostics;
using System.Text.Json;
using Dapper;
using GpOne.Api.Agenti;
using GpOne.Api.Ai;
using GpOne.Api.Infrastruttura;
using GpOne.Api.Schema;
using GpOne.Core;
using GpOne.Motore;

namespace GpOne.Api.Domande;

public record RispostaDomanda(
    long Id,
    string Domanda,
    string? Sql,
    string Spiegazione,
    GraficoProposto? Grafico,
    IReadOnlyList<ColonnaRisultato> Colonne,
    IReadOnlyList<object?[]> Righe,
    bool Troncato,
    int DurataMs,
    string? Errore,
    string? Valutazione = null,
    string? Chiarimento = null,
    bool DatiInviati = false);

public record VoceStorico(long Id, string Testo, string? SqlGenerato, string Esito, int? Righe, DateTime DataOra, string? Valutazione);

public static class Valutazioni
{
    public const string Corretta = "Corretta";
    public const string Sbagliata = "Sbagliata";
}

/// <summary>
/// Domanda → l'AI (struttura + domanda) propone una o più SELECT collegate da segnaposto → validazione → esecuzione tramite
/// l'agente del cliente (<see cref="CicloDomanda"/>). I risultati vanno all'utente; all'AI tornano al massimo risultati
/// anonimizzati (se il cliente lo consente) e messaggi d'errore ripuliti, mai valori in chiaro.
/// </summary>
public class DomandeService(
    GpOneDatabase database,
    SchemaService schema,
    ConfigurazioneAiService configurazioni,
    IGeneratoreSql generatore,
    AgenteClient agente,
    ILogger<DomandeService> logger)
{
    /// <summary>Quante risposte sbagliate precedenti (della stessa catena di riprova) si mostrano all'AI.</summary>
    private const int MaxRisposteSbagliate = 5;

    public Task<RispostaDomanda> ChiediAsync(int utenteId, int clienteId, string domanda, CancellationToken cancellationToken) =>
        EseguiAsync(utenteId, clienteId, domanda, sbagliate: null, origineId: null, cancellationToken);

    /// <summary>
    /// L'utente giudica sbagliata la risposta <paramref name="domandaId"/>: la si segna con il suo commento e si chiede all'AI
    /// una nuova query, mostrandole le query già giudicate sbagliate (con i commenti) della stessa catena. Mai i risultati.
    /// Restituisce null se la domanda non esiste o non è dell'utente.
    /// </summary>
    public async Task<RispostaDomanda?> RiprovaAsync(long domandaId, int utenteId, int clienteId, string? commento, CancellationToken cancellationToken)
    {
        string? testo;
        List<RispostaSbagliata> sbagliate;
        await using (var connection = await database.ApriAsync(cancellationToken))
        {
            testo = await connection.ExecuteScalarAsync<string?>("""
                UPDATE dbo.Domande SET Valutazione = @Sbagliata, CommentoValutazione = @commento
                OUTPUT INSERTED.Testo
                WHERE Id = @domandaId AND UtenteId = @utenteId AND ClienteId = @clienteId
                """, new { Valutazioni.Sbagliata, commento = Tronca(commento?.Trim() is { Length: > 0 } c ? c : null, 1000), domandaId, utenteId, clienteId });
            if (testo is null)
                return null;

            // Risale la catena delle riprove (DomandaOrigineId) e prende le risposte giudicate sbagliate, dalla più vecchia
            sbagliate = [.. (await connection.QueryAsync<RispostaSbagliata>("""
                WITH Catena AS
                (
                    SELECT Id, DomandaOrigineId, SqlGenerato, CommentoValutazione, Valutazione, 0 AS Livello
                    FROM dbo.Domande WHERE Id = @domandaId
                    UNION ALL
                    SELECT d.Id, d.DomandaOrigineId, d.SqlGenerato, d.CommentoValutazione, d.Valutazione, c.Livello + 1
                    FROM dbo.Domande d JOIN Catena c ON d.Id = c.DomandaOrigineId
                    WHERE c.Livello < 20
                )
                SELECT TOP (@MaxRisposteSbagliate) SqlGenerato AS Sql, CommentoValutazione AS Commento
                FROM Catena WHERE Valutazione = @Sbagliata
                ORDER BY Livello
                """, new { domandaId, MaxRisposteSbagliate, Valutazioni.Sbagliata })).Reverse()];
        }

        return await EseguiAsync(utenteId, clienteId, testo, sbagliate, domandaId, cancellationToken);
    }

    /// <summary>
    /// Segna (o, con null, annulla) la risposta come corretta: da quel momento la coppia domanda/SQL è un esempio per l'AI.
    /// Si può confermare solo una risposta eseguita con successo.
    /// </summary>
    public async Task<bool> ValutaAsync(long domandaId, int utenteId, int clienteId, string? valutazione)
    {
        if (valutazione is not (null or Valutazioni.Corretta))
            throw new ArgumentException("Valutazione non ammessa.", nameof(valutazione));

        await using var connection = await database.ApriAsync();
        return await connection.ExecuteAsync("""
            UPDATE dbo.Domande SET Valutazione = @valutazione, CommentoValutazione = NULL
            WHERE Id = @domandaId AND UtenteId = @utenteId AND ClienteId = @clienteId
              AND (@valutazione IS NULL OR (Esito = 'Ok' AND SqlGenerato IS NOT NULL))
            """, new { valutazione, domandaId, utenteId, clienteId }) > 0;
    }

    private async Task<RispostaDomanda> EseguiAsync(int utenteId, int clienteId, string domanda,
        IReadOnlyList<RispostaSbagliata>? sbagliate, long? origineId, CancellationToken cancellationToken)
    {
        var cronometro = Stopwatch.StartNew();
        var configurazione = await configurazioni.GetAsync(clienteId, conChiave: true)
            ?? throw new InvalidOperationException("Motore AI non configurato per questo cliente.");
        var oggetti = await schema.ElencoAsync(clienteId, soloAbilitati: true);
        if (oggetti.Count == 0)
            throw new InvalidOperationException("Nessuna tabella abilitata per questo cliente: configurare la struttura in Amministrazione > Clienti.");

        EsitoDomanda esitoDomanda;
        using (logger.BeginScope("Cliente {ClienteId}", clienteId))
            esitoDomanda = await CicloDomanda.EseguiAsync(generatore, configurazione, oggetti, await EsempiAsync(clienteId, cancellationToken),
                domanda, sbagliate, (richiesta, ct) => agente.EseguiQueryAsync(clienteId, richiesta, ct), logger, cancellationToken);
        var (proposta, esito, errore, risultato) = esitoDomanda;

        var durata = (int)cronometro.ElapsedMilliseconds;
        await using var connection = await database.ApriAsync(cancellationToken);
        var id = await connection.ExecuteScalarAsync<long>("""
            INSERT INTO dbo.Domande (UtenteId, ClienteId, Testo, SqlGenerato, Spiegazione, Grafico, Esito, Errore, Righe, DurataMs, DomandaOrigineId,
                Chiarimento, DatiInviati)
            OUTPUT INSERTED.Id
            VALUES (@utenteId, @clienteId, @Testo, @Sql, @Spiegazione, @Grafico, @esito, @Errore, @Righe, @durata, @origineId,
                @Chiarimento, @DatiInviati)
            """, new
        {
            Chiarimento = Tronca(proposta?.Chiarimento, 2000),
            esitoDomanda.DatiInviati,
            origineId,
            utenteId,
            clienteId,
            Testo = Tronca(domanda, 2000),
            Sql = proposta?.Sql is { Length: > 0 } s ? s : null,
            Spiegazione = Tronca(proposta?.Spiegazione, 2000),
            Grafico = proposta?.Grafico is { } g ? JsonSerializer.Serialize(g) : null,
            esito,
            Errore = Tronca(errore, 2000),
            Righe = risultato?.Righe.Count,
            durata
        });

        return new RispostaDomanda(id, domanda, proposta?.Sql, proposta?.Spiegazione ?? string.Empty, proposta?.Grafico,
            risultato?.Colonne ?? [], risultato?.Righe ?? [], risultato?.Troncato ?? false, durata, errore, null,
            Tronca(proposta?.Chiarimento, 2000), esitoDomanda.DatiInviati);
    }

    /// <summary>
    /// Risposta dell'utente al chiarimento chiesto dall'AI per la domanda <paramref name="domandaId"/>: si richiama il ciclo con
    /// domanda originale, chiarimento e risposta. Null se la domanda non esiste o non è dell'utente.
    /// </summary>
    public async Task<RispostaDomanda?> RispondiChiarimentoAsync(long domandaId, int utenteId, int clienteId, string risposta,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.ApriAsync(cancellationToken);
        var d = await connection.QuerySingleOrDefaultAsync<(string Testo, string? Chiarimento, bool DatiInviati)>(
            "SELECT Testo, Chiarimento, DatiInviati FROM dbo.Domande WHERE Id = @domandaId AND UtenteId = @utenteId AND ClienteId = @clienteId",
            new { domandaId, utenteId, clienteId });
        if (d.Testo is null)
            return null;
        if (d.Chiarimento is null)
            throw new InvalidOperationException("Per questa domanda l'AI non ha chiesto chiarimenti.");

        return await EseguiAsync(utenteId, clienteId, PromptBuilder.DomandaConChiarimento(d.Testo, d.Chiarimento, d.DatiInviati, risposta),
            sbagliate: null, domandaId, cancellationToken);
    }

    public async Task<IEnumerable<VoceStorico>> StoricoAsync(int utenteId, int clienteId, int limite)
    {
        await using var connection = await database.ApriAsync();
        var voci = await connection.QueryAsync<VoceStorico>("""
            SELECT TOP (@limite) Id, Testo, SqlGenerato, Esito, Righe, DataOra, Valutazione
            FROM dbo.Domande WHERE UtenteId = @utenteId AND ClienteId = @clienteId
            ORDER BY DataOra DESC, Id DESC
            """, new { utenteId, clienteId, limite = Math.Clamp(limite, 1, 500) });
        return voci.Select(v => v with { DataOra = DateTime.SpecifyKind(v.DataOra, DateTimeKind.Utc) });
    }

    /// <summary>
    /// Riapre una domanda dello storico: rilegge testo, SQL, spiegazione e grafico salvati e riesegue la query tramite l'agente
    /// (l'AI non viene chiamata). Null se la domanda non è dell'utente/cliente.
    /// </summary>
    public async Task<RispostaDomanda?> ApriAsync(long domandaId, int utenteId, int clienteId, CancellationToken cancellationToken)
    {
        await using var connection = await database.ApriAsync(cancellationToken);
        var d = await connection.QuerySingleOrDefaultAsync<DomandaSalvata>("""
            SELECT Testo, SqlGenerato, Spiegazione, Grafico, Esito, Errore, Valutazione, Chiarimento, DatiInviati
            FROM dbo.Domande WHERE Id = @domandaId AND UtenteId = @utenteId AND ClienteId = @clienteId
            """, new { domandaId, utenteId, clienteId });
        if (d is null)
            return null;

        var grafico = d.Grafico is { Length: > 0 } json ? JsonSerializer.Deserialize<GraficoProposto>(json) : null;
        RispostaDomanda Risposta(RisultatoQuery? r, int durata, string? errore) =>
            new(domandaId, d.Testo, d.SqlGenerato, d.Spiegazione ?? string.Empty, grafico,
                r?.Colonne ?? [], r?.Righe ?? [], r?.Troncato ?? false, durata, errore, d.Valutazione, d.Chiarimento, d.DatiInviati);

        if (d.SqlGenerato is null || d.Esito != "Ok")
            return Risposta(null, 0, d.Errore);

        var configurazione = await configurazioni.GetAsync(clienteId)
            ?? throw new InvalidOperationException("Motore AI non configurato per questo cliente.");
        // Ri-validata sulla struttura attuale (anche ogni passo della catena)
        var catena = await CatenaSql.EseguiAsync(d.SqlGenerato, await schema.ConsentitiAsync(clienteId),
            configurazione.MaxRighe, configurazione.TimeoutSecondi, (r, ct) => agente.EseguiQueryAsync(clienteId, r, ct), cancellationToken);
        return catena.Risultato is { } risultato ? Risposta(risultato, risultato.DurataMs, null) : Risposta(null, 0, catena.Errore);
    }

    private sealed record DomandaSalvata(string Testo, string? SqlGenerato, string? Spiegazione, string? Grafico, string Esito, string? Errore,
        string? Valutazione, string? Chiarimento, bool DatiInviati);

    /// <summary>Riesegue (per l'export) la query di una domanda dell'utente, ri-validandola sulla struttura attuale.</summary>
    public async Task<(string Testo, RisultatoQuery Risultato)> RieseguiAsync(long domandaId, int utenteId, int clienteId, CancellationToken cancellationToken)
    {
        await using var connection = await database.ApriAsync(cancellationToken);
        var domanda = await connection.QuerySingleOrDefaultAsync<(string Testo, string? Sql)>(
            "SELECT Testo, SqlGenerato FROM dbo.Domande WHERE Id = @domandaId AND UtenteId = @utenteId AND ClienteId = @clienteId",
            new { domandaId, utenteId, clienteId });
        if (domanda.Sql is null)
            throw new InvalidOperationException("Domanda non trovata o senza query.");

        var configurazione = await configurazioni.GetAsync(clienteId)
            ?? throw new InvalidOperationException("Motore AI non configurato per questo cliente.");
        var catena = await CatenaSql.EseguiAsync(domanda.Sql, await schema.ConsentitiAsync(clienteId),
            configurazione.MaxRighe, configurazione.TimeoutSecondi, (r, ct) => agente.EseguiQueryAsync(clienteId, r, ct), cancellationToken);
        return catena.Risultato is { } risultato ? (domanda.Testo, risultato) : throw new InvalidOperationException(catena.Errore);
    }

    /// <summary>
    /// Domande confermate corrette per il cliente (di qualunque utente), dalla più recente: solo testo e SQL.
    /// Se ne leggono più del necessario perché alcune possono essere scartate da <see cref="PromptBuilder.FiltraEsempi"/>.
    /// </summary>
    private async Task<IEnumerable<EsempioValidato>> EsempiAsync(int clienteId, CancellationToken cancellationToken)
    {
        await using var connection = await database.ApriAsync(cancellationToken);
        return await connection.QueryAsync<EsempioValidato>(new CommandDefinition("""
            SELECT TOP (@quanti) Testo AS Domanda, SqlGenerato AS Sql
            FROM dbo.Domande
            WHERE ClienteId = @clienteId AND Valutazione = @Corretta AND SqlGenerato IS NOT NULL
            ORDER BY DataOra DESC, Id DESC
            """, new { clienteId, Valutazioni.Corretta, quanti = PromptBuilder.MaxEsempi * 3 }, cancellationToken: cancellationToken));
    }

    private static string? Tronca(string? testo, int max) => testo is null || testo.Length <= max ? testo : testo[..max];
}
