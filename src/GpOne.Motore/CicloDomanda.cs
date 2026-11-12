using GpOne.Core;
using Microsoft.Extensions.Logging;

namespace GpOne.Motore;

public static class Esiti
{
    public const string Ok = "Ok";
    public const string ErroreAi = "ErroreAi";
    public const string NonRisolvibile = "NonRisolvibile";
    public const string SqlNonValido = "SqlNonValido";
    public const string ErroreSql = "ErroreSql";
    /// <summary>L'AI ha chiesto un chiarimento all'utente (testo in <see cref="PropostaSql.Chiarimento"/>).</summary>
    public const string Chiarimento = "Chiarimento";
    /// <summary>Un passo intermedio della catena non ha trovato risultati: i successivi non sono stati eseguiti.</summary>
    public const string PassoVuoto = "PassoVuoto";
    /// <summary>Raggiunto il numero massimo di chiamate all'AI senza una risposta.</summary>
    public const string TroppiTentativi = "TroppiTentativi";
}

/// <summary>Come è andata una domanda: la proposta dell'AI, l'esito, l'eventuale errore e il risultato (solo se Ok).</summary>
public record EsitoDomanda(PropostaSql? Proposta, string Esito, string? Errore, RisultatoQuery? Risultato)
{
    /// <summary>True se all'AI sono stati mandati risultati anonimizzati per questa domanda.</summary>
    public bool DatiInviati { get; init; }
}

/// <summary>
/// Domanda → l'AI (struttura + domanda) propone una o più SELECT collegate da segnaposto → validazione sugli oggetti abilitati →
/// esecuzione in locale della catena. Gestisce:
/// <list type="number">
/// <item>query sbagliata: si rimanda all'AI il passo con i segnaposto (mai i valori) e l'errore ripulito;</item>
/// <item>passo intermedio senza righe: ci si ferma senza eseguire i successivi;</item>
/// <item>risultato richiesto dall'AI: solo anonimizzato, al massimo N righe più un riepilogo (se il cliente lo consente);</item>
/// <item>troppe chiamate: ci si ferma dopo <see cref="ConfigurazioneAi.MaxChiamateAi"/>;</item>
/// <item>chiarimento: la domanda dell'AI torna all'utente;</item>
/// <item>segnaposto mancanti o valori scritti a mano: passo rifiutato e rimandato all'AI;</item>
/// <item>risposta troncata (token/contesto): si riprova una volta con il solo essenziale.</item>
/// </list>
/// Ogni chiamata ricostruisce il prompt da zero: la conversazione non cresce. Usato sia dall'API (esecuzione tramite l'agente)
/// sia dal desktop (esecuzione diretta).
/// </summary>
public static class CicloDomanda
{
    public static async Task<EsitoDomanda> EseguiAsync(
        IGeneratoreSql generatore,
        ConfigurazioneAi configurazione,
        IReadOnlyList<OggettoConfigurato> abilitati,
        IEnumerable<EsempioValidato> esempi,
        string domanda,
        IReadOnlyList<RispostaSbagliata>? sbagliate,
        Func<RichiestaQuery, CancellationToken, Task<RisultatoQuery>> esegui,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var consentiti = Struttura.Consentiti(abilitati);
        var anonimizzati = configurazione.InvioDatiAnonimizzati;
        var maxRigheAnonimizzate = Math.Clamp(configurazione.MaxRigheAnonimizzate, ConfigurazioneAi.MinRigheAnonimizzate, ConfigurazioneAi.MaxMaxRigheAnonimizzate);
        var maxChiamate = Math.Clamp(configurazione.MaxChiamateAi, ConfigurazioneAi.MinChiamateAi, ConfigurazioneAi.MaxMaxChiamateAi);
        var istruzioni = PromptBuilder.Istruzioni(abilitati, configurazione.Istruzioni, PromptBuilder.FiltraEsempi(esempi, consentiti),
            anonimizzati ? maxRigheAnonimizzate : null);

        var anonimizzatore = new Anonimizzatore();
        // Valori dei passi già eseguiti e dei token: restano qui, non vanno mai all'AI
        var valori = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var risultatiAnonimi = new List<string>();
        PropostaSql? proposta = null;
        string? sqlRifiutato = null, errore = null;
        var esito = Esiti.ErroreAi;
        var ridotto = false;
        var datiInviati = false;

        EsitoDomanda Fine(string e, string? messaggio, RisultatoQuery? risultato) => new(
            proposta is null ? null : proposta with
            {
                Spiegazione = anonimizzatore.Ripristina(proposta.Spiegazione),
                Chiarimento = proposta.Chiarimento is null ? null : anonimizzatore.Ripristina(proposta.Chiarimento)
            },
            e, messaggio is null ? null : anonimizzatore.Ripristina(messaggio), risultato) { DatiInviati = datiInviati };

        for (var chiamata = 1; chiamata <= maxChiamate; chiamata++)
        {
            var testo = PromptBuilder.Domanda(domanda, sqlRifiutato,
                errore is null ? null : PromptBuilder.RipulisciErrore(errore, valori.Values), sbagliate,
                risultatiAnonimi.Count == 0 ? null : string.Join("\n", risultatiAnonimi), valori.Keys);
            datiInviati |= risultatiAnonimi.Count > 0;

            try
            {
                proposta = await generatore.GeneraAsync(configurazione, istruzioni, testo, cancellationToken);
            }
            catch (RispostaTroncataException ex) when (!ridotto)
            {
                // Caso 7: si riparte con l'essenziale (domanda + ultimo risultato anonimizzato)
                logger?.LogWarning(ex, "Risposta dell'AI troncata: nuovo tentativo con prompt ridotto.");
                ridotto = true;
                if (risultatiAnonimi.Count > 1)
                    risultatiAnonimi.RemoveRange(0, risultatiAnonimi.Count - 1);
                (sqlRifiutato, errore, esito) = (null, null, Esiti.ErroreAi);
                continue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogWarning(ex, "Errore del motore AI.");
                return Fine(Esiti.ErroreAi, $"Errore del motore AI: {ex.Message}", null);
            }

            switch (proposta.Stato)
            {
                case StatiRisposta.Chiarimento:
                    return Fine(Esiti.Chiarimento, null, null);
                case StatiRisposta.Impossibile:
                    return Fine(Esiti.NonRisolvibile, null, null);
                case StatiRisposta.ServeRisultato when !anonimizzati:
                    return Fine(Esiti.NonRisolvibile,
                        "Per rispondere l'AI dovrebbe vedere un risultato anonimizzato, ma l'invio di dati anonimizzati è disattivato nella configurazione.",
                        null);
            }

            var catena = await CatenaSql.EseguiAsync(proposta.Passi, consentiti, configurazione.MaxRighe, configurazione.TimeoutSecondi,
                esegui, valori, cancellationToken);
            foreach (var (nome, valore) in catena.Valori)
                valori[nome] = valore;

            if (catena.Esito is Esiti.SqlNonValido or Esiti.ErroreSql)
            {
                // Casi 1 e 6: all'AI il testo con i segnaposto, mai i valori sostituiti
                (esito, errore, sqlRifiutato) = (catena.Esito, catena.Errore, CatenaSql.ATesto(proposta.Passi));
                continue;
            }
            if (catena.Esito == Esiti.PassoVuoto)
                return Fine(Esiti.PassoVuoto, catena.Errore, null);

            if (proposta.Stato == StatiRisposta.ServeRisultato)
            {
                // Caso 3: solo anonimizzato, al massimo maxRigheAnonimizzate righe più un riepilogo
                risultatiAnonimi.Add(anonimizzatore.Descrivi(proposta.Passi[^1].Nome, catena.Risultato!, maxRigheAnonimizzate));
                foreach (var (token, valore) in anonimizzatore.Valori)
                    valori[token] = valore;
                (sqlRifiutato, errore, esito) = (null, null, Esiti.TroppiTentativi);
                continue;
            }

            return Fine(Esiti.Ok, null, catena.Risultato);
        }

        // Caso 4: troppe chiamate
        return Fine(esito, esito == Esiti.TroppiTentativi || errore is null
            ? $"Non sono riuscito a rispondere dopo {maxChiamate} chiamate all'AI: prova a dividere la domanda in domande più piccole."
            : errore, null);
    }
}
