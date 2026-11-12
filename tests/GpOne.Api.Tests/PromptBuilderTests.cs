using GpOne.Motore;
using GpOne.Core;

namespace GpOne.Api.Tests;

public class PromptBuilderTests
{
    [Fact]
    public void IstruzioniContengonoSoloLaStrutturaAbilitata()
    {
        var oggetti = new List<OggettoConfigurato>
        {
            new()
            {
                SchemaNome = "dbo", Nome = "Fatture", Tipo = "TABELLA", Abilitato = true, Descrizione = "Fatture di vendita",
                Colonne = [new() { Nome = "Importo", TipoDati = "decimal(18,2)", Abilitata = true, Descrizione = "Imponibile" }]
            }
        };

        var testo = PromptBuilder.Istruzioni(oggetti, "Gli importi sono in euro.");

        Assert.Contains("[dbo].[Fatture]", testo);
        Assert.Contains("Importo decimal(18,2): Imponibile", testo);
        Assert.Contains("Gli importi sono in euro.", testo);
    }

    private static readonly List<OggettoConsentito> Consentiti =
        [new("dbo", "Fatture", new HashSet<string>(["Importo", "Data"], StringComparer.OrdinalIgnoreCase))];

    [Fact]
    public void EsempiValidatiFinisconoNelleIstruzioni()
    {
        var testo = PromptBuilder.Istruzioni([], null, [new("Totale fatturato", "SELECT SUM(Importo) AS Totale FROM dbo.Fatture")]);

        Assert.Contains("Domanda: Totale fatturato", testo);
        Assert.Contains("SELECT SUM(Importo) AS Totale FROM dbo.Fatture", testo);
    }

    [Fact]
    public void EsempiNonPiuValidiSullaStrutturaVengonoScartati()
    {
        var esempi = PromptBuilder.FiltraEsempi(
        [
            new("Totale", "SELECT SUM(Importo) AS Totale FROM dbo.Fatture"),
            new("totale ", "SELECT SUM(Importo) AS T FROM dbo.Fatture"),          // duplicato (stessa domanda)
            new("Sconti", "SELECT SUM(Sconto) AS Sconti FROM dbo.Fatture"),       // colonna non abilitata
            new("Clienti", "SELECT Nome FROM dbo.Clienti"),                       // tabella non abilitata
        ], Consentiti);

        var esempio = Assert.Single(esempi);
        Assert.Equal("Totale", esempio.Domanda);
    }

    [Fact]
    public void EsempiLimitatiAlMassimo()
    {
        var candidati = Enumerable.Range(1, PromptBuilder.MaxEsempi + 5)
            .Select(i => new EsempioValidato($"Domanda {i}", "SELECT Importo FROM dbo.Fatture"));

        Assert.Equal(PromptBuilder.MaxEsempi, PromptBuilder.FiltraEsempi(candidati, Consentiti).Count);
    }

    [Fact]
    public void RispostaSbagliataMandaQueryECommentoAllAi()
    {
        var testo = PromptBuilder.Domanda("Fatturato 2025", null, null,
            [new("SELECT SUM(Importo) AS Totale FROM dbo.Fatture", "mancano le note di credito"), new(null, "si può fare con Fatture")]);

        Assert.Contains("giudicato sbagliate", testo);
        Assert.Contains("SELECT SUM(Importo) AS Totale FROM dbo.Fatture", testo);
        Assert.Contains("Commento dell'utente: mancano le note di credito", testo);
        Assert.Contains("nessuna query", testo);
        Assert.Contains("Commento dell'utente: si può fare con Fatture", testo);
    }

    [Fact]
    public void ErroreRipulitoDaiValoriPrimaDiAndareAllAi()
    {
        var pulito = PromptBuilder.RipulisciErrore("Conversion failed when converting the varchar value 'Mario Rossi' to data type int.");
        Assert.DoesNotContain("Mario Rossi", pulito);
        Assert.Contains("'<valore>'", pulito);
    }
}
