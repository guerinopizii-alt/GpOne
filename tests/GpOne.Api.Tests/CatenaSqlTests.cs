using System.Globalization;
using System.Text.Json;
using GpOne.Core;
using GpOne.Motore;

namespace GpOne.Api.Tests;

/// <summary>
/// Catena di query con segnaposto, fallback anonimizzato e i 7 casi del ciclo:
/// all'AI non arrivano mai valori in chiaro, solo token dell'anonimizzatore.
/// </summary>
public class CatenaSqlTests
{
    private static readonly List<OggettoConfigurato> Struttura =
    [
        new()
        {
            SchemaNome = "dbo", Nome = "Vendite", Tipo = "TABELLA", Abilitato = true,
            Colonne =
            [
                new() { Nome = "Cliente", TipoDati = "nvarchar(100)", Abilitata = true, Ordine = 1 },
                new() { Nome = "Prodotto", TipoDati = "nvarchar(100)", Abilitata = true, Ordine = 2 },
                new() { Nome = "Importo", TipoDati = "decimal(18,2)", Abilitata = true, Ordine = 3 },
                new() { Nome = "Data", TipoDati = "date", Abilitata = true, Ordine = 4 }
            ]
        }
    ];

    private static readonly List<OggettoConsentito> Consentiti = Motore.Struttura.Consentiti(Struttura);

    private const string ClienteTop = "SELECT TOP (1) Cliente FROM dbo.Vendite GROUP BY Cliente ORDER BY SUM(Importo) DESC";
    private const string ProdottoTop = "SELECT TOP (1) Prodotto FROM dbo.Vendite WHERE Cliente = {{CLIENTE_TOP}} GROUP BY Prodotto ORDER BY SUM(Importo) DESC";
    private const string Mesi = "SELECT MONTH(Data) AS Mese, SUM(Importo) AS Totale FROM dbo.Vendite WHERE Cliente = {{CLIENTE_TOP}} AND Prodotto = {{PRODOTTO_TOP}} GROUP BY MONTH(Data)";

    private static PropostaSql Fatto(params PassoSql[] passi) => new(StatiRisposta.Fatto, passi, null, "spiegazione", null);

    private sealed class GeneratoreFinto(params PropostaSql[] risposte) : IGeneratoreSql
    {
        private int chiamata;
        public List<string> Prompt { get; } = [];
        public Func<int, Exception?>? Eccezione { get; init; }

        public Task<PropostaSql> GeneraAsync(ConfigurazioneAi configurazione, string istruzioni, string domanda, CancellationToken cancellationToken)
        {
            Prompt.Add(istruzioni + "\n" + domanda);
            var n = chiamata++;
            if (Eccezione?.Invoke(n) is { } ex)
                throw ex;
            return Task.FromResult(risposte[Math.Min(n, risposte.Length - 1)]);
        }
    }

    /// <summary>Esecutore finto: risponde in base al testo della query e registra le query ricevute.</summary>
    private sealed class DbFinto
    {
        public List<string> Eseguite { get; } = [];
        public Func<string, RisultatoQuery> Risposta { get; init; } = _ => new RisultatoQuery([new("X", "int")], [[1]], false, 1);

        public Task<RisultatoQuery> Esegui(RichiestaQuery r, CancellationToken _)
        {
            Eseguite.Add(r.Sql);
            return Task.FromResult(Risposta(r.Sql));
        }
    }

    private static RisultatoQuery Testo(string colonna, params string[] valori) =>
        new([new(colonna, "nvarchar")], [.. valori.Select(v => new object?[] { v })], false, 1);

    private static readonly RisultatoQuery Vuoto = new([new("Cliente", "nvarchar")], [], false, 1);

    private static Task<EsitoDomanda> Chiedi(GeneratoreFinto generatore, DbFinto db, ConfigurazioneAi? configurazione = null) =>
        CicloDomanda.EseguiAsync(generatore, configurazione ?? new ConfigurazioneAi(), Struttura, [],
            "Chi è il cliente top, il suo prodotto top e quanto ha speso mese per mese?", null, db.Esegui, null, CancellationToken.None);

    private static RisultatoQuery RispostaVendite(string sql) =>
        sql.Contains("GROUP BY Cliente") ? Testo("Cliente", "Mario O'Rossi")
        : sql.Contains("GROUP BY Prodotto") ? Testo("Prodotto", "iPhone")
        : new RisultatoQuery([new("Mese", "int"), new("Totale", "decimal")], [[1, 200m], [2, 300m]], false, 1);

    [Fact]
    public async Task CatenaConSegnapostoEseguitaInLocaleSenzaValoriAllAi()
    {
        var generatore = new GeneratoreFinto(Fatto(new PassoSql("CLIENTE_TOP", ClienteTop), new PassoSql("PRODOTTO_TOP", ProdottoTop), new PassoSql("RISULTATO", Mesi)));
        var db = new DbFinto { Risposta = RispostaVendite };

        var esito = await Chiedi(generatore, db);

        Assert.Equal(Esiti.Ok, esito.Esito);
        Assert.Equal(2, esito.Risultato!.Righe.Count);
        Assert.Single(generatore.Prompt);
        Assert.False(esito.DatiInviati);
        Assert.Contains("Cliente = N'Mario O''Rossi'", db.Eseguite[1]);
        Assert.Contains("Prodotto = N'iPhone'", db.Eseguite[2]);
        Assert.Contains("-- passo: CLIENTE_TOP", esito.Proposta!.Sql);
        Assert.Contains("{{CLIENTE_TOP}}", esito.Proposta.Sql);
    }

    [Fact]
    public async Task PassoIntermedioVuotoFermaLaCatena()
    {
        var generatore = new GeneratoreFinto(Fatto(new PassoSql("CLIENTE_TOP", ClienteTop), new PassoSql("RISULTATO", Mesi.Replace(" AND Prodotto = {{PRODOTTO_TOP}}", ""))));
        var db = new DbFinto { Risposta = _ => Vuoto };

        var esito = await Chiedi(generatore, db);

        Assert.Equal(Esiti.PassoVuoto, esito.Esito);
        Assert.Single(db.Eseguite);
        Assert.Single(generatore.Prompt);
        Assert.Contains("CLIENTE_TOP", esito.Errore);
    }

    [Fact]
    public async Task ErroreSqlTornaAllAiConISegnapostoEMaiConIValori()
    {
        var sbagliato = Fatto(new PassoSql("CLIENTE_TOP", ClienteTop), new PassoSql("RISULTATO", Mesi.Replace(" AND Prodotto = {{PRODOTTO_TOP}}", "")));
        var generatore = new GeneratoreFinto(sbagliato, sbagliato);
        var esecuzioni = 0;
        var db = new DbFinto
        {
            Risposta = sql => sql.Contains("GROUP BY Cliente") ? Testo("Cliente", "Mario Rossi")
                : ++esecuzioni == 1 ? new RisultatoQuery([], [], false, 1, "Invalid value Mario Rossi near 'Mario Rossi'.")
                : new RisultatoQuery([new("Mese", "int")], [[1]], false, 1)
        };

        var esito = await Chiedi(generatore, db);

        Assert.Equal(Esiti.Ok, esito.Esito);
        Assert.Equal(2, generatore.Prompt.Count);
        Assert.Contains("Cliente = {{CLIENTE_TOP}}", generatore.Prompt[1]);
        Assert.DoesNotContain("Mario", string.Join("\n", generatore.Prompt));
    }

    [Fact]
    public async Task SegnapostoNonDefinitoOTokenScrittoAManoVengonoRifiutati()
    {
        Assert.Contains("non è definito", CatenaSql.Verifica([new("RISULTATO", ProdottoTop)], Consentiti));
        Assert.Contains("{{Valore_001}}", CatenaSql.Verifica([new("RISULTATO", "SELECT Importo FROM dbo.Vendite WHERE Cliente = 'Valore_001'")], Consentiti));
        Assert.Null(CatenaSql.Verifica([new("RISULTATO", "SELECT Importo FROM dbo.Vendite WHERE Cliente = {{Valore_001}}")], Consentiti, ["Valore_001"]));

        var generatore = new GeneratoreFinto(Fatto(new PassoSql("RISULTATO", ProdottoTop)));
        var db = new DbFinto();
        var esito = await Chiedi(generatore, db);

        Assert.Equal(Esiti.SqlNonValido, esito.Esito);
        Assert.Empty(db.Eseguite);
    }

    [Fact]
    public async Task ServeRisultatoConInvioDisattivatoNonMandaNulla()
    {
        var generatore = new GeneratoreFinto(new PropostaSql(StatiRisposta.ServeRisultato, [new("CLIENTI", ClienteTop)], null, "", null));
        var db = new DbFinto { Risposta = RispostaVendite };

        var esito = await Chiedi(generatore, db, new ConfigurazioneAi { InvioDatiAnonimizzati = false });

        Assert.Equal(Esiti.NonRisolvibile, esito.Esito);
        Assert.Empty(db.Eseguite);
        Assert.Single(generatore.Prompt);
        Assert.Contains("serveRisultato non è disponibile", generatore.Prompt[0]);
        Assert.False(esito.DatiInviati);
    }

    [Fact]
    public async Task ServeRisultatoMandaSoloTokenERipristinaIValoriInLocale()
    {
        var generatore = new GeneratoreFinto(
            new PropostaSql(StatiRisposta.ServeRisultato, [new("CLIENTI", "SELECT Cliente, SUM(Importo) AS Totale FROM dbo.Vendite GROUP BY Cliente")], null, "", null),
            new PropostaSql(StatiRisposta.Fatto, [new("RISULTATO", "SELECT Importo FROM dbo.Vendite WHERE Cliente = {{Valore_002}}")], null,
                "Vendite di Valore_002", null));
        var db = new DbFinto
        {
            Risposta = sql => sql.Contains("GROUP BY Cliente")
                ? new RisultatoQuery([new("Cliente", "nvarchar"), new("Totale", "decimal")],
                    [["Mario Rossi", 500m], [JsonSerializer.SerializeToElement("Luigi Verdi"), JsonSerializer.SerializeToElement(300.5m)]], false, 1)
                : new RisultatoQuery([new("Importo", "decimal")], [[300.5m]], false, 1)
        };

        var esito = await Chiedi(generatore, db);

        Assert.Equal(Esiti.Ok, esito.Esito);
        Assert.True(esito.DatiInviati);
        var prompt = string.Join("\n", generatore.Prompt);
        Assert.DoesNotContain("Mario", prompt);
        Assert.DoesNotContain("Luigi", prompt);
        Assert.Contains("Valore_001 | 500", generatore.Prompt[1]);
        Assert.Contains("Valore_002 | 300.5", generatore.Prompt[1]);
        Assert.Contains("Cliente = N'Luigi Verdi'", db.Eseguite[^1]);
        Assert.Equal("Vendite di Luigi Verdi", esito.Proposta!.Spiegazione);
    }

    [Fact]
    public void TroppeRigheMandaPrimeRigheERiepilogo()
    {
        var righe = Enumerable.Range(1, 1000).Select(i => new object?[] { $"Cliente {i}", (decimal)i }).ToList();
        var testo = new Anonimizzatore().Descrivi("CLIENTI", new RisultatoQuery([new("Cliente", "nvarchar"), new("Totale", "decimal")], righe, false, 1), 10);

        Assert.Contains("1000 righe", testo);
        Assert.Contains("Totale: somma 500500, min 1, max 1000", testo);
        Assert.Contains("Valore_010 | 10", testo);
        Assert.DoesNotContain("Valore_011", testo);
        Assert.DoesNotContain("Cliente 1", testo);
    }

    [Fact]
    public async Task ServeRisultatoInLoopSiFermaAlMassimoDiChiamate()
    {
        var generatore = new GeneratoreFinto(new PropostaSql(StatiRisposta.ServeRisultato, [new("CLIENTI", ClienteTop)], null, "", null));
        var db = new DbFinto { Risposta = RispostaVendite };

        var esito = await Chiedi(generatore, db, new ConfigurazioneAi { MaxChiamateAi = 3 });

        Assert.Equal(Esiti.TroppiTentativi, esito.Esito);
        Assert.Equal(3, generatore.Prompt.Count);
        Assert.Contains("domande più piccole", esito.Errore);
    }

    [Fact]
    public async Task ChiarimentoTornaAllUtente()
    {
        var generatore = new GeneratoreFinto(new PropostaSql(StatiRisposta.Chiarimento, [], "Per quale anno? 2024 o 2025?", "", null));
        var db = new DbFinto();

        var esito = await Chiedi(generatore, db);

        Assert.Equal(Esiti.Chiarimento, esito.Esito);
        Assert.Equal("Per quale anno? 2024 o 2025?", esito.Proposta!.Chiarimento);
        Assert.Empty(db.Eseguite);
    }

    [Fact]
    public async Task RispostaTroncataSiRiprovaUnaVolta()
    {
        var generatore = new GeneratoreFinto(Fatto(new PassoSql("RISULTATO", "SELECT SUM(Importo) AS Totale FROM dbo.Vendite")))
        {
            Eccezione = n => n == 0 ? new RispostaTroncataException("limite") : null
        };

        var esito = await Chiedi(generatore, new DbFinto());

        Assert.Equal(Esiti.Ok, esito.Esito);
        Assert.Equal(2, generatore.Prompt.Count);

        var sempreTroncata = new GeneratoreFinto(Fatto()) { Eccezione = _ => new RispostaTroncataException("limite") };
        Assert.Equal(Esiti.ErroreAi, (await Chiedi(sempreTroncata, new DbFinto())).Esito);
        Assert.Equal(2, sempreTroncata.Prompt.Count);
    }

    [Fact]
    public void LiteraliSqlIndipendentiDallaCultura()
    {
        var cultura = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("it-IT");
            Assert.Equal("1234.5", CatenaSql.LiteraleSql(1234.5m));
            Assert.Equal("N'l''uno'", CatenaSql.LiteraleSql("l'uno"));
            Assert.Equal("N'àèì €'", CatenaSql.LiteraleSql("àèì €"));
            Assert.Equal("NULL", CatenaSql.LiteraleSql(null));
            Assert.Equal("1", CatenaSql.LiteraleSql(true));
            Assert.Equal("42", CatenaSql.LiteraleSql(JsonSerializer.SerializeToElement(42)));
            Assert.Equal("N'2026-01-15'", CatenaSql.LiteraleSql("2026-01-15"));
            // Segnaposto messo tra apici dall'AI: niente doppi apici
            Assert.Equal("WHERE Cliente = N'A'", CatenaSql.Sostituisci("WHERE Cliente = '{{X}}'", _ => "A"));
        }
        finally
        {
            CultureInfo.CurrentCulture = cultura;
        }
    }

    [Fact]
    public void TestoCatenaAndataERitorno()
    {
        IReadOnlyList<PassoSql> passi = [new("CLIENTE_TOP", ClienteTop), new("RISULTATO", Mesi.Replace(" AND Prodotto = {{PRODOTTO_TOP}}", ""))];
        Assert.Equal(passi, CatenaSql.DaTesto(CatenaSql.ATesto(passi)));
        Assert.Equal(ClienteTop, CatenaSql.ATesto([new("RISULTATO", ClienteTop)]));
        Assert.Equal([new PassoSql("RISULTATO", ClienteTop)], CatenaSql.DaTesto(ClienteTop));
    }

    [Fact]
    public void InterpretaNuovoEVecchioFormato()
    {
        var vecchia = GeneratoreSql.Interpreta("""{"sql":"SELECT 1 AS Uno","spiegazione":"x","grafico":{"tipo":"nessuno","x":"","y":[]}}""");
        Assert.Equal(StatiRisposta.Fatto, vecchia.Stato);
        Assert.Equal("SELECT 1 AS Uno", vecchia.Sql);

        var nuova = GeneratoreSql.Interpreta("""
            {"stato":"fatto","passi":[{"nome":"A","sql":"SELECT 1 AS X"},{"nome":"B","sql":"SELECT {{A}} AS Y"}],
             "chiarimento":"","spiegazione":"x","grafico":{"tipo":"nessuno","x":"","y":[]}}
            """);
        Assert.Equal(2, nuova.Passi.Count);

        var chiarimento = GeneratoreSql.Interpreta("""{"stato":"chiarimento","passi":[],"chiarimento":"Quale anno?","spiegazione":"","grafico":{"tipo":"nessuno","x":"","y":[]}}""");
        Assert.Equal("Quale anno?", chiarimento.Chiarimento);
    }

    [Fact]
    public void ErroreRipulitoAncheDaiValoriDeiSegnaposto()
    {
        var pulito = PromptBuilder.RipulisciErrore("Errore vicino a Mario Rossi e 'x'", ["Mario Rossi"]);
        Assert.DoesNotContain("Mario", pulito);
    }
}
