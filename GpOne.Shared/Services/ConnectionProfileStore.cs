using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace GpOne.Services;

/// <summary>Dati di un profilo di connessione esposti all'interfaccia: la password non è mai inclusa.</summary>
public record ProfiloConnessione(
    string Nome,
    string Server,
    string Database,
    string Utente,
    bool Encrypt = true,
    bool TrustServerCertificate = true);

/// <summary>
/// Archivio dei profili di connessione definiti dall'amministratore.
/// Le password sono cifrate con ASP.NET Core Data Protection e decifrate solo per aprire la connessione.
/// </summary>
public class ConnectionProfileStore
{
    private sealed record ProfiloSalvato(
        string Nome,
        string Server,
        string Database,
        string Utente,
        string PasswordCifrata,
        bool Encrypt,
        bool TrustServerCertificate);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string filePath;
    private readonly IDataProtector protector;
    private readonly Lock sync = new();
    private readonly List<ProfiloSalvato> profili;

    public event Action? Changed;

    public ConnectionProfileStore(string filePath, IDataProtectionProvider dataProtectionProvider)
    {
        this.filePath = filePath;
        protector = dataProtectionProvider.CreateProtector("GpOne.ConnectionProfiles.v1");
        profili = File.Exists(filePath)
            ? JsonSerializer.Deserialize<List<ProfiloSalvato>>(File.ReadAllText(filePath)) ?? []
            : [];
    }

    public IReadOnlyList<string> Nomi
    {
        get { lock (sync) return [.. profili.Select(p => p.Nome)]; }
    }

    public IReadOnlyList<ProfiloConnessione> Elenco
    {
        get { lock (sync) return [.. profili.Select(ToProfilo)]; }
    }

    public ProfiloConnessione? Get(string nome)
    {
        lock (sync) return Trova(nome) is { } salvato ? ToProfilo(salvato) : null;
    }

    /// <summary>
    /// Stringa di connessione completa di password: da usare solo per aprire connessioni.
    /// Con <paramref name="database"/> si sostituisce il database del profilo (es. master).
    /// </summary>
    public string? GetConnectionString(string nome, string? database = null)
    {
        ProfiloSalvato? salvato;
        lock (sync) salvato = Trova(nome);
        if (salvato is null)
            return null;

        var profilo = ToProfilo(salvato);
        if (database is not null)
            profilo = profilo with { Database = database };

        return BuildConnectionString(profilo, protector.Unprotect(salvato.PasswordCifrata));
    }

    /// <summary>
    /// Prova la connessione. Se la password è vuota e si modifica un profilo esistente, usa quella salvata.
    /// Restituisce null se la connessione riesce, altrimenti il messaggio d'errore.
    /// </summary>
    public async Task<string?> TestAsync(ProfiloConnessione profilo, string? password, string? nomeOriginale, CancellationToken cancellationToken = default)
    {
        try
        {
            // Test su master: il database del profilo può non esistere ancora (viene creato all'inizializzazione)
            var connectionString = BuildConnectionString(profilo with { Database = "master" }, RisolviPassword(password, nomeOriginale));
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return null;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or ArgumentException)
        {
            return ex.Message;
        }
    }

    /// <summary>Crea o aggiorna un profilo. Con password vuota in modifica, mantiene quella esistente.</summary>
    public void Salva(ProfiloConnessione profilo, string? password, string? nomeOriginale)
    {
        lock (sync)
        {
            var esistente = nomeOriginale is null ? null : Trova(nomeOriginale);
            var omonimo = Trova(profilo.Nome);
            if (omonimo is not null && !ReferenceEquals(omonimo, esistente))
                throw new InvalidOperationException($"Esiste già un profilo con nome '{profilo.Nome}'.");

            var passwordCifrata = !string.IsNullOrEmpty(password)
                ? protector.Protect(password)
                : esistente?.PasswordCifrata ?? throw new InvalidOperationException("Indicare la password.");

            var nuovo = new ProfiloSalvato(
                profilo.Nome.Trim(), profilo.Server.Trim(), profilo.Database.Trim(), profilo.Utente.Trim(),
                passwordCifrata, profilo.Encrypt, profilo.TrustServerCertificate);

            if (esistente is null)
                profili.Add(nuovo);
            else
                profili[profili.IndexOf(esistente)] = nuovo;

            Scrivi();
        }
        Changed?.Invoke();
    }

    public void Elimina(string nome)
    {
        lock (sync)
        {
            var esistente = Trova(nome);
            if (esistente is null)
                return;
            profili.Remove(esistente);
            Scrivi();
        }
        Changed?.Invoke();
    }

    private string RisolviPassword(string? password, string? nomeOriginale)
    {
        if (!string.IsNullOrEmpty(password))
            return password;

        ProfiloSalvato? esistente;
        lock (sync) esistente = nomeOriginale is null ? null : Trova(nomeOriginale);

        return esistente is null
            ? throw new InvalidOperationException("Indicare la password.")
            : protector.Unprotect(esistente.PasswordCifrata);
    }

    private static string BuildConnectionString(ProfiloConnessione profilo, string password) =>
        new SqlConnectionStringBuilder
        {
            DataSource = profilo.Server,
            InitialCatalog = profilo.Database,
            UserID = profilo.Utente,
            Password = password,
            Encrypt = profilo.Encrypt,
            TrustServerCertificate = profilo.TrustServerCertificate,
            ApplicationName = "GpOne"
        }.ConnectionString;

    private ProfiloSalvato? Trova(string nome) =>
        profili.Find(p => string.Equals(p.Nome, nome, StringComparison.OrdinalIgnoreCase));

    private static ProfiloConnessione ToProfilo(ProfiloSalvato p) =>
        new(p.Nome, p.Server, p.Database, p.Utente, p.Encrypt, p.TrustServerCertificate);

    private void Scrivi()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temp = filePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(profili, JsonOptions));
        File.Move(temp, filePath, overwrite: true);
    }
}
