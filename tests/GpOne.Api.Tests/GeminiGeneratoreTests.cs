using System.Net;
using System.Text;
using GpOne.Motore;

namespace GpOne.Api.Tests;

/// <summary>Gemini (Google AI Studio) con un server finto: richiesta inviata e lettura della risposta.</summary>
public class GeminiGeneratoreTests
{
    private sealed class ServerFinto(HttpStatusCode stato, string risposta) : HttpMessageHandler
    {
        public HttpRequestMessage? Richiesta { get; private set; }
        public string? Corpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Richiesta = request;
            Corpo = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(stato) { Content = new StringContent(risposta, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly ConfigurazioneAi Configurazione = new() { Provider = ProviderAi.Gemini, Modello = "gemini-prova", ApiKey = "chiave-segreta" };

    [Fact]
    public async Task MandaSoloIstruzioniEDomandaELeggeLaProposta()
    {
        var server = new ServerFinto(HttpStatusCode.OK, """
            {"candidates":[{"content":{"parts":[{"text":"{\"sql\":\"SELECT 1 AS Uno\",\"spiegazione\":\"prova\",\"grafico\":{\"tipo\":\"nessuno\",\"x\":\"\",\"y\":[]}}"}]},"finishReason":"STOP"}]}
            """);
        var generatore = new GeminiGeneratore(() => new HttpClient(server));

        var proposta = await generatore.GeneraAsync(Configurazione, "ISTRUZIONI-STRUTTURA", "DOMANDA-UTENTE", CancellationToken.None);

        Assert.Equal("SELECT 1 AS Uno", proposta.Sql);
        Assert.Null(proposta.Grafico);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-prova:generateContent", server.Richiesta!.RequestUri!.ToString());
        Assert.Equal("chiave-segreta", Assert.Single(server.Richiesta.Headers.GetValues("x-goog-api-key")));
        Assert.Contains("ISTRUZIONI-STRUTTURA", server.Corpo);
        Assert.Contains("DOMANDA-UTENTE", server.Corpo);
        Assert.Contains("\"responseMimeType\":\"application/json\"", server.Corpo);
        Assert.DoesNotContain("chiave-segreta", server.Corpo);
    }

    [Fact]
    public async Task ErroreDiGoogleDiventaMessaggioLeggibile()
    {
        var server = new ServerFinto(HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"API key not valid.","status":"INVALID_ARGUMENT"}}""");
        var generatore = new GeminiGeneratore(() => new HttpClient(server));

        var errore = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            generatore.GeneraAsync(Configurazione, "i", "d", CancellationToken.None));

        Assert.Contains("API key not valid.", errore.Message);
    }

    [Fact]
    public async Task RichiestaBloccataDaGoogle()
    {
        var server = new ServerFinto(HttpStatusCode.OK, """{"promptFeedback":{"blockReason":"SAFETY"}}""");
        var generatore = new GeminiGeneratore(() => new HttpClient(server));

        var errore = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            generatore.GeneraAsync(Configurazione, "i", "d", CancellationToken.None));

        Assert.Contains("SAFETY", errore.Message);
    }

    [Theory]
    [InlineData("""{"type":"authorized_user"}""")]
    [InlineData("""{"type":"service_account","project_id":"p"}""")]
    [InlineData("non json")]
    public void AccountDiServizioNonValidoVieneRifiutato(string json) =>
        Assert.Throws<InvalidOperationException>(() => AccountServizioGoogle.Leggi(json));

    [Fact]
    public void VerificaAccettaIQuattroMotoriEControllaLaRegioneVertex()
    {
        foreach (var p in ProviderAi.Tutti)
        {
            var endpoint = p == ProviderAi.Ollama ? "http://localhost:11434" : p == ProviderAi.GeminiVertex ? "europe-west8" : null;
            Assert.Null(new ConfigurazioneAi { Provider = p, Modello = "m", Endpoint = endpoint }.Verifica());
        }
        Assert.NotNull(new ConfigurazioneAi { Provider = ProviderAi.GeminiVertex, Modello = "m", Endpoint = "europe-west8/../x" }.Verifica());
        Assert.NotNull(new ConfigurazioneAi { Provider = "OpenAI", Modello = "m" }.Verifica());
    }
}
