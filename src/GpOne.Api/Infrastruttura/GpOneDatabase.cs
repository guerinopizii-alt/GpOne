using Microsoft.Data.SqlClient;

namespace GpOne.Api.Infrastruttura;

/// <summary>
/// Connessione al database applicativo GpOne: appsettings.json, ConnectionStrings:GpOne.
/// La password può essere cifrata: Password=ENC:... (si ottiene con GpOne.Api.exe --cifra-password &lt;password&gt;).
/// </summary>
public class GpOneDatabase(IConfiguration configuration, Cifratura cifratura)
{
    public const string NomeConnessione = "GpOne";

    public string ConnectionString
    {
        get
        {
            var valore = configuration.GetConnectionString(NomeConnessione);
            if (string.IsNullOrWhiteSpace(valore))
                throw new InvalidOperationException($"Stringa di connessione 'ConnectionStrings:{NomeConnessione}' non definita in appsettings.json.");

            var builder = new SqlConnectionStringBuilder(valore);
            builder.Password = cifratura.Decifra(builder.Password);
            if (builder.ApplicationName == new SqlConnectionStringBuilder().ApplicationName)
                builder.ApplicationName = "GpOne";
            return builder.ConnectionString;
        }
    }

    /// <summary>Server, database e utente per il log (mai la password); indica se la password è in chiaro.</summary>
    public (string Descrizione, bool PasswordInChiaro) Descrizione()
    {
        var valore = configuration.GetConnectionString(NomeConnessione);
        if (string.IsNullOrWhiteSpace(valore))
            return ("(stringa di connessione non definita)", false);

        var builder = new SqlConnectionStringBuilder(valore);
        if (builder.IntegratedSecurity)
            return ($"server '{builder.DataSource}', database '{builder.InitialCatalog}', autenticazione Windows", false);

        var cifrata = builder.Password.StartsWith(Cifratura.Prefisso, StringComparison.Ordinal);
        return ($"server '{builder.DataSource}', database '{builder.InitialCatalog}', utente '{builder.UserID}', password {(cifrata ? "cifrata" : "in chiaro")}",
            !cifrata && builder.Password.Length > 0);
    }

    public async Task<SqlConnection> ApriAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
