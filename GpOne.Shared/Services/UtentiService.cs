using System.Data;
using Microsoft.Data.SqlClient;

namespace GpOne.Services;

public class UtenteGestione
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Nome { get; set; }
    public string? Cognome { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime? BloccatoFino { get; set; }
    public DateTime? UltimoAccesso { get; set; }
    public List<string> Ruoli { get; set; } = [];

    /// <summary>Configurazione AI assegnata; null = utente non abilitato a GpOne AI.</summary>
    public int? AiConfigurazioneId { get; set; }

    public bool Bloccato => BloccatoFino > DateTime.UtcNow;
}

public class RuoloGestione
{
    public int Id { get; set; }
    public string Codice { get; set; } = string.Empty;
    public string? Descrizione { get; set; }
    public bool Attivo { get; set; } = true;
    public int NumeroUtenti { get; set; }
    public List<string> Permessi { get; set; } = [];
}

public record Permesso(string Codice, string Area, string? Descrizione);

/// <summary>
/// Gestione di utenti, ruoli e permessi (tabelle degli script 01 e 02), riservata agli amministratori.
/// Impedisce di restare senza amministratori attivi e di disattivare o declassare il proprio utente.
/// </summary>
public class UtentiService(UserSessionService sessione)
{
    // ---------------- Utenti ----------------

    public async Task<IReadOnlyList<UtenteGestione>> UtentiAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var utenti = new Dictionary<int, UtenteGestione>();
        await using (var command = new SqlCommand("""
            SELECT Id, Username, Email, Nome, Cognome, Attivo, BloccatoFino, UltimoAccesso
            FROM dbo.Utenti ORDER BY Username
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                utenti[reader.GetInt32(0)] = new UtenteGestione
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    Email = reader.GetString(2),
                    Nome = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Cognome = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Attivo = reader.GetBoolean(5),
                    BloccatoFino = reader.IsDBNull(6) ? null : DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc),
                    UltimoAccesso = reader.IsDBNull(7) ? null : DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc)
                };
        }
        await using (var command = new SqlCommand("""
            SELECT ur.UtenteId, r.Codice FROM dbo.UtentiRuoli ur JOIN dbo.Ruoli r ON r.Id = ur.RuoloId ORDER BY r.Codice
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                if (utenti.TryGetValue(reader.GetInt32(0), out var utente))
                    utente.Ruoli.Add(reader.GetString(1));
        }
        // Abilitazione all'AI (script 11)
        if (await ColonnaAiAsync(connection, null, cancellationToken))
        {
            await using var command = new SqlCommand("SELECT Id, AiConfigurazioneId FROM dbo.Utenti", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                if (utenti.TryGetValue(reader.GetInt32(0), out var utente))
                    utente.AiConfigurazioneId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
        }
        return [.. utenti.Values];
    }

    /// <summary>Crea o aggiorna un utente; con <paramref name="nuovaPassword"/> imposta anche la password.</summary>
    public async Task SalvaUtenteAsync(UtenteGestione utente, string? nuovaPassword, CancellationToken cancellationToken = default)
    {
        if (utente.Id == 0 && string.IsNullOrEmpty(nuovaPassword))
            throw new InvalidOperationException("Indicare la password del nuovo utente.");
        if (!string.IsNullOrEmpty(nuovaPassword) && nuovaPassword.Length < 8)
            throw new InvalidOperationException("La password deve contenere almeno 8 caratteri.");

        var seStesso = utente.Id == sessione.Utente?.Id;
        if (seStesso && !utente.Attivo)
            throw new InvalidOperationException("Non puoi disattivare il tuo utente.");
        if (seStesso && !utente.Ruoli.Contains(UserSessionService.RuoloAmministratore, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Non puoi togliere a te stesso il ruolo di amministratore.");

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var command = new SqlCommand(utente.Id == 0
                ? """
                  INSERT INTO dbo.Utenti (Username, Email, Nome, Cognome, Attivo, PasswordHash)
                  OUTPUT INSERTED.Id
                  VALUES (@Username, @Email, @Nome, @Cognome, @Attivo, @Hash)
                  """
                : """
                  UPDATE dbo.Utenti
                  SET Username = @Username, Email = @Email, Nome = @Nome, Cognome = @Cognome, Attivo = @Attivo,
                      PasswordHash = COALESCE(@Hash, PasswordHash), DataModifica = SYSUTCDATETIME()
                  OUTPUT INSERTED.Id
                  WHERE Id = @Id
                  """, connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = utente.Id;
                command.Parameters.Add("@Username", SqlDbType.NVarChar, 100).Value = utente.Username.Trim();
                command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = utente.Email.Trim();
                command.Parameters.Add("@Nome", SqlDbType.NVarChar, 100).Value = Valore(utente.Nome);
                command.Parameters.Add("@Cognome", SqlDbType.NVarChar, 100).Value = Valore(utente.Cognome);
                command.Parameters.Add("@Attivo", SqlDbType.Bit).Value = utente.Attivo;
                command.Parameters.Add("@Hash", SqlDbType.NVarChar, 500).Value =
                    string.IsNullOrEmpty(nuovaPassword) ? DBNull.Value : UserSessionService.CalcolaHash(nuovaPassword);
                utente.Id = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
            }

            await EseguiAsync(connection, transaction, "DELETE FROM dbo.UtentiRuoli WHERE UtenteId = @Id", ("@Id", utente.Id), cancellationToken);
            foreach (var ruolo in utente.Ruoli.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await using var command = new SqlCommand(
                    "INSERT INTO dbo.UtentiRuoli (UtenteId, RuoloId) SELECT @Id, Id FROM dbo.Ruoli WHERE Codice = @Codice",
                    connection, transaction);
                command.Parameters.Add("@Id", SqlDbType.Int).Value = utente.Id;
                command.Parameters.Add("@Codice", SqlDbType.NVarChar, 50).Value = ruolo;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (await ColonnaAiAsync(connection, transaction, cancellationToken))
            {
                await using var command = new SqlCommand("UPDATE dbo.Utenti SET AiConfigurazioneId = @Ai WHERE Id = @Id", connection, transaction);
                command.Parameters.Add("@Id", SqlDbType.Int).Value = utente.Id;
                command.Parameters.Add("@Ai", SqlDbType.Int).Value = (object?)utente.AiConfigurazioneId ?? DBNull.Value;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await VerificaAmministratoriAsync(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new InvalidOperationException("Nome utente o email già usati da un altro utente.");
        }
    }

    public async Task SbloccaAsync(int utenteId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "UPDATE dbo.Utenti SET BloccatoFino = NULL, TentativiFalliti = 0 WHERE Id = @Id", connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = utenteId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task EliminaUtenteAsync(int utenteId, CancellationToken cancellationToken = default)
    {
        if (utenteId == sessione.Utente?.Id)
            throw new InvalidOperationException("Non puoi eliminare il tuo utente.");

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await EseguiAsync(connection, transaction, "DELETE FROM dbo.Utenti WHERE Id = @Id", ("@Id", utenteId), cancellationToken);
        await VerificaAmministratoriAsync(connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // ---------------- Ruoli e permessi ----------------

    public async Task<IReadOnlyList<RuoloGestione>> RuoliAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var ruoli = new Dictionary<int, RuoloGestione>();
        await using (var command = new SqlCommand("""
            SELECT r.Id, r.Codice, r.Descrizione, r.Attivo, (SELECT COUNT(*) FROM dbo.UtentiRuoli ur WHERE ur.RuoloId = r.Id)
            FROM dbo.Ruoli r ORDER BY r.Codice
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                ruoli[reader.GetInt32(0)] = new RuoloGestione
                {
                    Id = reader.GetInt32(0),
                    Codice = reader.GetString(1),
                    Descrizione = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Attivo = reader.GetBoolean(3),
                    NumeroUtenti = reader.GetInt32(4)
                };
        }

        // Permessi (script 02): se le tabelle mancano i ruoli funzionano comunque
        try
        {
            await using var command = new SqlCommand(
                "SELECT rp.RuoloId, p.Codice FROM dbo.RuoliPermessi rp JOIN dbo.Permessi p ON p.Id = rp.PermessoId", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                if (ruoli.TryGetValue(reader.GetInt32(0), out var ruolo))
                    ruolo.Permessi.Add(reader.GetString(1));
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
        }
        return [.. ruoli.Values];
    }

    public async Task<IReadOnlyList<Permesso>> PermessiAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        try
        {
            await using var command = new SqlCommand("SELECT Codice, Area, Descrizione FROM dbo.Permessi ORDER BY Area, Codice", connection);
            var permessi = new List<Permesso>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                permessi.Add(new Permesso(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
            return permessi;
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            return [];
        }
    }

    public async Task SalvaRuoloAsync(RuoloGestione ruolo, CancellationToken cancellationToken = default)
    {
        var amministratore = string.Equals(ruolo.Codice, UserSessionService.RuoloAmministratore, StringComparison.OrdinalIgnoreCase);
        if (amministratore && !ruolo.Attivo)
            throw new InvalidOperationException("Il ruolo Amministratore non può essere disattivato.");

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            // Il codice del ruolo Amministratore non si può rinominare (è usato dall'applicazione)
            await using (var command = new SqlCommand(ruolo.Id == 0
                ? "INSERT INTO dbo.Ruoli (Codice, Descrizione, Attivo) OUTPUT INSERTED.Id VALUES (@Codice, @Descrizione, @Attivo)"
                : """
                  UPDATE dbo.Ruoli
                  SET Codice = CASE WHEN Codice = @Amministratore THEN Codice ELSE @Codice END,
                      Descrizione = @Descrizione, Attivo = @Attivo
                  OUTPUT INSERTED.Id
                  WHERE Id = @Id
                  """, connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = ruolo.Id;
                command.Parameters.Add("@Codice", SqlDbType.NVarChar, 50).Value = ruolo.Codice.Trim();
                command.Parameters.Add("@Descrizione", SqlDbType.NVarChar, 200).Value = Valore(ruolo.Descrizione);
                command.Parameters.Add("@Attivo", SqlDbType.Bit).Value = ruolo.Attivo;
                command.Parameters.Add("@Amministratore", SqlDbType.NVarChar, 50).Value = UserSessionService.RuoloAmministratore;
                ruolo.Id = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
            }

            if (await TabellaEsisteAsync(connection, transaction, "dbo.RuoliPermessi", cancellationToken))
            {
                await EseguiAsync(connection, transaction, "DELETE FROM dbo.RuoliPermessi WHERE RuoloId = @Id", ("@Id", ruolo.Id), cancellationToken);
                foreach (var permesso in ruolo.Permessi.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    await using var command = new SqlCommand(
                        "INSERT INTO dbo.RuoliPermessi (RuoloId, PermessoId) SELECT @Id, Id FROM dbo.Permessi WHERE Codice = @Codice",
                        connection, transaction);
                    command.Parameters.Add("@Id", SqlDbType.Int).Value = ruolo.Id;
                    command.Parameters.Add("@Codice", SqlDbType.NVarChar, 100).Value = permesso;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new InvalidOperationException($"Esiste già un ruolo con codice '{ruolo.Codice}'.");
        }
    }

    public async Task EliminaRuoloAsync(RuoloGestione ruolo, CancellationToken cancellationToken = default)
    {
        if (string.Equals(ruolo.Codice, UserSessionService.RuoloAmministratore, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Il ruolo Amministratore non può essere eliminato.");
        if (ruolo.NumeroUtenti > 0)
            throw new InvalidOperationException("Il ruolo è assegnato ad alcuni utenti: toglilo prima dagli utenti.");

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand("DELETE FROM dbo.Ruoli WHERE Id = @Id", connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = ruolo.Id;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // ---------------- Supporto ----------------

    /// <summary>Deve restare almeno un amministratore attivo, altrimenti la modifica viene annullata.</summary>
    private static async Task VerificaAmministratoriAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            SELECT COUNT(*) FROM dbo.Utenti u
            JOIN dbo.UtentiRuoli ur ON ur.UtenteId = u.Id
            JOIN dbo.Ruoli r ON r.Id = ur.RuoloId
            WHERE u.Attivo = 1 AND r.Attivo = 1 AND r.Codice = @Amministratore
            """, connection, transaction);
        command.Parameters.Add("@Amministratore", SqlDbType.NVarChar, 50).Value = UserSessionService.RuoloAmministratore;
        if ((int)(await command.ExecuteScalarAsync(cancellationToken))! == 0)
            throw new InvalidOperationException("Deve rimanere almeno un amministratore attivo.");
    }

    private static async Task<bool> TabellaEsisteAsync(SqlConnection connection, SqlTransaction transaction, string nome, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT OBJECT_ID(@Nome, N'U')", connection, transaction);
        command.Parameters.Add("@Nome", SqlDbType.NVarChar, 256).Value = nome;
        return await command.ExecuteScalarAsync(cancellationToken) is not (null or DBNull);
    }

    private static async Task EseguiAsync(SqlConnection connection, SqlTransaction transaction, string sql, (string Nome, int Valore) parametro, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add(parametro.Nome, SqlDbType.Int).Value = parametro.Valore;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> ColonnaAiAsync(SqlConnection connection, SqlTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT COL_LENGTH(N'dbo.Utenti', N'AiConfigurazioneId')", connection, transaction);
        return await command.ExecuteScalarAsync(cancellationToken) is not (null or DBNull);
    }

    private static object Valore(string? testo) => string.IsNullOrWhiteSpace(testo) ? DBNull.Value : testo.Trim();

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (!sessione.IsInRole(UserSessionService.RuoloAmministratore))
            throw new InvalidOperationException("Operazione riservata agli amministratori.");

        var connection = new SqlConnection(sessione.ConnectionString
            ?? throw new InvalidOperationException("Nessuna connessione al database attiva."));
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
