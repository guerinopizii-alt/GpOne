using Dapper;
using GpOne.Core;
using Microsoft.Data.SqlClient;

namespace GpOne.Api.Infrastruttura;

/// <summary>
/// Controlli all'avvio dell'API, con messaggi chiari nel log: configurazione del database GpOne (password non decifrabile,
/// utente/password errati, server non raggiungibile...) e configurazioni AI dei clienti (chiavi mancanti o non decifrabili).
/// Se il database GpOne non è utilizzabile l'avvio si interrompe.
/// </summary>
public static class VerificaAvvio
{
    public static async Task<bool> EseguiAsync(WebApplication app, string cartellaDati)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GpOne.Avvio");
        logger.LogInformation("Avvio GpOne.Api {Versione} - ambiente {Ambiente}, cartella dati '{CartellaDati}', log in '{CartellaLog}'.",
            typeof(VerificaAvvio).Assembly.GetName().Version, app.Environment.EnvironmentName, cartellaDati, LogSuFile.Cartella);

        var database = app.Services.GetRequiredService<GpOneDatabase>();
        var (descrizione, passwordInChiaro) = database.Descrizione();
        logger.LogInformation("Database GpOne: {Descrizione}.", descrizione);
        if (passwordInChiaro)
            logger.LogWarning("La password del database GpOne è in chiaro in appsettings.json: cifrarla con CifraPassword.bat.");

        try
        {
            await DatabaseSetup.EseguiAsync(database, logger);
            logger.LogInformation("Database GpOne raggiungibile e aggiornato.");
        }
        catch (SqlException ex)
        {
            logger.LogCritical("Impossibile usare il database GpOne ({Descrizione}): {Suggerimento} Errore SQL {Numero}: {Messaggio}",
                descrizione, DiagnosticaSql.Suggerimento(ex.Number, ex.Message), ex.Number, ex.Message);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogCritical("Configurazione del database GpOne non valida: {Messaggio}", ex.Message);
            return false;
        }

        await VerificaConfigurazioniAiAsync(database, app.Services.GetRequiredService<Cifratura>(), logger);
        return true;
    }

    /// <summary>Solo avvisi: un cliente configurato male non deve impedire l'avvio per gli altri.</summary>
    private static async Task VerificaConfigurazioniAiAsync(GpOneDatabase database, Cifratura cifratura, ILogger logger)
    {
        try
        {
            await using var connection = await database.ApriAsync();
            var righe = await connection.QueryAsync<(string Cliente, string Provider, string? Endpoint, string? ApiKeyCifrata, bool ChiaveAgente)>("""
                SELECT c.Codice + ' - ' + c.RagioneSociale, a.Provider, a.Endpoint, a.ApiKeyCifrata,
                       CAST(CASE WHEN c.AgenteChiaveHash IS NULL THEN 0 ELSE 1 END AS BIT)
                FROM dbo.Clienti c JOIN dbo.ConfigurazioniAi a ON a.ClienteId = c.Id
                WHERE c.Attivo = 1
                """);

            var problemi = 0;
            foreach (var r in righe)
            {
                string? problema = r.Provider switch
                {
                    "Claude" or "Gemini" or "GeminiVertex" when r.ApiKeyCifrata is null => $"chiave/credenziali di {r.Provider} non impostate",
                    "Claude" or "Gemini" or "GeminiVertex" when !Decifrabile(cifratura, r.ApiKeyCifrata!) =>
                        $"chiave/credenziali di {r.Provider} non decifrabili (cifrate con chiavi diverse): reinserirle in Clienti",
                    "Ollama" when string.IsNullOrWhiteSpace(r.Endpoint) => "indirizzo di Ollama non impostato",
                    "Claude" or "Gemini" or "GeminiVertex" or "Ollama" => null,
                    _ => $"motore AI '{r.Provider}' non supportato"
                };
                if (problema is null && !r.ChiaveAgente)
                    problema = "chiave dell'agente non ancora generata";
                if (problema is not null)
                {
                    problemi++;
                    logger.LogWarning("Cliente {Cliente}: {Problema}.", r.Cliente, problema);
                }
            }
            logger.LogInformation("Configurazioni AI controllate: {Clienti} clienti attivi configurati, {Problemi} con problemi.", righe.Count(), problemi);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Controllo delle configurazioni AI non riuscito.");
        }
    }

    private static bool Decifrabile(Cifratura cifratura, string valore)
    {
        try
        {
            cifratura.Decifra(valore);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
