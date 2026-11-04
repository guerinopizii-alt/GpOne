using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace GpOne.Services;

/// <summary>Configurazione delle dashboard fornita dall'host web; assente nell'app desktop.</summary>
/// <param name="Endpoint">API in sola visualizzazione.</param>
/// <param name="EndpointProgettazione">API del designer (solo amministratori).</param>
/// <param name="Cartella">Cartella dei file delle dashboard (XML).</param>
public record DashboardOpzioni(string Endpoint, string EndpointProgettazione, string Cartella)
{
    public const string Intestazione = "X-GpOne-Dashboard";

    public record Voce(string Id, string Titolo);

    /// <summary>Dashboard salvate: l'Id è il nome del file, il titolo quello indicato nella dashboard.</summary>
    public IReadOnlyList<Voce> Elenco()
    {
        if (!Directory.Exists(Cartella))
            return [];
        return
        [
            .. Directory.EnumerateFiles(Cartella, "*.xml")
                .Select(file =>
                {
                    var id = Path.GetFileNameWithoutExtension(file);
                    try
                    {
                        var titolo = XDocument.Load(file).Root?.Element("Title")?.Attribute("Text")?.Value;
                        return new Voce(id, string.IsNullOrWhiteSpace(titolo) ? id : titolo);
                    }
                    catch (System.Xml.XmlException)
                    {
                        return new Voce(id, id);
                    }
                })
                .OrderBy(v => v.Titolo, StringComparer.CurrentCultureIgnoreCase)
        ];
    }
}

/// <summary>
/// Codici temporanei che autorizzano le chiamate HTTP delle dashboard: le API DevExpress non conoscono la sessione
/// Blazor, quindi ogni pagina aperta da un utente collegato ottiene un codice da inviare nelle intestazioni.
/// </summary>
public class DashboardAccessi
{
    private static readonly TimeSpan Durata = TimeSpan.FromHours(12);

    private readonly ConcurrentDictionary<string, (bool Amministratore, DateTime Scadenza)> codici = new();

    public string Crea(bool amministratore)
    {
        // Pulizia dei codici scaduti
        foreach (var (codice, voce) in codici)
            if (voce.Scadenza < DateTime.UtcNow)
                codici.TryRemove(codice, out _);

        var nuovo = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        codici[nuovo] = (amministratore, DateTime.UtcNow.Add(Durata));
        return nuovo;
    }

    /// <summary>Verifica il codice; <paramref name="amministratore"/> indica se può modificare le dashboard.</summary>
    public bool Valido(string? codice, out bool amministratore)
    {
        amministratore = false;
        if (codice is null || !codici.TryGetValue(codice, out var voce) || voce.Scadenza < DateTime.UtcNow)
            return false;
        amministratore = voce.Amministratore;
        return true;
    }

    public void Revoca(string? codice)
    {
        if (codice is not null)
            codici.TryRemove(codice, out _);
    }
}
