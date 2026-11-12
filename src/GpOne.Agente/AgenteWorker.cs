using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using GpOne.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace GpOne.Agente;

public class OpzioniAgente
{
    /// <summary>Indirizzo dell'API GpOne, es. https://gpone.miodominio.it</summary>
    public string ServerUrl { get; set; } = string.Empty;
    public int ClienteId { get; set; }
    /// <summary>Chiave generata in GpOne (Amministrazione > Clienti), meglio cifrata con CifraPassword.bat.</summary>
    public string Chiave { get; set; } = string.Empty;
}

/// <summary>
/// Mantiene la connessione in uscita verso l'API GpOne (nessuna porta da aprire presso il cliente)
/// e risponde alle richieste di struttura e di esecuzione delle query.
/// </summary>
public class AgenteWorker(
    IOptions<OpzioniAgente> opzioni,
    DatabaseCliente database,
    IHostApplicationLifetime lifetime,
    IHostEnvironment ambiente,
    ILogger<AgenteWorker> logger) : BackgroundService
{
    private static readonly string Versione = typeof(AgenteWorker).Assembly.GetName().Version?.ToString() ?? "0";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = opzioni.Value;
        logger.LogInformation("Avvio GpOne.Agente {Versione} - configurazione '{File}', log in '{CartellaLog}'.",
            Versione, Path.Combine(ambiente.ContentRootPath, "appsettings.json"), LogSuFile.Cartella);

        if (VerificaConfigurazione(o) is not { } chiave)
        {
            logger.LogCritical("Agente fermato: correggere la sezione GpOneAgente di appsettings.json e riavviare.");
            Environment.ExitCode = 1;
            lifetime.StopApplication();
            return;
        }

        await database.VerificaAsync(stoppingToken);

        var connessione = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(o.ServerUrl.TrimEnd('/') + "/"), ProtocolloAgente.PercorsoHub.TrimStart('/')), http =>
            {
                http.Headers[ProtocolloAgente.HeaderCliente] = o.ClienteId.ToString();
                http.Headers[ProtocolloAgente.HeaderChiave] = chiave;
                http.Headers[ProtocolloAgente.HeaderVersione] = Versione;
            })
            .WithAutomaticReconnect(new RiprovaSempre())
            .Build();
        connessione.ServerTimeout = TimeSpan.FromMinutes(2);

        connessione.On(ProtocolloAgente.MetodoLeggiSchema, async () =>
        {
            logger.LogInformation("Richiesta la struttura del database.");
            return await database.LeggiSchemaAsync();
        });
        connessione.On<RichiestaQuery, RisultatoQuery>(ProtocolloAgente.MetodoEseguiQuery, database.EseguiQueryAsync);

        connessione.Reconnecting += ex => { logger.LogWarning("Connessione persa, riconnessione in corso: {Messaggio}", ex?.Message); return Task.CompletedTask; };
        connessione.Reconnected += _ => { logger.LogInformation("Riconnesso a GpOne."); return Task.CompletedTask; };
        // Chiusa dal server (es. chiave rifiutata: il motivo arriva nel messaggio) o dopo riconnessioni fallite
        connessione.Closed += ex =>
        {
            if (ex is null)
                logger.LogWarning("Connessione con GpOne chiusa. Nuovo tentativo entro 30 secondi.");
            else
                logger.LogError("Connessione con GpOne chiusa: {Messaggio} Nuovo tentativo entro 30 secondi.", ex.Message);
            return Task.CompletedTask;
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            if (connessione.State == HubConnectionState.Disconnected)
            {
                try
                {
                    await connessione.StartAsync(stoppingToken);
                    logger.LogInformation("Collegato a GpOne ({Server}) per il cliente {ClienteId}.", o.ServerUrl, o.ClienteId);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning("Collegamento a GpOne ({Server}) non riuscito: {Suggerimento} Dettaglio: {Messaggio}. Nuovo tentativo tra 30 secondi.",
                        o.ServerUrl, SuggerimentoCollegamento(ex), ex.Message);
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
        }

        await connessione.DisposeAsync();
    }

    /// <summary>Controlla la sezione GpOneAgente e restituisce la chiave decifrata, o null (con gli errori nel log).</summary>
    private string? VerificaConfigurazione(OpzioniAgente o)
    {
        var valida = true;
        if (!Uri.TryCreate(o.ServerUrl, UriKind.Absolute, out var url) || url.Scheme is not ("https" or "http"))
        {
            logger.LogError("GpOneAgente:ServerUrl mancante o non valido ('{ServerUrl}'): indicare l'indirizzo di GpOne, es. https://gpone.miodominio.it", o.ServerUrl);
            valida = false;
        }
        else if (url.Scheme == "http" && !url.IsLoopback)
            logger.LogWarning("GpOneAgente:ServerUrl usa http: la chiave dell'agente e i risultati viaggiano in chiaro. Usare https.");

        if (o.ClienteId <= 0)
        {
            logger.LogError("GpOneAgente:ClienteId non impostato: indicare l'ID del cliente mostrato in GpOne (Clienti).");
            valida = false;
        }

        string? chiave = null;
        if (string.IsNullOrWhiteSpace(o.Chiave))
        {
            logger.LogError("GpOneAgente:Chiave non impostata: generarla in GpOne (Clienti) e cifrarla con CifraPassword.bat.");
            valida = false;
        }
        else
        {
            try
            {
                chiave = Segreti.Decifra(o.Chiave);
                if (!o.Chiave.StartsWith("ENC:", StringComparison.Ordinal))
                    logger.LogWarning("GpOneAgente:Chiave è in chiaro in appsettings.json: cifrarla con CifraPassword.bat.");
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError("GpOneAgente:Chiave non decifrabile: {Messaggio}", ex.Message);
                valida = false;
            }
        }

        if (valida)
            logger.LogInformation("Configurazione: server {Server}, cliente {ClienteId}.", o.ServerUrl, o.ClienteId);
        return valida ? chiave : null;
    }

    private static string SuggerimentoCollegamento(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            switch (e)
            {
                case HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden }:
                    return "il server ha rifiutato l'agente: controllare ClienteId e Chiave.";
                case HttpRequestException { StatusCode: HttpStatusCode.NotFound }:
                    return "indirizzo raggiunto ma GpOne non risponde su quel percorso: controllare ServerUrl.";
                case AuthenticationException:
                    return "certificato HTTPS del server non valido o non attendibile su questo computer.";
                case SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData }:
                    return "nome del server non trovato: controllare ServerUrl e il DNS.";
                case SocketException { SocketErrorCode: SocketError.ConnectionRefused }:
                    return "il server rifiuta la connessione: GpOne non è avviato o la porta in ServerUrl è sbagliata.";
                case SocketException or TimeoutException or TaskCanceledException:
                    return "server non raggiungibile: controllare ServerUrl, la rete e il firewall/proxy in uscita.";
            }
        }
        return "controllare ServerUrl e che GpOne sia avviato.";
    }

    private sealed class RiprovaSempre : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext context) =>
            TimeSpan.FromSeconds(Math.Min(60, 2 << (int)Math.Min(context.PreviousRetryCount, 5)));
    }
}
