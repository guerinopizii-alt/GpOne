using GpOne.Core;
using Microsoft.Data.SqlClient;

namespace GpOne.Agente;

/// <summary>
/// Accesso in sola lettura al DB del cliente. La stringa di connessione resta su questo computer
/// (ConnectionStrings:Cliente, password "ENC:..."). Consigliato un utente SQL con il solo ruolo db_datareader.
/// </summary>
public class DatabaseCliente(IConfiguration configuration, ILogger<DatabaseCliente> logger)
{
    private string ConnectionString
    {
        get
        {
            var valore = configuration.GetConnectionString("Cliente");
            if (string.IsNullOrWhiteSpace(valore))
                throw new InvalidOperationException("ConnectionStrings:Cliente non configurata nell'agente.");
            var builder = new SqlConnectionStringBuilder(valore) { ApplicationName = "GpOne Agente", ApplicationIntent = ApplicationIntent.ReadOnly };
            builder.Password = Segreti.Decifra(builder.Password);
            return builder.ConnectionString;
        }
    }

    /// <summary>
    /// Controllo all'avvio, solo per il log: configurazione (mai la password), collegamento e permessi dell'utente SQL.
    /// Non blocca l'agente: il database potrebbe tornare disponibile più tardi.
    /// </summary>
    public async Task VerificaAsync(CancellationToken cancellationToken)
    {
        var valore = configuration.GetConnectionString("Cliente");
        if (string.IsNullOrWhiteSpace(valore))
        {
            logger.LogError("ConnectionStrings:Cliente non configurata in appsettings.json: l'agente non potrà eseguire query.");
            return;
        }

        string descrizione;
        try
        {
            var configurata = new SqlConnectionStringBuilder(valore);
            var cifrata = configurata.Password.StartsWith("ENC:", StringComparison.Ordinal);
            descrizione = configurata.IntegratedSecurity
                ? $"server '{configurata.DataSource}', database '{configurata.InitialCatalog}', autenticazione Windows (account del servizio)"
                : $"server '{configurata.DataSource}', database '{configurata.InitialCatalog}', utente '{configurata.UserID}', password {(cifrata ? "cifrata" : "in chiaro")}";
            if (!configurata.IntegratedSecurity && !cifrata && configurata.Password.Length > 0)
                logger.LogWarning("La password del database è in chiaro in appsettings.json: cifrarla con CifraPassword.bat.");
            if (string.IsNullOrWhiteSpace(configurata.InitialCatalog))
                logger.LogWarning("Nella stringa di connessione manca 'Initial Catalog': si userà il database predefinito dell'utente.");
        }
        catch (ArgumentException ex)
        {
            logger.LogError("ConnectionStrings:Cliente non valida: {Messaggio}", ex.Message);
            return;
        }
        logger.LogInformation("Database del cliente: {Descrizione}.", descrizione);

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("""
                SELECT @@VERSION, DB_NAME(), SUSER_SNAME(),
                       IS_SRVROLEMEMBER('sysadmin'), IS_ROLEMEMBER('db_owner'), IS_ROLEMEMBER('db_datawriter'), IS_ROLEMEMBER('db_ddladmin')
                """, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);

            logger.LogInformation("Collegamento al database riuscito: '{Database}' come '{Utente}' ({Versione}).",
                reader.GetString(1), reader.GetString(2), reader.GetString(0).Split('\n')[0].Trim());

            var permessiAmpi = new[] { (3, "sysadmin"), (4, "db_owner"), (5, "db_datawriter"), (6, "db_ddladmin") }
                .Where(p => !reader.IsDBNull(p.Item1) && reader.GetInt32(p.Item1) == 1)
                .Select(p => p.Item2)
                .ToList();
            if (permessiAmpi.Count > 0)
                logger.LogWarning("L'utente SQL ha permessi più ampi della sola lettura ({Ruoli}): le query sono comunque controllate e annullate, " +
                    "ma è consigliato un utente con il solo ruolo db_datareader.", string.Join(", ", permessiAmpi));
        }
        catch (SqlException ex)
        {
            logger.LogError("Collegamento al database del cliente non riuscito ({Descrizione}): {Suggerimento} Errore SQL {Numero}: {Messaggio}",
                descrizione, DiagnosticaSql.Suggerimento(ex.Number, ex.Message), ex.Number, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError("Configurazione del database del cliente non valida: {Messaggio}", ex.Message);
        }
    }

    /// <summary>Solo struttura (INFORMATION_SCHEMA): nessun dato.</summary>
    public Task<List<OggettoSchema>> LeggiSchemaAsync() => EsecutoreSql.LeggiSchemaAsync(ConnectionString);

    /// <summary>
    /// Esegue la SELECT (ri-validata da <see cref="EsecutoreSql"/>: seconda linea di difesa) in una transazione sempre annullata,
    /// leggendo al massimo MaxRighe righe.
    /// </summary>
    public async Task<RisultatoQuery> EseguiQueryAsync(RichiestaQuery richiesta)
    {
        var risultato = await EsecutoreSql.EseguiQueryAsync(ConnectionString, richiesta);
        if (risultato.Errore is null)
            logger.LogInformation("Query eseguita: {Righe} righe in {Ms} ms.", risultato.Righe.Count, risultato.DurataMs);
        else
            logger.LogWarning("Query non eseguita: {Messaggio}", risultato.Errore);
        return risultato;
    }
}
