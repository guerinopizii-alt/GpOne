using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GpOne.Core;

namespace GpOne.Motore;

/// <summary>
/// Unica via con cui dati del cliente possono arrivare all'AI (solo se <see cref="ConfigurazioneAi.InvioDatiAnonimizzati"/>):
/// testi e identificativi sostituiti da token (<c>Valore_001</c>, stesso valore = stesso token), numeri e date in chiaro,
/// binari omessi, al massimo un numero fissato di righe più un riepilogo delle colonne numeriche.
/// La corrispondenza token → valore vive solo in memoria per la singola domanda e non va mai all'AI.
/// </summary>
public sealed partial class Anonimizzatore
{
    private readonly Dictionary<string, string> tokenPerValore = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> valorePerToken = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Token → valore vero, da usare per risolvere i segnaposto <c>{{Valore_001}}</c> in locale.</summary>
    public IReadOnlyDictionary<string, object?> Valori => valorePerToken;

    [GeneratedRegex(@"\bValore_\d+\b", RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();

    public enum Trattamento { Token, InChiaro, Omesso }

    /// <summary>Come trattare una colonna del risultato in base al tipo SQL.</summary>
    public static Trattamento Tratta(string? tipo)
    {
        var t = (tipo ?? string.Empty).ToLowerInvariant();
        if (t.Contains("binary") || t is "image" or "timestamp" or "rowversion")
            return Trattamento.Omesso;
        if (t.Contains("char") || t.Contains("text") || t is "uniqueidentifier" or "xml" or "sql_variant" or "" || t.Contains("hierarchyid") || t.Contains("geo"))
            return Trattamento.Token;
        return Trattamento.InChiaro;
    }

    public string Token(object valore)
    {
        var chiave = Convert.ToString(valore, CultureInfo.InvariantCulture) ?? string.Empty;
        if (tokenPerValore.TryGetValue(chiave, out var token))
            return token;
        token = $"Valore_{tokenPerValore.Count + 1:000}";
        tokenPerValore[chiave] = token;
        valorePerToken[token] = valore;
        return token;
    }

    /// <summary>Testo anonimizzato del risultato di un passo, da mandare all'AI.</summary>
    public string Descrivi(string passo, RisultatoQuery risultato, int maxRighe)
    {
        maxRighe = Math.Max(1, maxRighe);
        var trattamenti = risultato.Colonne.Select(c => Tratta(c.Tipo)).ToArray();
        var sb = new StringBuilder();
        sb.Append($"Passo {passo}: {risultato.Righe.Count} righe");
        if (risultato.Troncato)
            sb.Append(" (lettura interrotta al limite di righe: ce ne sono altre)");
        sb.AppendLine(".");
        if (risultato.Righe.Count == 0)
            return sb.ToString();

        sb.AppendLine("Colonne: " + string.Join(", ", risultato.Colonne.Select((c, i) =>
            $"{c.Nome} ({c.Tipo}{(trattamenti[i] == Trattamento.Token ? ", anonimizzata" : trattamenti[i] == Trattamento.Omesso ? ", omessa" : "")})")));

        if (risultato.Righe.Count > maxRighe)
        {
            var riepilogo = Riepilogo(risultato, trattamenti);
            if (riepilogo.Length > 0)
                sb.AppendLine("Riepilogo su tutte le righe lette: " + riepilogo);
            sb.AppendLine($"Prime {maxRighe} righe:");
        }

        foreach (var riga in risultato.Righe.Take(maxRighe))
            sb.AppendLine(string.Join(" | ", riga.Select((v, i) => Cella(CatenaSql.Normalizza(v), i < trattamenti.Length ? trattamenti[i] : Trattamento.Token))));
        return sb.ToString();
    }

    private string Cella(object? valore, Trattamento trattamento) => valore switch
    {
        null => "NULL",
        _ when trattamento == Trattamento.Omesso => "<omesso>",
        _ when trattamento == Trattamento.Token => Token(valore),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        // Valori in chiaro ma arrivati come testo (date normalizzate, ecc.)
        string s => s,
        _ => Token(valore)
    };

    private static string Riepilogo(RisultatoQuery risultato, Trattamento[] trattamenti)
    {
        var parti = new List<string>();
        for (var i = 0; i < risultato.Colonne.Count; i++)
        {
            if (trattamenti[i] != Trattamento.InChiaro)
                continue;
            var numeri = risultato.Righe.Select(r => CatenaSql.Normalizza(r[i])).Where(v => v is not (null or string or bool))
                .Select(v => Convert.ToDecimal(v, CultureInfo.InvariantCulture)).ToList();
            if (numeri.Count == 0)
                continue;
            parti.Add(string.Create(CultureInfo.InvariantCulture,
                $"{risultato.Colonne[i].Nome}: somma {numeri.Sum()}, min {numeri.Min()}, max {numeri.Max()}"));
        }
        return string.Join("; ", parti);
    }

    /// <summary>Rimette i valori veri al posto dei token (spiegazioni e chiarimenti mostrati all'utente).</summary>
    public string Ripristina(string testo) =>
        valorePerToken.Count == 0 ? testo : TokenRegex().Replace(testo, m =>
            valorePerToken.TryGetValue(m.Value, out var v) ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty : m.Value);
}
