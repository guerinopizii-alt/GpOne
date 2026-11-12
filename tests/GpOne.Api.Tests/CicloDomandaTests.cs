using GpOne.Core;
using GpOne.Motore;

namespace GpOne.Api.Tests;

/// <summary>Ciclo condiviso da API e desktop: all'AI non devono mai arrivare dati del cliente.</summary>
public class CicloDomandaTests
{
    private static readonly List<OggettoConfigurato> Struttura =
    [
        new()
        {
            SchemaNome = "dbo", Nome = "Fatture", Tipo = "TABELLA", Abilitato = true,
            Colonne =
            [
                new() { Nome = "Importo", TipoDati = "decimal(18,2)", Abilitata = true, Ordine = 1 },
                new() { Nome = "Cliente", TipoDati = "nvarchar(100)", Abilitata = false, Ordine = 2 }
            ]
        },
        new() { SchemaNome = "dbo", Nome = "Segreti", Tipo = "TABELLA", Abilitato = false, Colonne = [new() { Nome = "Valore", Abilitata = true }] }
    ];

    private sealed class GeneratoreFinto(params string[] sql) : IGeneratoreSql
    {
        private int chiamata;
        public List<(string Istruzioni, string Domanda)> Ricevuti { get; } = [];

        public Task<PropostaSql> GeneraAsync(ConfigurazioneAi configurazione, string istruzioni, string domanda, CancellationToken cancellationToken)
        {
            Ricevuti.Add((istruzioni, domanda));
            return Task.FromResult(new PropostaSql(sql[Math.Min(chiamata++, sql.Length - 1)], "spiegazione", null));
        }
    }

    [Fact]
    public void SoloAbilitatiTogliTabelleEColonneDisabilitate()
    {
        var abilitati = Motore.Struttura.SoloAbilitati(Struttura);

        var fatture = Assert.Single(abilitati);
        Assert.Equal(["Importo"], fatture.Colonne.Select(c => c.Nome));
        Assert.Equal(2, Struttura[0].Colonne.Count); // l'originale non cambia
    }

    [Fact]
    public async Task ErroreSqlArrivaAllAiRipulitoEIRisultatiMai()
    {
        var generatore = new GeneratoreFinto("SELECT Importo FROM dbo.Fatture", "SELECT SUM(Importo) AS Totale FROM dbo.Fatture");
        var esecuzioni = 0;
        Task<RisultatoQuery> Esegui(RichiestaQuery r, CancellationToken _) => Task.FromResult(++esecuzioni == 1
            ? new RisultatoQuery([], [], false, 1, "Conversion failed when converting the varchar value 'Mario Rossi' to data type int.")
            : new RisultatoQuery([new("Totale", "decimal")], [[123.45m]], false, 1));

        var esito = await CicloDomanda.EseguiAsync(generatore, new ConfigurazioneAi(), Motore.Struttura.SoloAbilitati(Struttura), [],
            "Totale fatturato", null, Esegui, null, CancellationToken.None);

        Assert.Equal(Esiti.Ok, esito.Esito);
        Assert.Equal(2, generatore.Ricevuti.Count);
        var tuttoIlPrompt = string.Join("\n", generatore.Ricevuti.SelectMany(r => new[] { r.Istruzioni, r.Domanda }));
        Assert.DoesNotContain("Mario Rossi", tuttoIlPrompt);
        Assert.DoesNotContain("123", tuttoIlPrompt);
        Assert.DoesNotContain("Segreti", tuttoIlPrompt);
        Assert.DoesNotContain("Cliente nvarchar", tuttoIlPrompt);
        Assert.Contains("'<valore>'", generatore.Ricevuti[1].Domanda);
    }

    [Fact]
    public async Task SqlSuTabellaDisabilitataNonVieneEseguita()
    {
        var generatore = new GeneratoreFinto("SELECT Valore FROM dbo.Segreti");
        var eseguita = false;

        var esito = await CicloDomanda.EseguiAsync(generatore, new ConfigurazioneAi(), Motore.Struttura.SoloAbilitati(Struttura), [],
            "Mostra i segreti", null, (_, _) => { eseguita = true; return Task.FromResult(new RisultatoQuery([], [], false, 0)); },
            null, CancellationToken.None);

        Assert.False(eseguita);
        Assert.Equal(Esiti.SqlNonValido, esito.Esito);
        Assert.Equal(new ConfigurazioneAi().MaxChiamateAi, generatore.Ricevuti.Count);
    }
}
