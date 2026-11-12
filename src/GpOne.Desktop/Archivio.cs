using System.Text.Json;
using GpOne.Motore;
using Microsoft.Data.SqlClient;

namespace GpOne.Desktop;

/// <summary>
/// Un database del cliente su cui fare domande: collegamento SQL Server e motore AI.
/// Password e chiave API sono sempre cifrate (<see cref="Segreti"/>).
/// </summary>
public sealed class Profilo : IVoceElenco
{
    private static readonly Color[] Colori = [Tema.Viola, Tema.Rosa, Tema.Arancio, Tema.Ciano, Tema.Verde];

    string IVoceElenco.Titolo => Nome;
    string IVoceElenco.Dettaglio => string.Join(" · ", new[] { Server, Database }.Where(s => !string.IsNullOrWhiteSpace(s)))
                                    + (AutenticazioneWindows ? " · utente Windows" : string.IsNullOrWhiteSpace(Utente) ? "" : $" · {Utente}");
    string IVoceElenco.Icona => "";
    Color IVoceElenco.Colore => Colori[Nome.ToUpperInvariant().Sum(c => (int)c) % Colori.Length];

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nome { get; set; } = "";

    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public bool AutenticazioneWindows { get; set; } = true;
    public string? Utente { get; set; }
    public string? PasswordCifrata { get; set; }
    /// <summary>Accetta il certificato del server senza verificarlo (tipico dei SQL Server interni con certificato autofirmato).</summary>
    public bool CertificatoAttendibile { get; set; }

    /// <summary>Configurazione AI; la chiave è in <see cref="ApiKeyCifrata"/>, mai in <see cref="ConfigurazioneAi.ApiKey"/>.</summary>
    public ConfigurazioneAi Ai { get; set; } = new();
    public string? ApiKeyCifrata { get; set; }

    public override string ToString() => $"{Nome}  ({Server} › {Database})";

    /// <summary>Stringa di connessione in sola lettura, con la password decifrata (solo in memoria).</summary>
    public string StringaConnessione()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = Database,
            ApplicationName = "GpOne Desktop",
            ApplicationIntent = ApplicationIntent.ReadOnly,
            TrustServerCertificate = CertificatoAttendibile,
            ConnectTimeout = 15
        };
        if (AutenticazioneWindows)
            builder.IntegratedSecurity = true;
        else
        {
            builder.UserID = Utente ?? "";
            builder.Password = Segreti.Decifra(PasswordCifrata) ?? "";
        }
        return builder.ConnectionString;
    }

    /// <summary>Configurazione AI con la chiave decifrata, da passare al generatore.</summary>
    public ConfigurazioneAi AiConChiave()
    {
        var c = JsonSerializer.Deserialize<ConfigurazioneAi>(JsonSerializer.Serialize(Ai))!;
        c.ApiKey = Segreti.Decifra(ApiKeyCifrata);
        c.ApiKeyImpostata = c.ApiKey is not null;
        return c;
    }
}

/// <summary>Una domanda fatta da questo PC: testo, SQL, spiegazione, esito. Mai i risultati.</summary>
public sealed class DomandaSalvata : IVoceElenco
{
    string IVoceElenco.Titolo => Testo;

    string IVoceElenco.Dettaglio => $"{DataOra:dd/MM HH:mm} · " + (
        Valutazione == Valutazioni.Corretta ? "Confermata"
        : Esito == Esiti.Chiarimento ? "Chiarimento"
        : Esito != Esiti.Ok ? "Non riuscita"
        : $"{Righe ?? 0:N0} righe");

    string IVoceElenco.Icona =>
        Valutazione == Valutazioni.Corretta ? Tema.Icona.Conferma
        : Esito == Esiti.Chiarimento ? Tema.Icona.Domanda
        : Esito != Esiti.Ok ? Tema.Icona.Attenzione
        : Tema.Icona.Storico;

    Color IVoceElenco.Colore =>
        Valutazione == Valutazioni.Corretta ? Tema.Verde
        : Esito == Esiti.Chiarimento ? Tema.Ciano
        : Esito != Esiti.Ok ? Tema.Rosa
        : Tema.Viola;

    public long Id { get; set; }
    public DateTime DataOra { get; set; }
    public string Testo { get; set; } = "";
    public string? Sql { get; set; }
    public string? Spiegazione { get; set; }
    public GraficoProposto? Grafico { get; set; }
    public string Esito { get; set; } = "";
    public string? Errore { get; set; }
    public int? Righe { get; set; }
    public int DurataMs { get; set; }
    /// <summary>"Corretta" (esempio per l'AI), "Sbagliata" o null.</summary>
    public string? Valutazione { get; set; }
    public string? CommentoValutazione { get; set; }
    /// <summary>Domanda giudicata sbagliata da cui è nata questa (catena di "riprova").</summary>
    public long? DomandaOrigineId { get; set; }
    /// <summary>Domanda di chiarimento dell'AI per l'utente (esito Chiarimento).</summary>
    public string? Chiarimento { get; set; }
    /// <summary>Per rispondere sono stati mandati all'AI risultati anonimizzati.</summary>
    public bool DatiInviati { get; set; }

    public override string ToString() =>
        $"{DataOra:dd/MM HH:mm}  {(Valutazione == Valutazioni.Corretta ? "✔ " : Esito != Esiti.Ok ? "⚠ " : "")}{Testo}";
}

public static class Valutazioni
{
    public const string Corretta = "Corretta";
    public const string Sbagliata = "Sbagliata";
}

/// <summary>
/// Archivio locale in %LOCALAPPDATA%\GpOne\Desktop: profili.json e, per ogni profilo, una cartella con
/// struttura.json (abilitazioni e descrizioni) e storico.json (domande, senza risultati).
/// </summary>
public static class Archivio
{
    public const int MaxStorico = 2000;

    public static readonly string Cartella = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GpOne", "Desktop");

    private static readonly JsonSerializerOptions Opzioni = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static string FileProfili => Path.Combine(Cartella, "profili.json");
    private static string CartellaProfilo(Guid id) => Path.Combine(Cartella, id.ToString("N"));

    public static List<Profilo> Profili() => Leggi<List<Profilo>>(FileProfili) ?? [];

    public static void SalvaProfili(IEnumerable<Profilo> profili)
    {
        // La chiave in chiaro non deve mai finire su disco
        foreach (var p in profili)
            p.Ai.ApiKey = null;
        Scrivi(FileProfili, profili.ToList());
    }

    public static void EliminaCartellaProfilo(Guid id)
    {
        var cartella = CartellaProfilo(id);
        if (Directory.Exists(cartella))
            Directory.Delete(cartella, recursive: true);
    }

    public static List<OggettoConfigurato> Struttura(Guid profilo) =>
        Leggi<List<OggettoConfigurato>>(Path.Combine(CartellaProfilo(profilo), "struttura.json")) ?? [];

    public static void SalvaStruttura(Guid profilo, List<OggettoConfigurato> oggetti) =>
        Scrivi(Path.Combine(CartellaProfilo(profilo), "struttura.json"), oggetti);

    public static List<DomandaSalvata> Storico(Guid profilo) =>
        Leggi<List<DomandaSalvata>>(Path.Combine(CartellaProfilo(profilo), "storico.json")) ?? [];

    /// <summary>Salva lo storico tenendo le <see cref="MaxStorico"/> domande più recenti (le confermate corrette restano sempre).</summary>
    public static void SalvaStorico(Guid profilo, List<DomandaSalvata> storico)
    {
        if (storico.Count > MaxStorico)
        {
            var tenere = storico.OrderByDescending(d => d.Id).Take(MaxStorico).Select(d => d.Id).ToHashSet();
            storico.RemoveAll(d => !tenere.Contains(d.Id) && d.Valutazione != Valutazioni.Corretta);
        }
        Scrivi(Path.Combine(CartellaProfilo(profilo), "storico.json"), storico);
    }

    private static T? Leggi<T>(string percorso) where T : class
    {
        if (!File.Exists(percorso))
            return null;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(percorso), Opzioni);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"File dell'archivio GpOne non valido: {percorso} ({ex.Message}).", ex);
        }
    }

    /// <summary>Scrittura su file temporaneo e poi sostituzione: un'interruzione non lascia il file a metà.</summary>
    private static void Scrivi<T>(string percorso, T valore)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        var temporaneo = percorso + ".tmp";
        File.WriteAllText(temporaneo, JsonSerializer.Serialize(valore, Opzioni));
        File.Move(temporaneo, percorso, overwrite: true);
    }
}
