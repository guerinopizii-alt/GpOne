using Dapper;
using GpOne.Api.Auth;
using GpOne.Api.Infrastruttura;
using GpOne.Motore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GpOne.Api.Ai;

// ConfigurazioneAi e ProviderAi sono in GpOne.Motore (condivisi con GpOne.Desktop).

public class ConfigurazioneAiService(GpOneDatabase database, Cifratura cifratura)
{
    /// <summary>Configurazione del cliente; con <paramref name="conChiave"/> la chiave API è decifrata in <see cref="ConfigurazioneAi.ApiKey"/>.</summary>
    public async Task<ConfigurazioneAi?> GetAsync(int clienteId, bool conChiave = false)
    {
        await using var connection = await database.ApriAsync();
        var riga = await connection.QuerySingleOrDefaultAsync<RigaDb>("""
            SELECT ClienteId, Provider, Modello, Endpoint, ApiKeyCifrata, Effort, Istruzioni, MaxRighe, TimeoutSecondi,
                   MaxChiamateAi, InvioDatiAnonimizzati, MaxRigheAnonimizzate
            FROM dbo.ConfigurazioniAi WHERE ClienteId = @clienteId
            """, new { clienteId });
        if (riga is null)
            return null;
        return new ConfigurazioneAi
        {
            ClienteId = riga.ClienteId,
            Provider = riga.Provider,
            Modello = riga.Modello,
            Endpoint = riga.Endpoint,
            ApiKey = conChiave && riga.ApiKeyCifrata is not null ? cifratura.Decifra(riga.ApiKeyCifrata) : null,
            ApiKeyImpostata = riga.ApiKeyCifrata is not null,
            AccountServizio = riga.Provider == ProviderAi.GeminiVertex && riga.ApiKeyCifrata is not null ? DescriviAccount(riga.ApiKeyCifrata) : null,
            Effort = riga.Effort,
            Istruzioni = riga.Istruzioni,
            MaxRighe = riga.MaxRighe,
            TimeoutSecondi = riga.TimeoutSecondi,
            MaxChiamateAi = riga.MaxChiamateAi,
            InvioDatiAnonimizzati = riga.InvioDatiAnonimizzati,
            MaxRigheAnonimizzate = riga.MaxRigheAnonimizzate
        };
    }

    /// <summary>Account e progetto dell'account di servizio Vertex, da mostrare all'amministratore (mai la chiave privata).</summary>
    private string DescriviAccount(string jsonCifrato)
    {
        try
        {
            var account = AccountServizioGoogle.Leggi(cifratura.Decifra(jsonCifrato));
            return $"{account.Email} (progetto {account.ProgettoId})";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Security.Cryptography.CryptographicException)
        {
            return "account di servizio non leggibile: ricaricare il file JSON";
        }
    }

    /// <summary>
    /// Salva la configurazione. Il segreto (chiave API o JSON dell'account di servizio) vuoto resta invariato solo se il motore
    /// non cambia: cambiando motore il segreto precedente viene tolto, così non finisce mai al fornitore sbagliato.
    /// </summary>
    public async Task SalvaAsync(int clienteId, ConfigurazioneAi c)
    {
        if (c.Verifica() is { } errore)
            throw new InvalidOperationException(errore);
        if (!ProviderAi.RichiedeChiave(c.Provider))
            c.ApiKey = null;
        if (c.Provider == ProviderAi.GeminiVertex && !string.IsNullOrWhiteSpace(c.ApiKey))
            AccountServizioGoogle.Leggi(c.ApiKey); // eccezione con messaggio chiaro se il JSON non è un account di servizio
        if (c.Provider != ProviderAi.Claude)
            c.Effort = null;
        if (c.Provider is not (ProviderAi.Ollama or ProviderAi.GeminiVertex))
            c.Endpoint = null;

        await using var connection = await database.ApriAsync();
        await connection.ExecuteAsync("""
            MERGE dbo.ConfigurazioniAi AS t
            USING (SELECT @ClienteId AS ClienteId) AS s ON t.ClienteId = s.ClienteId
            WHEN MATCHED THEN UPDATE SET Provider = @Provider, Modello = @Modello, Endpoint = @Endpoint,
                ApiKeyCifrata = CASE
                    WHEN @ApiKeyCifrata IS NOT NULL THEN @ApiKeyCifrata
                    WHEN Provider = @Provider AND @RichiedeChiave = 1 THEN ApiKeyCifrata
                    ELSE NULL END,
                Effort = @Effort, Istruzioni = @Istruzioni,
                MaxRighe = @MaxRighe, TimeoutSecondi = @TimeoutSecondi,
                MaxChiamateAi = @MaxChiamateAi, InvioDatiAnonimizzati = @InvioDatiAnonimizzati, MaxRigheAnonimizzate = @MaxRigheAnonimizzate
            WHEN NOT MATCHED THEN INSERT (ClienteId, Provider, Modello, Endpoint, ApiKeyCifrata, Effort, Istruzioni, MaxRighe, TimeoutSecondi,
                MaxChiamateAi, InvioDatiAnonimizzati, MaxRigheAnonimizzate)
                VALUES (@ClienteId, @Provider, @Modello, @Endpoint, @ApiKeyCifrata, @Effort, @Istruzioni, @MaxRighe, @TimeoutSecondi,
                @MaxChiamateAi, @InvioDatiAnonimizzati, @MaxRigheAnonimizzate);
            """, new
        {
            ClienteId = clienteId,
            c.Provider,
            RichiedeChiave = ProviderAi.RichiedeChiave(c.Provider),
            Modello = c.Modello.Trim(),
            Endpoint = string.IsNullOrWhiteSpace(c.Endpoint) ? null : c.Endpoint.Trim(),
            ApiKeyCifrata = string.IsNullOrWhiteSpace(c.ApiKey) ? null : cifratura.Cifra(c.ApiKey.Trim()),
            Effort = string.IsNullOrWhiteSpace(c.Effort) ? null : c.Effort,
            Istruzioni = string.IsNullOrWhiteSpace(c.Istruzioni) ? null : c.Istruzioni.Trim(),
            MaxRighe = Math.Clamp(c.MaxRighe, ConfigurazioneAi.MinMaxRighe, ConfigurazioneAi.MaxMaxRighe),
            TimeoutSecondi = Math.Clamp(c.TimeoutSecondi, ConfigurazioneAi.MinTimeout, ConfigurazioneAi.MaxTimeout),
            MaxChiamateAi = Math.Clamp(c.MaxChiamateAi, ConfigurazioneAi.MinChiamateAi, ConfigurazioneAi.MaxMaxChiamateAi),
            c.InvioDatiAnonimizzati,
            MaxRigheAnonimizzate = Math.Clamp(c.MaxRigheAnonimizzate, ConfigurazioneAi.MinRigheAnonimizzate, ConfigurazioneAi.MaxMaxRigheAnonimizzate)
        });
    }

    /// <summary>
    /// Modelli utilizzabili con il motore indicato. Senza un segreto nuovo si usa quello salvato, ma solo se è dello stesso motore.
    /// </summary>
    public async Task<IReadOnlyList<string>> ModelliAsync(int clienteId, ConfigurazioneAi c, ElencoModelli elenco, CancellationToken cancellationToken)
    {
        if (!ProviderAi.Tutti.Contains(c.Provider))
            throw new InvalidOperationException("Motore AI non valido.");
        var prova = new ConfigurazioneAi { Provider = c.Provider, Endpoint = c.Endpoint };
        if (ProviderAi.RichiedeChiave(c.Provider))
        {
            if (!string.IsNullOrWhiteSpace(c.ApiKey))
                prova.ApiKey = c.ApiKey.Trim();
            else if (clienteId > 0 && await GetAsync(clienteId, conChiave: true) is { } salvata && salvata.Provider == c.Provider)
                prova.ApiKey = salvata.ApiKey;
        }
        return await elenco.CaricaAsync(prova, cancellationToken);
    }

    private sealed record RigaDb(int ClienteId, string Provider, string Modello, string? Endpoint, string? ApiKeyCifrata,
        string? Effort, string? Istruzioni, int MaxRighe, int TimeoutSecondi, int MaxChiamateAi, bool InvioDatiAnonimizzati, int MaxRigheAnonimizzate);
}

[ApiController]
[Route("api/admin/clienti/{clienteId:int}/ai")]
[Authorize(Roles = TokenService.RuoloAmministratore)]
public class ConfigurazioneAiController(ConfigurazioneAiService servizio, ElencoModelli elencoModelli) : ControllerBase
{
    /// <summary>Elenco dei modelli del motore indicato (chiave nuova nel corpo, oppure quella salvata per lo stesso motore).</summary>
    [HttpPost("modelli")]
    public async Task<IActionResult> Modelli(int clienteId, ConfigurazioneAi configurazione, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await servizio.ModelliAsync(clienteId, configurazione, elencoModelli, cancellationToken));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Solo per l'amministratore: tipo e cause interne dell'errore, per poterlo diagnosticare
            var cause = new List<string>();
            for (var e = ex; e is not null; e = e.InnerException)
                cause.Add($"{e.GetType().FullName}: {e.Message}");
            return Problem(string.Join("\n  ← ", cause), title: "Impossibile caricare i modelli", statusCode: 400);
        }
    }

    [HttpGet]
    public async Task<ConfigurazioneAi> Get(int clienteId) =>
        await servizio.GetAsync(clienteId) ?? new ConfigurazioneAi { ClienteId = clienteId };

    [HttpPut]
    public async Task<IActionResult> Salva(int clienteId, ConfigurazioneAi configurazione)
    {
        try
        {
            await servizio.SalvaAsync(clienteId, configurazione);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: 400);
        }
    }
}
