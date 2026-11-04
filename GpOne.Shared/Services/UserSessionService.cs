using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace GpOne.Services;

public record UtenteCorrente(int Id, string Username, string NomeCompleto, IReadOnlyList<string> Ruoli);

/// <summary>
/// Sessione dell'utente applicativo (tabelle dbo.Utenti, dbo.Ruoli, dbo.UtentiRuoli).
/// Vive per la durata del circuito Blazor: ricaricando la pagina si torna al login.
/// </summary>
public class UserSessionService(ConnectionProfileStore profiles)
{
    public const string RuoloAmministratore = "Amministratore";

    private const int MaxTentativi = 5;
    private const int MinutiBlocco = 15;
    private const string CredenzialiNonValide = "Nome utente o password non validi.";

    private static readonly PasswordHasher<UtenteCorrente> Hasher = new();

    public event Action? Changed;

    /// <summary>Hash di una password nel formato usato per il login (ASP.NET Identity).</summary>
    public static string CalcolaHash(string password) => Hasher.HashPassword(null!, password);

    public UtenteCorrente? Utente { get; private set; }

    public string? Profilo { get; private set; }

    public IReadOnlyList<string> Profili => profiles.Nomi;

    public bool IsAuthenticated => Utente is not null;

    /// <summary>Stringa di connessione del profilo attivo, per l'accesso ai dati: non mostrarla all'utente.</summary>
    public string? ConnectionString => Profilo is null ? null : profiles.GetConnectionString(Profilo);

    public bool IsInRole(string ruolo) => Utente?.Ruoli.Contains(ruolo, StringComparer.OrdinalIgnoreCase) == true;

    public async Task<int> ContaUtentiAsync(string profilo, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(profilo, cancellationToken);
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.Utenti", connection);
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>Restituisce null se l'accesso riesce, altrimenti il messaggio da mostrare.</summary>
    public async Task<string?> LoginAsync(string profilo, string username, string password, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(profilo, cancellationToken);

        int id;
        string nomeCompleto;
        string? hash;
        bool attivo;
        DateTime? bloccatoFino;

        await using (var command = new SqlCommand("""
            SELECT Id, Username, LTRIM(RTRIM(CONCAT(Nome, ' ', Cognome))), PasswordHash, Attivo, BloccatoFino
            FROM dbo.Utenti
            WHERE Username = @Username
            """, connection))
        {
            command.Parameters.Add("@Username", SqlDbType.NVarChar, 100).Value = username;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return CredenzialiNonValide;

            id = reader.GetInt32(0);
            username = reader.GetString(1);
            nomeCompleto = reader.GetString(2);
            hash = reader.IsDBNull(3) ? null : reader.GetString(3);
            attivo = reader.GetBoolean(4);
            bloccatoFino = reader.IsDBNull(5) ? null : DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc);
        }

        if (bloccatoFino > DateTime.UtcNow)
            return $"Utente bloccato per troppi tentativi falliti. Riprovare dopo le {bloccatoFino.Value.ToLocalTime():HH:mm}.";

        var verifica = hash is null
            ? PasswordVerificationResult.Failed
            : Hasher.VerifyHashedPassword(null!, hash, password);

        if (verifica == PasswordVerificationResult.Failed)
        {
            await RegistraTentativoFallitoAsync(connection, id, cancellationToken);
            return CredenzialiNonValide;
        }

        if (!attivo)
            return "Utente disattivato.";

        await using (var command = new SqlCommand("""
            UPDATE dbo.Utenti
            SET TentativiFalliti = 0,
                BloccatoFino = NULL,
                UltimoAccesso = SYSUTCDATETIME(),
                PasswordHash = COALESCE(@NuovoHash, PasswordHash)
            WHERE Id = @Id
            """, connection))
        {
            var nuovoHash = verifica == PasswordVerificationResult.SuccessRehashNeeded
                ? Hasher.HashPassword(null!, password)
                : null;
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@NuovoHash", SqlDbType.NVarChar, 500).Value = (object?)nuovoHash ?? DBNull.Value;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var ruoli = new List<string>();
        await using (var command = new SqlCommand("""
            SELECT r.Codice
            FROM dbo.UtentiRuoli ur
            JOIN dbo.Ruoli r ON r.Id = ur.RuoloId
            WHERE ur.UtenteId = @Id AND r.Attivo = 1
            """, connection))
        {
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                ruoli.Add(reader.GetString(0));
        }

        Utente = new UtenteCorrente(id, username, nomeCompleto.Length > 0 ? nomeCompleto : username, ruoli);
        Profilo = profilo;
        Changed?.Invoke();
        return null;
    }

    /// <summary>Crea il primo utente con ruolo Amministratore; consentito solo se la tabella Utenti è vuota.</summary>
    public async Task<string?> CreaAmministratoreAsync(string profilo, string username, string email, string password, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(profilo, cancellationToken);
        await using var command = new SqlCommand("""
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            IF EXISTS (SELECT 1 FROM dbo.Utenti WITH (UPDLOCK, HOLDLOCK))
            BEGIN
                ROLLBACK;
                THROW 50001, 'Esiste già almeno un utente.', 1;
            END;

            INSERT INTO dbo.Utenti (Username, Email, PasswordHash) VALUES (@Username, @Email, @Hash);
            DECLARE @Id INT = SCOPE_IDENTITY();

            INSERT INTO dbo.UtentiRuoli (UtenteId, RuoloId)
            SELECT @Id, Id FROM dbo.Ruoli WHERE Codice = @Ruolo;

            COMMIT;
            """, connection);
        command.Parameters.Add("@Username", SqlDbType.NVarChar, 100).Value = username;
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = email;
        command.Parameters.Add("@Hash", SqlDbType.NVarChar, 500).Value = Hasher.HashPassword(null!, password);
        command.Parameters.Add("@Ruolo", SqlDbType.NVarChar, 50).Value = RuoloAmministratore;

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            return null;
        }
        catch (SqlException ex) when (ex.Number == 50001)
        {
            return ex.Message;
        }
    }

    /// <summary>Cambia la password dell'utente corrente; restituisce null se riesce, altrimenti il messaggio d'errore.</summary>
    public async Task<string?> CambiaPasswordAsync(string passwordAttuale, string nuovaPassword, CancellationToken cancellationToken = default)
    {
        if (Utente is null || Profilo is null)
            return "Sessione scaduta: effettuare di nuovo l'accesso.";

        await using var connection = await OpenAsync(Profilo, cancellationToken);

        string? hash;
        await using (var command = new SqlCommand("SELECT PasswordHash FROM dbo.Utenti WHERE Id = @Id", connection))
        {
            command.Parameters.Add("@Id", SqlDbType.Int).Value = Utente.Id;
            hash = await command.ExecuteScalarAsync(cancellationToken) as string;
        }

        if (hash is null || Hasher.VerifyHashedPassword(null!, hash, passwordAttuale) == PasswordVerificationResult.Failed)
            return "La password attuale non è corretta.";

        await using (var command = new SqlCommand("""
            UPDATE dbo.Utenti
            SET PasswordHash = @Hash, DataModifica = SYSUTCDATETIME()
            WHERE Id = @Id
            """, connection))
        {
            command.Parameters.Add("@Id", SqlDbType.Int).Value = Utente.Id;
            command.Parameters.Add("@Hash", SqlDbType.NVarChar, 500).Value = Hasher.HashPassword(null!, nuovaPassword);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return null;
    }

    public void Logout()
    {
        Utente = null;
        Profilo = null;
        Changed?.Invoke();
    }

    private async Task<SqlConnection> OpenAsync(string profilo, CancellationToken cancellationToken)
    {
        var connectionString = profiles.GetConnectionString(profilo)
            ?? throw new InvalidOperationException($"Profilo di connessione '{profilo}' non configurato.");

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task RegistraTentativoFallitoAsync(SqlConnection connection, int id, CancellationToken cancellationToken)
    {
        // Le espressioni a destra usano i valori precedenti all'UPDATE
        await using var command = new SqlCommand("""
            UPDATE dbo.Utenti
            SET BloccatoFino = CASE WHEN TentativiFalliti + 1 >= @Max THEN DATEADD(MINUTE, @Minuti, SYSUTCDATETIME()) ELSE BloccatoFino END,
                TentativiFalliti = CASE WHEN TentativiFalliti + 1 >= @Max THEN 0 ELSE TentativiFalliti + 1 END
            WHERE Id = @Id
            """, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        command.Parameters.Add("@Max", SqlDbType.Int).Value = MaxTentativi;
        command.Parameters.Add("@Minuti", SqlDbType.Int).Value = MinutiBlocco;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
