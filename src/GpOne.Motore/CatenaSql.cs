using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using GpOne.Core;

namespace GpOne.Motore;

/// <summary>Una SELECT della catena: <see cref="Nome"/> è il segnaposto con cui i passi successivi ne usano il valore.</summary>
public record PassoSql(string Nome, string Sql);

/// <summary>
/// Esito dell'esecuzione di una catena. <see cref="Valori"/>: valori dei segnaposto risolti in locale,
/// che non vanno MAI all'AI. <see cref="Passo"/>: il passo in errore o vuoto (testo con i segnaposto, senza valori).
/// </summary>
public record EsitoCatena(string Esito, string? Errore, PassoSql? Passo, RisultatoQuery? Risultato,
    IReadOnlyDictionary<string, object?> Valori);

/// <summary>
/// Query consecutive collegate da segnaposto <c>{{NOME}}</c>: il valore di un passo intermedio (prima colonna della
/// prima riga) viene inserito come letterale SQL tipizzato nei passi successivi, senza mai passare dall'AI.
/// Ogni passo, prima e dopo la sostituzione, passa da <see cref="SqlValidator"/> con gli oggetti abilitati.
/// Testo salvato (storico, esempi): una SELECT sola così com'è, più passi preceduti da <c>-- passo: NOME</c>.
/// </summary>
public static partial class CatenaSql
{
    public const int MaxPassi = 6;
    public const string NomeFinale = "RISULTATO";

    [GeneratedRegex(@"N?'\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}'|\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}")]
    private static partial Regex Segnaposto();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]{0,63}$")]
    private static partial Regex NomeValido();

    [GeneratedRegex(@"^[ \t]*--[ \t]*passo:[ \t]*(\S+)[ \t]*\r?$", RegexOptions.Multiline)]
    private static partial Regex Intestazione();

    /// <summary>Token dell'anonimizzatore scritto direttamente nella query invece che come segnaposto.</summary>
    [GeneratedRegex(@"(?<!\{\{\s*)\bValore_\d+\b")]
    private static partial Regex TokenNudo();

    public static string ATesto(IReadOnlyList<PassoSql> passi) => passi.Count switch
    {
        0 => string.Empty,
        1 when passi[0].Nome == NomeFinale => passi[0].Sql.Trim(),
        _ => string.Join("\n\n", passi.Select(p => $"-- passo: {p.Nome}\n{p.Sql.Trim()}"))
    };

    public static IReadOnlyList<PassoSql> DaTesto(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo))
            return [];
        var intestazioni = Intestazione().Matches(testo);
        if (intestazioni.Count == 0)
            return [new PassoSql(NomeFinale, testo.Trim())];

        var passi = new List<PassoSql>();
        for (var i = 0; i < intestazioni.Count; i++)
        {
            var inizio = intestazioni[i].Index + intestazioni[i].Length;
            var fine = i + 1 < intestazioni.Count ? intestazioni[i + 1].Index : testo.Length;
            passi.Add(new PassoSql(intestazioni[i].Groups[1].Value, testo[inizio..fine].Trim()));
        }
        return passi;
    }

    public static IEnumerable<string> Segnaposti(string sql) =>
        Segnaposto().Matches(sql).Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);

    /// <summary>
    /// Controlla la catena senza eseguirla: nomi, segnaposto definiti da un passo precedente (o già noti),
    /// nessun token scritto a mano, ogni passo valido sugli oggetti abilitati. Null se va bene.
    /// </summary>
    public static string? Verifica(IReadOnlyList<PassoSql> passi, IReadOnlyCollection<OggettoConsentito> consentiti,
        IEnumerable<string>? valoriNoti = null)
    {
        if (passi.Count == 0)
            return "Nessuna query.";
        if (passi.Count > MaxPassi)
            return $"Troppi passi: al massimo {MaxPassi}.";

        var noti = new HashSet<string>(valoriNoti ?? [], StringComparer.OrdinalIgnoreCase);
        var precedenti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var passo in passi)
        {
            if (!NomeValido().IsMatch(passo.Nome) || passo.Nome.StartsWith("Valore_", StringComparison.OrdinalIgnoreCase))
                return $"Nome del passo non valido: {passo.Nome}.";
            if (TokenNudo().Match(passo.Sql) is { Success: true } token)
                return $"Nel passo {passo.Nome} il valore {token.Value} è scritto direttamente: usare il segnaposto {{{{{token.Value}}}}}.";
            foreach (var nome in Segnaposti(passo.Sql))
                if (!precedenti.Contains(nome) && !noti.Contains(nome))
                    return $"Nel passo {passo.Nome} il segnaposto {{{{{nome}}}}} non è definito da un passo precedente.";

            var validazione = SqlValidator.Valida(Sostituisci(passo.Sql, _ => null), consentiti);
            if (!validazione.Valida)
                return passi.Count == 1 ? validazione.Errore : $"Passo {passo.Nome}: {validazione.Errore}";
            if (!precedenti.Add(passo.Nome))
                return $"Il nome del passo {passo.Nome} è ripetuto.";
        }
        return null;
    }

    /// <summary>Sostituisce i segnaposto (anche se l'AI li ha messi tra apici) con il letterale SQL del valore.</summary>
    public static string Sostituisci(string sql, Func<string, object?> valore) =>
        Segnaposto().Replace(sql, m => LiteraleSql(valore(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)));

    /// <summary>Letterale T-SQL per un valore letto dal database: testi con apici raddoppiati, numeri in cultura invariante.</summary>
    public static string LiteraleSql(object? valore) => Normalizza(valore) switch
    {
        null => "NULL",
        bool b => b ? "1" : "0",
        string s => Testo(s),
        sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToString(valore, CultureInfo.InvariantCulture)!,
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        IFormattable f => Testo(f.ToString(null, CultureInfo.InvariantCulture)),
        var altro => Testo(altro.ToString() ?? string.Empty)
    };

    private static string Testo(string s) => "N'" + s.Replace("'", "''") + "'";

    /// <summary>Valori arrivati dall'agente via SignalR sono <see cref="JsonElement"/>: li riporta a tipi semplici.</summary>
    public static object? Normalizza(object? valore) => valore switch
    {
        JsonElement e => e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.TryGetDecimal(out var d) ? d : e.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => e.GetRawText()
        },
        _ => valore
    };

    /// <summary>
    /// Esegue una catena salvata come testo (riapertura dallo storico, export), ri-validata sulla struttura attuale.
    /// Una catena nata da risultati anonimizzati usa token ({{Valore_001}}) non più risolvibili: va ripetuta la domanda.
    /// </summary>
    public static Task<EsitoCatena> EseguiAsync(string testo, IReadOnlyCollection<OggettoConsentito> consentiti, int maxRighe, int timeoutSecondi,
        Func<RichiestaQuery, CancellationToken, Task<RisultatoQuery>> esegui, CancellationToken cancellationToken)
    {
        var passi = DaTesto(testo);
        if (passi.SelectMany(p => Segnaposti(p.Sql)).Any(n => n.StartsWith("Valore_", StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult(new EsitoCatena(Esiti.SqlNonValido,
                "La risposta era stata costruita su risultati anonimizzati: per rivederla ripetere la domanda.", null, null,
                new Dictionary<string, object?>()));
        return EseguiAsync(passi, consentiti, maxRighe, timeoutSecondi, esegui, null, cancellationToken);
    }

    /// <summary>
    /// Esegue i passi in ordine. Un passo intermedio senza righe o con valore vuoto ferma la catena (<see cref="Esiti.PassoVuoto"/>):
    /// i passi successivi darebbero un risultato sbagliato (es. WHERE Cliente = '').
    /// </summary>
    public static async Task<EsitoCatena> EseguiAsync(IReadOnlyList<PassoSql> passi, IReadOnlyCollection<OggettoConsentito> consentiti,
        int maxRighe, int timeoutSecondi, Func<RichiestaQuery, CancellationToken, Task<RisultatoQuery>> esegui,
        IReadOnlyDictionary<string, object?>? noti, CancellationToken cancellationToken)
    {
        var valori = noti is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(noti, StringComparer.OrdinalIgnoreCase);
        if (Verifica(passi, consentiti, valori.Keys) is { } errore)
            return new EsitoCatena(Esiti.SqlNonValido, errore, null, null, valori);

        RisultatoQuery? risultato = null;
        var durata = 0;
        for (var i = 0; i < passi.Count; i++)
        {
            var passo = passi[i];
            var sql = Sostituisci(passo.Sql, nome => valori[nome]);
            var validazione = SqlValidator.Valida(sql, consentiti);
            if (!validazione.Valida)
                return new EsitoCatena(Esiti.SqlNonValido, validazione.Errore, passo, null, valori);

            risultato = await esegui(new RichiestaQuery(sql, maxRighe, timeoutSecondi), cancellationToken);
            durata += risultato.DurataMs;
            if (risultato.Errore is not null)
                return new EsitoCatena(Esiti.ErroreSql, risultato.Errore, passo, null, valori);

            var primo = risultato is { Righe.Count: > 0, Colonne.Count: > 0 } ? Normalizza(risultato.Righe[0][0]) : null;
            if (primo is not (null or ""))
                valori[passo.Nome] = primo;
            else if (i < passi.Count - 1)
                return new EsitoCatena(Esiti.PassoVuoto,
                    $"Il passo {passo.Nome} non ha trovato risultati: le query successive non sono state eseguite.", passo, null, valori);
        }
        return new EsitoCatena(Esiti.Ok, null, passi[^1], risultato! with { DurataMs = durata }, valori);
    }
}
