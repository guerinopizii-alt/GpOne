using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace GpOne.Core;

/// <summary>
/// Accesso in sola lettura al DB del cliente, condiviso da agente e desktop: lettura della struttura
/// (solo INFORMATION_SCHEMA) ed esecuzione di una SELECT in una transazione sempre annullata.
/// </summary>
public static class EsecutoreSql
{
    /// <summary>Solo struttura (INFORMATION_SCHEMA): nessun dato.</summary>
    public static async Task<List<OggettoSchema>> LeggiSchemaAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT t.TABLE_SCHEMA, t.TABLE_NAME, t.TABLE_TYPE, c.COLUMN_NAME, c.DATA_TYPE,
                   c.CHARACTER_MAXIMUM_LENGTH, c.NUMERIC_PRECISION, c.NUMERIC_SCALE, c.ORDINAL_POSITION
            FROM INFORMATION_SCHEMA.TABLES t
            JOIN INFORMATION_SCHEMA.COLUMNS c ON c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME
            WHERE t.TABLE_SCHEMA NOT IN ('sys', 'INFORMATION_SCHEMA')
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var oggetti = new List<OggettoSchema>();
        List<ColonnaSchema>? colonne = null;
        (string Schema, string Nome)? corrente = null;
        while (await reader.ReadAsync(cancellationToken))
        {
            var chiave = (reader.GetString(0), reader.GetString(1));
            if (corrente != chiave)
            {
                colonne = [];
                oggetti.Add(new OggettoSchema(chiave.Item1, chiave.Item2, reader.GetString(2) == "VIEW" ? "VISTA" : "TABELLA", colonne));
                corrente = chiave;
            }
            colonne!.Add(new ColonnaSchema(reader.GetString(3), Tipo(reader), reader.GetInt32(8)));
        }
        return oggetti;
    }

    /// <summary>
    /// Esegue la SELECT (ri-validata qui: seconda linea di difesa) in una transazione sempre annullata,
    /// leggendo al massimo MaxRighe righe. Gli errori SQL tornano in <see cref="RisultatoQuery.Errore"/> (non ripuliti).
    /// </summary>
    public static async Task<RisultatoQuery> EseguiQueryAsync(string connectionString, RichiestaQuery richiesta, CancellationToken cancellationToken = default)
    {
        var cronometro = Stopwatch.StartNew();
        var validazione = SqlValidator.Valida(richiesta.Sql);
        if (!validazione.Valida)
            return new RisultatoQuery([], [], false, 0, $"Query rifiutata: {validazione.Errore}");

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await using var command = new SqlCommand(richiesta.Sql, connection, transaction) { CommandTimeout = richiesta.TimeoutSecondi };
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);

            var colonne = Enumerable.Range(0, reader.FieldCount)
                .Select(i => new ColonnaRisultato(string.IsNullOrEmpty(reader.GetName(i)) ? $"Colonna{i + 1}" : reader.GetName(i), reader.GetDataTypeName(i)))
                .ToList();
            var righe = new List<object?[]>();
            var troncato = false;
            while (await reader.ReadAsync(cancellationToken))
            {
                if (righe.Count >= richiesta.MaxRighe)
                {
                    troncato = true;
                    command.Cancel();
                    break;
                }
                var riga = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                    riga[i] = Normalizza(reader.GetValue(i));
                righe.Add(riga);
            }
            await reader.CloseAsync();
            // Annullata sempre, qualunque cosa sia successa
            await transaction.RollbackAsync(CancellationToken.None);

            // Controllo a posteriori: una SELECT non modifica righe (RecordsAffected = -1). Se il server segnala righe
            // modificate, il validatore è stato aggirato: le modifiche sono già annullate e il risultato non si restituisce.
            if (reader.RecordsAffected >= 0)
                return new RisultatoQuery([], [], false, (int)cronometro.ElapsedMilliseconds,
                    "Query rifiutata: ha tentato di modificare dei dati (modifiche annullate).");

            return new RisultatoQuery(colonne, righe, troncato, (int)cronometro.ElapsedMilliseconds);
        }
        catch (SqlException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return new RisultatoQuery([], [], false, (int)cronometro.ElapsedMilliseconds, ex.Message);
        }
    }

    private static string Tipo(SqlDataReader r)
    {
        var tipo = r.GetString(4);
        if (!r.IsDBNull(5))
            return $"{tipo}({(r.GetInt32(5) == -1 ? "max" : r.GetInt32(5))})";
        if (tipo is "decimal" or "numeric" && !r.IsDBNull(6))
            return $"{tipo}({r.GetByte(6)},{r.GetInt32(7)})";
        return tipo;
    }

    /// <summary>Valori semplici e serializzabili in JSON (trasferimento via SignalR, storico, export).</summary>
    private static object? Normalizza(object valore) => valore switch
    {
        DBNull => null,
        DateTime d => d.ToString(d.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-ddTHH:mm:ss"),
        DateTimeOffset d => d.ToString("O"),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        TimeSpan t => t.ToString(),
        TimeOnly t => t.ToString("HH:mm:ss"),
        Guid g => g.ToString(),
        byte[] b => $"<binario {b.Length} byte>",
        string or bool or byte or short or int or long or decimal or double or float => valore,
        _ => valore.ToString()
    };
}
