namespace GpOne.Core;

/// <summary>
/// Contratto tra l'API GpOne e l'agente installato dal cliente (SignalR, connessione aperta dall'agente).
/// L'API invoca sull'agente <see cref="MetodoLeggiSchema"/> ed <see cref="MetodoEseguiQuery"/> e ne attende il risultato.
/// </summary>
public static class ProtocolloAgente
{
    public const string PercorsoHub = "/hub/agenti";
    public const string HeaderCliente = "X-Agente-Cliente";
    public const string HeaderChiave = "X-Agente-Chiave";
    public const string HeaderVersione = "X-Agente-Versione";

    public const string MetodoLeggiSchema = "LeggiSchema";
    public const string MetodoEseguiQuery = "EseguiQuery";
}

/// <summary>Tabella o vista del DB cliente: solo struttura, mai dati.</summary>
public record OggettoSchema(string Schema, string Nome, string Tipo, IReadOnlyList<ColonnaSchema> Colonne);

public record ColonnaSchema(string Nome, string TipoDati, int Ordine);

public record RichiestaQuery(string Sql, int MaxRighe, int TimeoutSecondi);

public record ColonnaRisultato(string Nome, string Tipo);

/// <summary>Risultato di una SELECT eseguita dall'agente; <see cref="Troncato"/> se le righe superavano MaxRighe.</summary>
public record RisultatoQuery(
    IReadOnlyList<ColonnaRisultato> Colonne,
    IReadOnlyList<object?[]> Righe,
    bool Troncato,
    int DurataMs,
    string? Errore = null);
