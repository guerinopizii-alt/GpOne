using System.Text.RegularExpressions;
using Dapper;
using GpOne.Api.Auth;
using Microsoft.Data.SqlClient;

namespace GpOne.Api.Infrastruttura;

/// <summary>
/// All'avvio: crea il database GpOne se manca ed esegue gli script SQL inclusi (cartella Database) non ancora applicati,
/// in ordine di nome. Gli script applicati sono registrati in dbo.VersioniSchema.
/// </summary>
public static partial class DatabaseSetup
{
    private const string PrefissoRisorse = "GpOne.Database.";

    [GeneratedRegex(@"^\s*GO\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex SeparatoreBatch();

    public static async Task EseguiAsync(GpOneDatabase database, ILogger logger, CancellationToken cancellationToken = default)
    {
        var builder = new SqlConnectionStringBuilder(database.ConnectionString);
        var nomeDatabase = builder.InitialCatalog;

        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync(cancellationToken);
            var creato = await master.ExecuteScalarAsync<int>("""
                IF DB_ID(@Nome) IS NULL
                BEGIN
                    DECLARE @sql NVARCHAR(MAX) = N'CREATE DATABASE ' + QUOTENAME(@Nome);
                    EXEC (@sql);
                    SELECT 1;
                END
                ELSE SELECT 0;
                """, new { Nome = nomeDatabase });
            if (creato == 1)
                logger.LogInformation("Database {Database} creato.", nomeDatabase);
        }

        await using var connection = await database.ApriAsync(cancellationToken);
        await connection.ExecuteAsync("""
            IF OBJECT_ID(N'dbo.VersioniSchema', N'U') IS NULL
                CREATE TABLE dbo.VersioniSchema
                (
                    Script      NVARCHAR(200)   NOT NULL CONSTRAINT PK_VersioniSchema PRIMARY KEY,
                    ApplicatoIl DATETIME2(0)    NOT NULL CONSTRAINT DF_VersioniSchema_ApplicatoIl DEFAULT (SYSUTCDATETIME())
                );
            """);
        var applicati = (await connection.QueryAsync<string>("SELECT Script FROM dbo.VersioniSchema"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var assembly = typeof(DatabaseSetup).Assembly;
        var scripts = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(PrefissoRisorse, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Select(n => (Nome: n[PrefissoRisorse.Length..], Risorsa: n))
            .Where(s => !applicati.Contains(s.Nome))
            .OrderBy(s => s.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var (nome, risorsa) in scripts)
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(risorsa)!);
            var testo = await reader.ReadToEndAsync(cancellationToken);

            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            foreach (var batch in SeparatoreBatch().Split(testo).Where(b => !string.IsNullOrWhiteSpace(b)))
                await connection.ExecuteAsync(new CommandDefinition(batch, transaction: transaction, commandTimeout: 300, cancellationToken: cancellationToken));
            await connection.ExecuteAsync("INSERT INTO dbo.VersioniSchema (Script) VALUES (@nome)", new { nome }, transaction);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("Script {Script} applicato.", nome);
        }

        // Database vuoto: si crea l'amministratore predefinito admin/admin (da cambiare al primo accesso).
        var adminCreato = await connection.ExecuteAsync("""
            INSERT INTO dbo.Utenti (Username, NomeCompleto, PasswordHash, Amministratore)
            SELECT N'admin', N'Amministratore', @Hash, 1
            WHERE NOT EXISTS (SELECT 1 FROM dbo.Utenti WITH (UPDLOCK, HOLDLOCK))
            """, new { Hash = AuthController.Hasher.HashPassword(null!, "admin") });
        if (adminCreato > 0)
            logger.LogWarning("Creato l'utente amministratore predefinito 'admin' con password 'admin': cambiarla al primo accesso.");
    }
}
