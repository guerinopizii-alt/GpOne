using System.Data;
using Microsoft.Data.SqlClient;

namespace GpOne.Services;

public record ConversazioneInfo(int Id, string Titolo, string? Motore, DateTime DataModifica);

/// <summary>
/// Storico delle conversazioni con GpOne AI (tabelle dbo.AiConversazioni e dbo.AiMessaggi).
/// Ogni utente vede e modifica solo le proprie conversazioni.
/// </summary>
public class ConversazioniService(UserSessionService sessione)
{
    private const int LunghezzaTitolo = 80;

    public async Task<IReadOnlyList<ConversazioneInfo>> ElencoAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT TOP (50) Id, Titolo, Motore, DataModifica
            FROM dbo.AiConversazioni
            WHERE UtenteId = @UtenteId
            ORDER BY DataModifica DESC
            """, connection);
        command.Parameters.Add("@UtenteId", SqlDbType.Int).Value = UtenteId;

        var conversazioni = new List<ConversazioneInfo>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            conversazioni.Add(new ConversazioneInfo(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc).ToLocalTime()));
        return conversazioni;
    }

    /// <summary>Crea una conversazione con il titolo ricavato dalla prima domanda; restituisce l'id.</summary>
    public async Task<int> CreaAsync(string primaDomanda, string? motore, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            INSERT INTO dbo.AiConversazioni (UtenteId, Titolo, Motore)
            OUTPUT INSERTED.Id
            VALUES (@UtenteId, @Titolo, @Motore)
            """, connection);
        command.Parameters.Add("@UtenteId", SqlDbType.Int).Value = UtenteId;
        command.Parameters.Add("@Titolo", SqlDbType.NVarChar, 200).Value = Titolo(primaDomanda);
        command.Parameters.Add("@Motore", SqlDbType.NVarChar, 200).Value = (object?)motore ?? DBNull.Value;
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task AggiungiMessaggioAsync(int conversazioneId, ChatMessage messaggio, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        // Il messaggio viene salvato solo se la conversazione appartiene all'utente corrente
        await using var command = new SqlCommand("""
            INSERT INTO dbo.AiMessaggi (ConversazioneId, Ruolo, Testo, Dettagli, DataOra)
            SELECT c.Id, @Ruolo, @Testo, @Dettagli, @DataOra
            FROM dbo.AiConversazioni c
            WHERE c.Id = @ConversazioneId AND c.UtenteId = @UtenteId;

            UPDATE dbo.AiConversazioni
            SET DataModifica = SYSUTCDATETIME()
            WHERE Id = @ConversazioneId AND UtenteId = @UtenteId;
            """, connection);
        command.Parameters.Add("@ConversazioneId", SqlDbType.Int).Value = conversazioneId;
        command.Parameters.Add("@UtenteId", SqlDbType.Int).Value = UtenteId;
        command.Parameters.Add("@Ruolo", SqlDbType.NVarChar, 20).Value = messaggio.Role.ToString();
        command.Parameters.Add("@Testo", SqlDbType.NVarChar, -1).Value = messaggio.Text;
        command.Parameters.Add("@Dettagli", SqlDbType.NVarChar, -1).Value = (object?)messaggio.Dettagli ?? DBNull.Value;
        command.Parameters.Add("@DataOra", SqlDbType.DateTime2).Value = messaggio.Timestamp;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> MessaggiAsync(int conversazioneId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT m.Ruolo, m.Testo, m.DataOra, m.Dettagli
            FROM dbo.AiMessaggi m
            JOIN dbo.AiConversazioni c ON c.Id = m.ConversazioneId
            WHERE m.ConversazioneId = @ConversazioneId AND c.UtenteId = @UtenteId
            ORDER BY m.Id
            """, connection);
        command.Parameters.Add("@ConversazioneId", SqlDbType.Int).Value = conversazioneId;
        command.Parameters.Add("@UtenteId", SqlDbType.Int).Value = UtenteId;

        var messaggi = new List<ChatMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            messaggi.Add(new ChatMessage(
                Enum.TryParse<ChatRole>(reader.GetString(0), out var ruolo) ? ruolo : ChatRole.Assistant,
                reader.GetString(1),
                reader.GetDateTime(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        return messaggi;
    }

    public async Task EliminaAsync(int conversazioneId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "DELETE FROM dbo.AiConversazioni WHERE Id = @ConversazioneId AND UtenteId = @UtenteId", connection);
        command.Parameters.Add("@ConversazioneId", SqlDbType.Int).Value = conversazioneId;
        command.Parameters.Add("@UtenteId", SqlDbType.Int).Value = UtenteId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private int UtenteId => sessione.Utente?.Id
        ?? throw new InvalidOperationException("Nessun utente autenticato.");

    private static string Titolo(string domanda)
    {
        var titolo = string.Join(' ', domanda.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(10));
        return titolo.Length <= LunghezzaTitolo ? titolo : titolo[..LunghezzaTitolo] + "…";
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(sessione.ConnectionString
            ?? throw new InvalidOperationException("Nessuna connessione al database attiva."));
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
