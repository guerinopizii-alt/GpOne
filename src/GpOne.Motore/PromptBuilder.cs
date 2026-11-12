using System.Text;
using System.Text.RegularExpressions;
using GpOne.Core;

namespace GpOne.Motore;

/// <summary>Domanda con la SELECT che l'utente ha confermato corretta: esempio per l'AI.</summary>
public record EsempioValidato(string Domanda, string Sql);

/// <summary>Risposta giudicata sbagliata dall'utente: la query proposta e il suo commento (mai i risultati).</summary>
public record RispostaSbagliata(string? Sql, string? Commento);

/// <summary>
/// Istruzioni per l'AI: regole + struttura delle sole tabelle/colonne abilitate, con le descrizioni dell'amministratore,
/// ed esempi domanda/SQL confermati dagli utenti.
/// Non contengono MAI dati del cliente in chiaro: gli unici dati ammessi sono i risultati prodotti da <see cref="Anonimizzatore"/>,
/// e solo se il cliente ha attivato <see cref="ConfigurazioneAi.InvioDatiAnonimizzati"/>. I valori sostituiti ai segnaposto non escono mai.
/// </summary>
public static partial class PromptBuilder
{
    /// <summary>Numero massimo di esempi validati inseriti nelle istruzioni.</summary>
    public const int MaxEsempi = 20;

    /// <param name="maxRigheAnonimizzate">Se non null l'AI può chiedere risultati anonimizzati (stato serveRisultato).</param>
    public static string Istruzioni(IReadOnlyList<OggettoConfigurato> oggetti, string? istruzioniCliente,
        IReadOnlyList<EsempioValidato>? esempi = null, int? maxRigheAnonimizzate = null)
    {
        var sb = new StringBuilder("""
            Sei un esperto di SQL Server. Trasformi domande in italiano in query SELECT T-SQL.
            Non vedrai mai i dati in chiaro: conosci solo la struttura qui sotto. Le query verranno eseguite dall'applicazione
            e il risultato mostrato direttamente all'utente.

            Regole per ogni query:
            - Una sola istruzione SELECT (CTE ammesse). Niente INSERT/UPDATE/DELETE, DDL, EXEC, variabili, tabelle temporanee, SELECT INTO.
            - Usa SOLO le tabelle/viste e le colonne elencate, con il nome schema.tabella. Niente SELECT *: elenca le colonne.
            - Dai alias chiari in italiano alle colonne calcolate (es. AS Totale, AS Mese). Usa ORDER BY quando ha senso.
            - Solo funzioni predefinite di SQL Server (niente funzioni utente).
            - Per il grafico usa gli alias delle colonne dell'ultima query; tipo "nessuno" se un grafico non aiuta.

            Stato della risposta:
            - "fatto": in passi metti le query da eseguire in ordine. Di solito basta UN passo: usa CTE e sottoquery quando puoi.
              Solo se una query dipende da un valore trovato da una precedente usa più passi (al massimo 6): ogni passo ha un nome
              in MAIUSCOLO (es. CLIENTE_TOP) e i passi successivi ne usano il valore con il segnaposto {{CLIENTE_TOP}}, senza apici
              (l'applicazione inserisce il valore già nel formato SQL giusto). Il valore di un passo intermedio è la prima colonna
              della prima riga: fagli restituire una sola colonna con TOP (1). All'utente va il risultato dell'ultimo passo.
              Non scrivere mai valori inventati o presunti: usa sempre i segnaposto.
            - "chiarimento": se la domanda è ambigua e nessuna interpretazione è ragionevole, scrivi in chiarimento una domanda breve
              per l'utente (es. "Per quale anno?") e lascia passi vuoto. Non chiedere chiarimenti se puoi scegliere un'interpretazione sensata.
            - "impossibile": se la domanda non si può soddisfare con questa struttura; passi vuoto, spiega il perché.

            """);

        if (maxRigheAnonimizzate is { } maxRighe)
            sb.AppendLine($$$"""
                - "serveRisultato": SOLO se per scrivere le query successive devi davvero vedere i valori di un risultato (un segnaposto non basta).
                  Metti in passi le query fino a quella di cui ti serve il risultato (l'ultima). Ti verrà rimandato ANONIMIZZATO:
                  testi e codici sostituiti da token (es. Valore_001), numeri e date in chiaro, al massimo {{{maxRighe}}} righe più un riepilogo.
                  Per usare un token in una query scrivi il segnaposto {{Valore_001}}; i nomi dei passi già eseguiti restano utilizzabili come segnaposto.

                """);
        else
            sb.AppendLine("Non puoi vedere risultati: lo stato serveRisultato non è disponibile.").AppendLine();

        if (!string.IsNullOrWhiteSpace(istruzioniCliente))
            sb.AppendLine("Indicazioni sul database di questo cliente:").AppendLine(istruzioniCliente.Trim()).AppendLine();

        sb.AppendLine("Struttura del database:");
        foreach (var o in oggetti)
        {
            sb.Append($"\n[{o.SchemaNome}].[{o.Nome}] ({o.Tipo.ToLowerInvariant()})");
            if (!string.IsNullOrWhiteSpace(o.Descrizione))
                sb.Append($" - {o.Descrizione.Trim()}");
            sb.AppendLine();
            foreach (var c in o.Colonne)
            {
                sb.Append($"  - {c.Nome} {c.TipoDati}");
                if (!string.IsNullOrWhiteSpace(c.Descrizione))
                    sb.Append($": {c.Descrizione.Trim()}");
                sb.AppendLine();
            }
        }

        if (esempi is { Count: > 0 })
        {
            sb.AppendLine().AppendLine("Esempi di domande con la query che l'utente ha confermato corretta (riferimento per tabelle, join e filtri):");
            foreach (var e in esempi)
                sb.AppendLine().AppendLine($"Domanda: {e.Domanda.Trim()}").AppendLine("SQL:").AppendLine(e.Sql.Trim());
        }
        return sb.ToString();
    }

    /// <summary>
    /// Tra le domande confermate corrette tiene, senza duplicati e fino a <see cref="MaxEsempi"/>, quelle la cui SQL
    /// è ancora valida sulla struttura abilitata attuale (una colonna disabilitata non deve rientrare tramite un esempio).
    /// </summary>
    public static IReadOnlyList<EsempioValidato> FiltraEsempi(IEnumerable<EsempioValidato> candidati,
        IReadOnlyCollection<OggettoConsentito> consentiti) =>
        [.. candidati
            .Where(e => !string.IsNullOrWhiteSpace(e.Domanda) && !string.IsNullOrWhiteSpace(e.Sql))
            .DistinctBy(e => e.Domanda.Trim().ToLowerInvariant())
            .Where(e => CatenaSql.Verifica(CatenaSql.DaTesto(e.Sql), consentiti) is null)
            .Take(MaxEsempi)];

    /// <summary>
    /// Nuova domanda dopo un chiarimento: domanda originale + chiarimento chiesto dall'AI + risposta dell'utente.
    /// Se la domanda aveva mandato risultati anonimizzati (<paramref name="datiInviati"/>), il chiarimento mostrato all'utente
    /// contiene i valori veri al posto dei token: non torna all'AI, si manda solo la risposta dell'utente.
    /// </summary>
    public static string DomandaConChiarimento(string domanda, string chiarimento, bool datiInviati, string risposta) =>
        datiInviati
            ? $"{domanda.Trim()}\n(Precisazione dell'utente: {Tronca(risposta.Trim(), 1000)})"
            : $"{domanda.Trim()}\n(Chiarimento chiesto: {Tronca(chiarimento.Trim(), 500)} Risposta dell'utente: {Tronca(risposta.Trim(), 1000)})";

    private static string Tronca(string s, int max) => s.Length <= max ? s : s[..max];

    /// <summary>
    /// Domanda dell'utente, con le risposte che l'utente ha giudicato sbagliate (query + suo commento, nessun risultato)
    /// e l'eventuale errore del tentativo precedente (già ripulito dai valori).
    /// </summary>
    /// <param name="risultatiAnonimizzati">Testo prodotto da <see cref="Anonimizzatore.Descrivi"/> (mai valori in chiaro).</param>
    /// <param name="segnapostoDisponibili">Nomi (non valori) dei passi già eseguiti, utilizzabili come segnaposto.</param>
    public static string Domanda(string domanda, string? sqlPrecedente, string? errorePrecedente,
        IReadOnlyList<RispostaSbagliata>? sbagliate = null, string? risultatiAnonimizzati = null,
        IEnumerable<string>? segnapostoDisponibili = null)
    {
        var testo = $"Data di oggi: {DateTime.Today:yyyy-MM-dd}\n\nDomanda: {domanda.Trim()}";
        if (sbagliate is { Count: > 0 })
        {
            testo += "\n\nL'utente ha giudicato sbagliate queste risposte alla stessa domanda: proponi una query diversa che tenga conto dei suoi commenti.";
            foreach (var s in sbagliate)
            {
                testo += string.IsNullOrWhiteSpace(s.Sql)
                    ? "\n\nRisposta sbagliata: nessuna query (la domanda era stata giudicata non risolvibile)."
                    : $"\n\nQuery sbagliata:\n{s.Sql.Trim()}";
                if (!string.IsNullOrWhiteSpace(s.Commento))
                    testo += $"\nCommento dell'utente: {s.Commento.Trim()}";
            }
        }
        if (!string.IsNullOrWhiteSpace(risultatiAnonimizzati))
        {
            testo += "\n\nRisultati che hai chiesto, ANONIMIZZATI (testi sostituiti da token Valore_NNN, numeri e date in chiaro):\n"
                     + risultatiAnonimizzati.Trim();
            var nomi = segnapostoDisponibili?.Where(n => !n.StartsWith("Valore_", StringComparison.OrdinalIgnoreCase)).ToList();
            if (nomi is { Count: > 0 })
                testo += "\n\nPassi già eseguiti, utilizzabili come segnaposto: " + string.Join(", ", nomi.Select(n => "{{" + n + "}}")) + ".";
            testo += "\nOra prosegui: rispondi con i passi per la domanda.";
        }
        if (sqlPrecedente is not null)
            testo += $"\n\nLa query proposta in precedenza non è stata accettata:\n{sqlPrecedente}\n\nMotivo: {errorePrecedente}\nCorreggila.";
        return testo;
    }

    /// <summary>
    /// I messaggi d'errore di SQL Server possono contenere valori dei dati (es. "Conversion failed when converting
    /// the varchar value 'ABC'..."): prima di mandarli all'AI si sostituiscono stringhe e numeri tra apici.
    /// </summary>
    public static string RipulisciErrore(string errore) =>
        ValoreTraApici().Replace(errore, "'<valore>'");

    /// <summary>Come <see cref="RipulisciErrore(string)"/>, togliendo anche i valori sostituiti ai segnaposto (anche senza apici).</summary>
    public static string RipulisciErrore(string errore, IEnumerable<object?> valori)
    {
        var pulito = RipulisciErrore(errore);
        foreach (var testo in valori.Select(v => Convert.ToString(CatenaSql.Normalizza(v), System.Globalization.CultureInfo.InvariantCulture))
                     .Where(t => t is { Length: >= 2 }).OrderByDescending(t => t!.Length))
            pulito = pulito.Replace(testo!, "<valore>", StringComparison.OrdinalIgnoreCase);
        return pulito;
    }

    [GeneratedRegex("'[^']*'")]
    private static partial Regex ValoreTraApici();
}
