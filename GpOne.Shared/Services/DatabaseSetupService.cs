using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace GpOne.Services;

public record StatoScript(string Nome, DateTime? ApplicatoIl)
{
    public bool Applicato => ApplicatoIl is not null;
}

/// <summary>
/// Crea il database del profilo (se manca) ed esegue gli script SQL inclusi nell'assembly (cartella Database),
/// in ordine di nome. Gli script applicati vengono registrati in dbo.VersioniSchema e non vengono rieseguiti.
/// </summary>
public partial class DatabaseSetupService(ConnectionProfileStore store)
{
    private const string PrefissoRisorse = "GpOne.Database.";

    private static readonly Lazy<IReadOnlyList<(string Nome, string Testo)>> Scripts = new(CaricaScripts);

    [GeneratedRegex(@"^\s*GO\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex SeparatoreBatch();

    public async Task<IReadOnlyList<StatoScript>> StatoAsync(string profilo, CancellationToken cancellationToken = default)
    {
        var applicati = await ScriptApplicatiAsync(profilo, cancellationToken);
        return [.. Scripts.Value.Select(s => new StatoScript(s.Nome, applicati.TryGetValue(s.Nome, out var data) ? data : null))];
    }

    /// <summary>Esegue gli script non ancora applicati; restituisce i nomi di quelli eseguiti.</summary>
    public async Task<IReadOnlyList<string>> EseguiAsync(string profilo, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var database = store.Get(profilo)?.Database
            ?? throw new InvalidOperationException($"Profilo di connessione '{profilo}' non configurato.");

        await CreaDatabaseSeMancaAsync(profilo, database, progress, cancellationToken);

        await using var connection = new SqlConnection(store.GetConnectionString(profilo));
        await connection.OpenAsync(cancellationToken);

        await using (var command = new SqlCommand("""
            IF OBJECT_ID(N'dbo.VersioniSchema', N'U') IS NULL
                CREATE TABLE dbo.VersioniSchema
                (
                    Script          NVARCHAR(200)   NOT NULL CONSTRAINT PK_VersioniSchema PRIMARY KEY,
                    ApplicatoIl     DATETIME2(0)    NOT NULL CONSTRAINT DF_VersioniSchema_ApplicatoIl DEFAULT (SYSUTCDATETIME())
                );
            """, connection))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var applicati = await LeggiApplicatiAsync(connection, cancellationToken);
        var eseguiti = new List<string>();

        foreach (var (nome, testo) in Scripts.Value.Where(s => !applicati.ContainsKey(s.Nome)))
        {
            progress?.Report($"Esecuzione {nome}…");

            // Ogni script in una transazione: o viene applicato per intero o per niente
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            foreach (var batch in SeparatoreBatch().Split(testo).Where(b => !string.IsNullOrWhiteSpace(b)))
            {
                await using var command = new SqlCommand(batch, connection, transaction) { CommandTimeout = 300 };
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var command = new SqlCommand("INSERT INTO dbo.VersioniSchema (Script) VALUES (@Script)", connection, transaction))
            {
                command.Parameters.Add("@Script", SqlDbType.NVarChar, 200).Value = nome;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            eseguiti.Add(nome);
        }

        progress?.Report(eseguiti.Count == 0 ? "Database già aggiornato." : $"Script eseguiti: {eseguiti.Count}.");
        return eseguiti;
    }

    private async Task CreaDatabaseSeMancaAsync(string profilo, string database, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(store.GetConnectionString(profilo, "master"));
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("""
            IF DB_ID(@Database) IS NULL
            BEGIN
                DECLARE @sql NVARCHAR(MAX) = N'CREATE DATABASE ' + QUOTENAME(@Database);
                EXEC (@sql);
                SELECT 1;
            END
            ELSE
                SELECT 0;
            """, connection);
        command.Parameters.Add("@Database", SqlDbType.NVarChar, 128).Value = database;

        if ((int)(await command.ExecuteScalarAsync(cancellationToken))! == 1)
            progress?.Report($"Database '{database}' creato.");
    }

    private async Task<Dictionary<string, DateTime>> ScriptApplicatiAsync(string profilo, CancellationToken cancellationToken)
    {
        var database = store.Get(profilo)?.Database
            ?? throw new InvalidOperationException($"Profilo di connessione '{profilo}' non configurato.");

        await using (var master = new SqlConnection(store.GetConnectionString(profilo, "master")))
        {
            await master.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT DB_ID(@Database)", master);
            command.Parameters.Add("@Database", SqlDbType.NVarChar, 128).Value = database;
            if (await command.ExecuteScalarAsync(cancellationToken) is DBNull or null)
                return [];
        }

        await using var connection = new SqlConnection(store.GetConnectionString(profilo));
        await connection.OpenAsync(cancellationToken);
        return await LeggiApplicatiAsync(connection, cancellationToken);
    }

    private static async Task<Dictionary<string, DateTime>> LeggiApplicatiAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var applicati = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        await using var command = new SqlCommand("""
            IF OBJECT_ID(N'dbo.VersioniSchema', N'U') IS NOT NULL
                SELECT Script, ApplicatoIl FROM dbo.VersioniSchema;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            applicati[reader.GetString(0)] = DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc);

        return applicati;
    }

    private static IReadOnlyList<(string Nome, string Testo)> CaricaScripts()
    {
        var assembly = typeof(DatabaseSetupService).Assembly;
        return
        [
            .. assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(PrefissoRisorse, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(n =>
                {
                    using var reader = new StreamReader(assembly.GetManifestResourceStream(n)!);
                    return (n[PrefissoRisorse.Length..], reader.ReadToEnd());
                })
        ];
    }
}
