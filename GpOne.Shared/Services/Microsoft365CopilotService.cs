using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;

namespace GpOne.Services;

/// <summary>
/// Microsoft 365 Copilot ("Copilot lavoro") tramite la Copilot Chat API di Microsoft Graph (anteprima, endpoint beta).
/// Accesso delegato: ogni utente si autentica con il proprio account aziendale Entra ID (flusso device code),
/// quindi Copilot vede solo i dati a cui l'utente ha già accesso. I dati restano nel tenant Microsoft 365
/// dell'azienda (EU Data Boundary per i tenant europei). Richiede una licenza Microsoft 365 Copilot.
/// Nell'app desktop usa l'account Windows del PC tramite il broker WAM (single sign-on, nessuna password).
/// </summary>
public class Microsoft365CopilotService(AiSettingsStore store, IHttpClientFactory httpClientFactory, Microsoft365Opzioni opzioni)
{
    private const string Conversazioni = "https://graph.microsoft.com/beta/copilot/conversations";

    // Permessi delegati richiesti dalla Copilot Chat API: servono tutti perché la chiamata riesca
    private static readonly string[] Scopes =
    [
        "https://graph.microsoft.com/Sites.Read.All",
        "https://graph.microsoft.com/Mail.Read",
        "https://graph.microsoft.com/People.Read.All",
        "https://graph.microsoft.com/OnlineMeetingTranscript.Read.All",
        "https://graph.microsoft.com/Chat.Read",
        "https://graph.microsoft.com/ChannelMessage.Read.All",
        "https://graph.microsoft.com/ExternalItem.Read.All",
    ];

    private IPublicClientApplication? app;
    private string? appKey;
    private IAccount? account;
    private string? conversationId;

    public event Action? Changed;

    public bool IsConnesso => account is not null;

    public string? NomeAccount => account?.Username;

    /// <summary>true nell'app desktop: le credenziali sono quelle dell'account Windows del PC.</summary>
    public bool AccessoWindows => opzioni.FinestraWindows is not null;

    /// <summary>
    /// Accesso automatico con l'account aziendale di Windows, senza interazione.
    /// Restituisce false se non è possibile (es. account non aziendale o consenso mancante).
    /// </summary>
    public async Task<bool> AccessoAutomaticoAsync(CancellationToken cancellationToken = default)
    {
        if (!AccessoWindows || IsConnesso)
            return IsConnesso;

        // Prima gli account di lavoro che hanno già usato GpOne su questo PC, poi l'account predefinito di Windows
        List<IAccount> candidati;
        try
        {
            candidati = [.. await App().GetAccountsAsync()];
        }
        catch (Exception ex) when (ex is MsalException or InvalidOperationException or ArgumentException)
        {
            // Configurazione non valida (es. Client ID errato): niente accesso automatico
            return false;
        }
        candidati.Add(PublicClientApplication.OperatingSystemAccount);

        foreach (var candidato in candidati)
        {
            try
            {
                var result = await App().AcquireTokenSilent(Scopes, candidato).ExecuteAsync(cancellationToken);
                Connesso(result.Account);
                return true;
            }
            catch (MsalException)
            {
                // Account non utilizzabile in modo silenzioso: si prova il successivo
            }
        }
        return false;
    }

    /// <summary>
    /// Accesso con device code: <paramref name="mostraIstruzioni"/> riceve il messaggio con indirizzo e codice
    /// da inserire; il metodo termina quando l'utente ha completato l'accesso nel browser.
    /// </summary>
    public async Task AccediAsync(Func<string, Task> mostraIstruzioni, CancellationToken cancellationToken = default)
    {
        if (AccessoWindows)
        {
            // Desktop: finestra di Windows per scegliere/confermare l'account aziendale del PC
            var result = await App()
                .AcquireTokenInteractive(Scopes)
                .WithAccount(PublicClientApplication.OperatingSystemAccount)
                .ExecuteAsync(cancellationToken);
            Connesso(result.Account);
        }
        else
        {
            var result = await App()
                .AcquireTokenWithDeviceCode(Scopes, codice => mostraIstruzioni(codice.Message))
                .ExecuteAsync(cancellationToken);
            Connesso(result.Account);
        }
    }

    private void Connesso(IAccount nuovoAccount)
    {
        account = nuovoAccount;
        conversationId = null;
        Changed?.Invoke();
    }

    public void Esci()
    {
        account = null;
        conversationId = null;
        Changed?.Invoke();
    }

    public async IAsyncEnumerable<string> ChatAsync(IReadOnlyList<ChatMessage> history, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var token = await TokenAsync(cancellationToken);
        var http = httpClientFactory.CreateClient(GpOneAiService.HttpClientName);

        // La conversazione Copilot vive lato Microsoft: se ne apre una nuova a ogni nuova chat
        if (conversationId is null || history.Count(m => m.Role == ChatRole.User) == 1)
            conversationId = await CreaConversazioneAsync(http, token, cancellationToken);

        var prompt = history.Last(m => m.Role == ChatRole.User).Text;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Conversazioni}/{conversationId}/chatOverStream")
        {
            Content = JsonContent.Create(new
            {
                message = new { text = prompt },
                locationHint = new { timeZone = "Europe/Rome" }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await VerificaRispostaAsync(response, cancellationToken);

        // Ogni evento SSE contiene la conversazione aggiornata: si restituisce solo il testo nuovo della risposta
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var inviato = string.Empty;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            using var doc = JsonDocument.Parse(line[5..]);
            if (!doc.RootElement.TryGetProperty("messages", out var messages) || messages.GetArrayLength() == 0)
                continue;

            var ultimo = messages[messages.GetArrayLength() - 1];
            var testo = ultimo.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            if (testo == prompt || !testo.StartsWith(inviato, StringComparison.Ordinal))
                continue;

            yield return testo[inviato.Length..];
            inviato = testo;
        }
    }

    private async Task<string> TokenAsync(CancellationToken cancellationToken)
    {
        if (account is null)
            throw new InvalidOperationException("Accedi con l'account Microsoft 365 aziendale dalla pagina Copilot.");

        try
        {
            return (await App().AcquireTokenSilent(Scopes, account).ExecuteAsync(cancellationToken)).AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            Esci();
            throw new InvalidOperationException("Sessione Microsoft scaduta: accedi di nuovo.");
        }
    }

    private static async Task<string> CreaConversazioneAsync(HttpClient http, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Conversazioni) { Content = JsonContent.Create(new { }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request, cancellationToken);
        await VerificaRispostaAsync(response, cancellationToken);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return doc.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Microsoft 365 Copilot non ha restituito l'id della conversazione.");
    }

    private IPublicClientApplication App()
    {
        var impostazioni = store.Correnti;
        if (string.IsNullOrWhiteSpace(impostazioni.ClientId))
            throw new InvalidOperationException("Client ID di Microsoft 365 non configurato.");

        // Se l'amministratore cambia tenant o applicazione, si riparte da zero
        var key = $"{impostazioni.TenantId}|{impostazioni.ClientId}";
        if (app is null || appKey != key)
        {
            // Senza Tenant ID si usa l'organizzazione dell'account con cui si accede
            var builder = PublicClientApplicationBuilder.Create(impostazioni.ClientId)
                .WithAuthority(string.IsNullOrWhiteSpace(impostazioni.TenantId)
                    ? "https://login.microsoftonline.com/organizations"
                    : $"https://login.microsoftonline.com/{impostazioni.TenantId}");

            if (opzioni.FinestraWindows is { } finestra)
                builder = builder
                    .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows))
                    .WithParentActivityOrWindow(finestra);

            app = builder.Build();
            appKey = key;
            account = null;
            conversationId = null;
        }
        return app;
    }

    private static async Task VerificaRispostaAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var dettaglio = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Microsoft 365 Copilot ha risposto {(int)response.StatusCode} {response.ReasonPhrase}. {dettaglio}".Trim());
    }
}
