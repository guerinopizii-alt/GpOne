using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace GpOne.Services;

/// <summary>Parametri di una connessione inseriti a mano: restano solo in memoria e non vengono salvati.</summary>
public class ParametriConnessione
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public bool AutenticazioneWindows { get; set; }
    public string? Utente { get; set; }
    public string? Password { get; set; }
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; } = true;

    /// <summary>Profilo della connessione attiva in uso al posto dei campi (la password non è mai esposta).</summary>
    public string? NomeProfilo { get; private set; }

    private string? connectionStringProfilo;

    public void UsaProfilo(string nomeProfilo, string connectionString)
    {
        NomeProfilo = nomeProfilo;
        connectionStringProfilo = connectionString;
        var builder = new SqlConnectionStringBuilder(connectionString);
        Server = builder.DataSource;
        Database = builder.InitialCatalog;
    }

    public void Svuota()
    {
        NomeProfilo = null;
        connectionStringProfilo = null;
        Server = Database = string.Empty;
        Utente = Password = null;
        AutenticazioneWindows = false;
    }

    public string ConnectionString
    {
        get
        {
            if (connectionStringProfilo is not null)
                return connectionStringProfilo;

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = Server.Trim(),
                InitialCatalog = Database.Trim(),
                Encrypt = Encrypt,
                TrustServerCertificate = TrustServerCertificate,
                ApplicationName = "GpOne - Copia tabelle"
            };
            if (AutenticazioneWindows)
                builder.IntegratedSecurity = true;
            else
            {
                builder.UserID = Utente ?? string.Empty;
                builder.Password = Password ?? string.Empty;
            }
            return builder.ConnectionString;
        }
    }

    public string Descrizione => $"{Server}/{Database}";
}

public enum ModalitaCopia
{
    /// <summary>Aggiunge le righe a quelle già presenti (crea la tabella se manca).</summary>
    Aggiungi,
    /// <summary>Cancella le righe della destinazione e copia (crea la tabella se manca).</summary>
    Sostituisci,
    /// <summary>Elimina e ricrea la tabella di destinazione, poi copia.</summary>
    Ricrea
}

public record TabellaSorgente(string Schema, string Nome, long Righe)
{
    public string NomeCompleto => $"{Schema}.{Nome}";
}

/// <summary>Copia tabelle (struttura delle colonne e dati) da un database SQL Server a un altro.</summary>
public class CopiaTabelleService
{
    private const int DimensioneBlocco = 5000;

    public static async Task<string?> TestAsync(ParametriConnessione parametri, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(parametri.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            return null;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or ArgumentException)
        {
            return ex.Message;
        }
    }

    public async Task<IReadOnlyList<TabellaSorgente>> ElencoTabelleAsync(ParametriConnessione origine, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(origine.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT s.name, t.name, SUM(CASE WHEN p.index_id IN (0, 1) THEN p.rows ELSE 0 END)
            FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            JOIN sys.partitions p ON p.object_id = t.object_id
            WHERE t.is_ms_shipped = 0
            GROUP BY s.name, t.name
            ORDER BY s.name, t.name
            """, connection);

        var tabelle = new List<TabellaSorgente>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            tabelle.Add(new TabellaSorgente(reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
        return tabelle;
    }

    /// <summary>Copia una tabella; restituisce il numero di righe copiate. In caso di errore la destinazione resta invariata.</summary>
    public async Task<long> CopiaAsync(
        ParametriConnessione origine, ParametriConnessione destinazione, TabellaSorgente tabella, ModalitaCopia modalita,
        IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        await using var sorgente = new SqlConnection(origine.ConnectionString);
        await sorgente.OpenAsync(cancellationToken);
        var colonne = await LeggiColonneAsync(sorgente, null, tabella, cancellationToken);
        if (colonne.Count == 0)
            throw new InvalidOperationException($"La tabella {tabella.NomeCompleto} non ha colonne copiabili.");

        await using var dest = new SqlConnection(destinazione.ConnectionString);
        await dest.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await dest.BeginTransactionAsync(cancellationToken);

        var nomeDestinazione = $"{Quota(tabella.Schema)}.{Quota(tabella.Nome)}";
        var esiste = await EsisteAsync(dest, transaction, tabella, cancellationToken);

        if (esiste && modalita == ModalitaCopia.Ricrea)
        {
            await EseguiAsync(dest, transaction, $"DROP TABLE {nomeDestinazione}", cancellationToken);
            esiste = false;
        }
        if (!esiste)
        {
            await EseguiAsync(dest, transaction,
                $"IF SCHEMA_ID(N'{tabella.Schema.Replace("'", "''")}') IS NULL EXEC (N'CREATE SCHEMA {Quota(tabella.Schema).Replace("'", "''")}')",
                cancellationToken);
            await EseguiAsync(dest, transaction, CreaTabella(nomeDestinazione, colonne), cancellationToken);
        }
        else if (modalita == ModalitaCopia.Sostituisci)
        {
            await EseguiAsync(dest, transaction, $"DELETE FROM {nomeDestinazione}", cancellationToken);
        }

        // Si copiano solo le colonne presenti in entrambe le tabelle
        var colonneDestinazione = (await LeggiColonneAsync(dest, transaction, tabella, cancellationToken))
            .Select(c => c.Nome).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var comuni = colonne.Where(c => colonneDestinazione.Contains(c.Nome)).ToList();
        if (comuni.Count == 0)
            throw new InvalidOperationException($"Nessuna colonna in comune tra origine e destinazione per {tabella.NomeCompleto}.");

        await using var select = new SqlCommand(
            $"SELECT {string.Join(", ", comuni.Select(c => Quota(c.Nome)))} FROM {nomeDestinazione}", sorgente) { CommandTimeout = 0 };
        await using var reader = await select.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

        using var bulk = new SqlBulkCopy(dest, SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.TableLock, transaction)
        {
            DestinationTableName = nomeDestinazione,
            BatchSize = DimensioneBlocco,
            BulkCopyTimeout = 0,
            NotifyAfter = DimensioneBlocco,
            EnableStreaming = true
        };
        foreach (var colonna in comuni)
            bulk.ColumnMappings.Add(colonna.Nome, colonna.Nome);
        bulk.SqlRowsCopied += (_, e) => progress?.Report(e.RowsCopied);

        await bulk.WriteToServerAsync(reader, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var copiate = (long)bulk.RowsCopied;
        progress?.Report(copiate);
        return copiate;
    }

    private sealed record Colonna(string Nome, string Tipo, bool Nullable);

    /// <summary>Colonne copiabili (escluse quelle calcolate e rowversion), con il tipo SQL completo.</summary>
    private static async Task<List<Colonna>> LeggiColonneAsync(SqlConnection connection, SqlTransaction? transaction, TabellaSorgente tabella, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            SELECT c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, c.is_nullable
            FROM sys.columns c
            WHERE c.object_id = OBJECT_ID(@Nome, N'U')
              AND c.is_computed = 0
              AND TYPE_NAME(c.system_type_id) NOT IN (N'timestamp', N'rowversion')
            ORDER BY c.column_id
            """, connection, transaction);
        command.Parameters.Add("@Nome", SqlDbType.NVarChar, 300).Value = $"{Quota(tabella.Schema)}.{Quota(tabella.Nome)}";

        var colonne = new List<Colonna>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var tipo = reader.GetString(1);
            var lunghezza = reader.GetInt16(2);
            var precisione = reader.GetByte(3);
            var scala = reader.GetByte(4);
            var tipoCompleto = tipo switch
            {
                "varchar" or "char" or "varbinary" or "binary" => $"{tipo}({(lunghezza == -1 ? "MAX" : lunghezza)})",
                "nvarchar" or "nchar" => $"{tipo}({(lunghezza == -1 ? "MAX" : lunghezza / 2)})",
                "decimal" or "numeric" => $"{tipo}({precisione},{scala})",
                "datetime2" or "time" or "datetimeoffset" => $"{tipo}({scala})",
                _ => tipo
            };
            colonne.Add(new Colonna(reader.GetString(0), tipoCompleto, reader.GetBoolean(5)));
        }
        return colonne;
    }

    private static string CreaTabella(string nome, IEnumerable<Colonna> colonne)
    {
        var sql = new StringBuilder($"CREATE TABLE {nome} (");
        sql.AppendJoin(", ", colonne.Select(c => $"{Quota(c.Nome)} {c.Tipo} {(c.Nullable ? "NULL" : "NOT NULL")}"));
        return sql.Append(')').ToString();
    }

    private static async Task<bool> EsisteAsync(SqlConnection connection, SqlTransaction transaction, TabellaSorgente tabella, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT OBJECT_ID(@Nome, N'U')", connection, transaction);
        command.Parameters.Add("@Nome", SqlDbType.NVarChar, 300).Value = $"{Quota(tabella.Schema)}.{Quota(tabella.Nome)}";
        return await command.ExecuteScalarAsync(cancellationToken) is not (null or DBNull);
    }

    private static async Task EseguiAsync(SqlConnection connection, SqlTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 0 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Quota(string nome) => $"[{nome.Replace("]", "]]")}]";
}
