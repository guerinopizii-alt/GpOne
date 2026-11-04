using System.Data;
using Microsoft.Data.SqlClient;

namespace GpOne.Services;

public record InvioAi(DateTime DataOra, string Username, string Motore, string Domanda, string? Query, int Caratteri,
    int? MsDati = null, int? MsPrimaRisposta = null, int? MsTotale = null);

/// <summary>
/// Registro degli invii all'AI (chi, quando, a quale motore, con quali query sui dati): utile per GDPR e controlli.
/// I dati letti dal database non vengono registrati, solo le query.
/// </summary>
public class LogInviiService(UserSessionService sessione)
{
    /// <summary>Registra un invio; un errore del registro non deve bloccare la chat.</summary>
    public async Task RegistraAsync(string motore, string domanda, string? query, int caratteri, TempiAi? tempi = null,
        CancellationToken cancellationToken = default)
    {
        if (sessione.Utente is not { } utente || sessione.ConnectionString is not { } connectionString)
            return;
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            // Con lo script 10 si registrano anche i tempi; senza, solo i dati di base
            var conTempi = tempi is not null && await HaColonneTempiAsync(connection, cancellationToken);
            await using var command = new SqlCommand(conTempi
                ? """
                  INSERT INTO dbo.AiLogInvii (UtenteId, Username, Motore, Domanda, Query, Caratteri, MsDati, MsPrimaRisposta, MsTotale)
                  VALUES (@UtenteId, @Username, @Motore, @Domanda, @Query, @Caratteri, @MsDati, @MsPrimaRisposta, @MsTotale)
                  """
                : """
                  INSERT INTO dbo.AiLogInvii (UtenteId, Username, Motore, Domanda, Query, Caratteri)
                  VALUES (@UtenteId, @Username, @Motore, @Domanda, @Query, @Caratteri)
                  """, connection);
            if (conTempi)
            {
                command.Parameters.Add("@MsDati", SqlDbType.Int).Value = (int)tempi!.MsDati;
                command.Parameters.Add("@MsPrimaRisposta", SqlDbType.Int).Value = tempi.MsPrimaRisposta is { } ms ? (int)ms : DBNull.Value;
                command.Parameters.Add("@MsTotale", SqlDbType.Int).Value = (int)tempi.MsTotale;
            }
            command.Parameters.Add("@UtenteId", SqlDbType.Int).Value = utente.Id;
            command.Parameters.Add("@Username", SqlDbType.NVarChar, 100).Value = utente.Username;
            command.Parameters.Add("@Motore", SqlDbType.NVarChar, 200).Value = motore;
            command.Parameters.Add("@Domanda", SqlDbType.NVarChar, -1).Value = domanda;
            command.Parameters.Add("@Query", SqlDbType.NVarChar, -1).Value = (object?)query ?? DBNull.Value;
            command.Parameters.Add("@Caratteri", SqlDbType.Int).Value = caratteri;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException)
        {
            // Registro non disponibile (es. script 09 non eseguito): la chat prosegue
        }
    }

    public async Task<IReadOnlyList<InvioAi>> UltimiAsync(int quanti = 200, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(sessione.ConnectionString
            ?? throw new InvalidOperationException("Nessuna connessione al database attiva."));
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand($"""
            SELECT TOP (@Quanti) DataOra, Username, Motore, Domanda, Query, Caratteri,
                   {(await HaColonneTempiAsync(connection, cancellationToken) ? "MsDati, MsPrimaRisposta, MsTotale" : "NULL, NULL, NULL")}
            FROM dbo.AiLogInvii
            ORDER BY DataOra DESC, Id DESC
            """, connection);
        command.Parameters.Add("@Quanti", SqlDbType.Int).Value = quanti;

        var invii = new List<InvioAi>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            invii.Add(new InvioAi(
                DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc),
                reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8)));
        return invii;
    }

    private static async Task<bool> HaColonneTempiAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT COL_LENGTH(N'dbo.AiLogInvii', N'MsTotale')", connection);
        return await command.ExecuteScalarAsync(cancellationToken) is not (null or DBNull);
    }
}
