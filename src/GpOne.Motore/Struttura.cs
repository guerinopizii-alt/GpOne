using GpOne.Core;

namespace GpOne.Motore;

/// <summary>Tabella o vista del DB cliente come configurata in GpOne: abilitazione e descrizione per il prompt dell'AI.</summary>
public class OggettoConfigurato
{
    public int Id { get; set; }
    public string SchemaNome { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public bool Abilitato { get; set; }
    public string? Descrizione { get; set; }
    public List<ColonnaConfigurata> Colonne { get; set; } = [];
}

public class ColonnaConfigurata
{
    public int Id { get; set; }
    public int OggettoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string TipoDati { get; set; } = string.Empty;
    public int Ordine { get; set; }
    public bool Abilitata { get; set; }
    public string? Descrizione { get; set; }
}

public static class Struttura
{
    /// <summary>
    /// Solo oggetti e colonne abilitati (quello che può vedere l'AI), a partire dalla struttura completa.
    /// Gli oggetti restituiti sono copie: modificarli non tocca la configurazione.
    /// </summary>
    public static List<OggettoConfigurato> SoloAbilitati(IEnumerable<OggettoConfigurato> oggetti) =>
        [.. oggetti.Where(o => o.Abilitato).Select(o => new OggettoConfigurato
        {
            Id = o.Id, SchemaNome = o.SchemaNome, Nome = o.Nome, Tipo = o.Tipo, Abilitato = true, Descrizione = o.Descrizione,
            Colonne = [.. o.Colonne.Where(c => c.Abilitata).OrderBy(c => c.Ordine)]
        })];

    /// <summary>Oggetti e colonne (già filtrati sugli abilitati) per il validatore SQL.</summary>
    public static List<OggettoConsentito> Consentiti(IEnumerable<OggettoConfigurato> abilitati) =>
        [.. abilitati.Select(o => new OggettoConsentito(o.SchemaNome, o.Nome, o.Colonne.Select(c => c.Nome).ToHashSet(StringComparer.OrdinalIgnoreCase)))];
}
