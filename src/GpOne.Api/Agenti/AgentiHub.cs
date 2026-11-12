using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using GpOne.Api.Infrastruttura;
using GpOne.Core;
using Microsoft.AspNetCore.SignalR;

namespace GpOne.Api.Agenti;

/// <summary>Agenti collegati: ClienteId → ConnectionId SignalR (vince l'ultima connessione).</summary>
public class AgentiRegistro
{
    private readonly ConcurrentDictionary<int, string> connessioni = new();

    public void Registra(int clienteId, string connectionId) => connessioni[clienteId] = connectionId;

    public void Rimuovi(string connectionId)
    {
        foreach (var (clienteId, id) in connessioni)
            if (id == connectionId)
                connessioni.TryRemove(new KeyValuePair<int, string>(clienteId, id));
    }

    public string? Connessione(int clienteId) => connessioni.GetValueOrDefault(clienteId);

    public bool Collegato(int clienteId) => connessioni.ContainsKey(clienteId);
}

/// <summary>
/// Hub a cui si collegano gli agenti installati dai clienti (connessione in uscita dalla rete del cliente).
/// Autenticazione con gli header ClienteId + chiave agente, verificata contro l'hash in dbo.Clienti.
/// </summary>
public class AgentiHub(GpOneDatabase database, AgentiRegistro registro, ILogger<AgentiHub> logger) : Hub
{
    public static byte[] HashChiave(string chiave) => SHA256.HashData(Encoding.UTF8.GetBytes(chiave));

    public override async Task OnConnectedAsync()
    {
        var richiesta = Context.GetHttpContext()?.Request;
        var chiave = richiesta?.Headers[ProtocolloAgente.HeaderChiave].ToString();
        if (!int.TryParse(richiesta?.Headers[ProtocolloAgente.HeaderCliente], out var clienteId) || string.IsNullOrEmpty(chiave))
        {
            // HubException: il messaggio arriva all'agente, che lo scrive nel suo log
            throw new HubException("Richiesta dell'agente senza ClienteId o chiave.");
        }

        await using var connection = await database.ApriAsync();
        var hash = await connection.ExecuteScalarAsync<byte[]?>(
            "SELECT AgenteChiaveHash FROM dbo.Clienti WHERE Id = @clienteId AND Attivo = 1", new { clienteId });
        if (hash is null || !CryptographicOperations.FixedTimeEquals(hash, HashChiave(chiave)))
        {
            logger.LogWarning("Agente rifiutato per il cliente {ClienteId}: chiave non valida.", clienteId);
            throw new HubException($"Chiave dell'agente non valida per il cliente {clienteId} (o cliente inesistente/non attivo): controllare GpOneAgente:ClienteId e Chiave, o rigenerare la chiave in Clienti.");
        }

        Context.Items["ClienteId"] = clienteId;
        registro.Registra(clienteId, Context.ConnectionId);
        await connection.ExecuteAsync(
            "UPDATE dbo.Clienti SET AgenteUltimoContatto = SYSUTCDATETIME(), AgenteVersione = @versione WHERE Id = @clienteId",
            new { clienteId, versione = richiesta!.Headers[ProtocolloAgente.HeaderVersione].ToString() });
        logger.LogInformation("Agente collegato per il cliente {ClienteId}.", clienteId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        registro.Rimuovi(Context.ConnectionId);
        if (Context.Items.TryGetValue("ClienteId", out var clienteId))
            logger.LogInformation("Agente scollegato per il cliente {ClienteId}.", clienteId);
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>Chiamate dall'API all'agente di un cliente, con attesa della risposta.</summary>
public class AgenteClient(IHubContext<AgentiHub> hub, AgentiRegistro registro)
{
    private ISingleClientProxy Agente(int clienteId) =>
        registro.Connessione(clienteId) is { } connectionId
            ? hub.Clients.Client(connectionId)
            : throw new InvalidOperationException("L'agente GpOne del cliente non è collegato: verificare che il servizio sia avviato presso il cliente.");

    public async Task<IReadOnlyList<OggettoSchema>> LeggiSchemaAsync(int clienteId, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        return await Agente(clienteId).InvokeAsync<List<OggettoSchema>>(ProtocolloAgente.MetodoLeggiSchema, timeout.Token);
    }

    public async Task<RisultatoQuery> EseguiQueryAsync(int clienteId, RichiestaQuery richiesta, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Margine oltre al timeout del comando SQL per lettura e trasferimento delle righe
        timeout.CancelAfter(TimeSpan.FromSeconds(richiesta.TimeoutSecondi + 30));
        return await Agente(clienteId).InvokeAsync<RisultatoQuery>(ProtocolloAgente.MetodoEseguiQuery, richiesta, timeout.Token);
    }
}
