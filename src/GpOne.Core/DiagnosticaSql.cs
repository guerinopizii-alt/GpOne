namespace GpOne.Core;

/// <summary>
/// Traduce gli errori di connessione a SQL Server in un suggerimento comprensibile per chi legge il log
/// (password errata, server non raggiungibile, database inesistente, certificato...).
/// Riceve numero e messaggio dell'errore per non dipendere da Microsoft.Data.SqlClient.
/// </summary>
public static class DiagnosticaSql
{
    public static string Suggerimento(int numero, string messaggio)
    {
        if (messaggio.Contains("certificate", StringComparison.OrdinalIgnoreCase) || messaggio.Contains("certificato", StringComparison.OrdinalIgnoreCase))
            return "Il certificato del server SQL non è considerato attendibile: aggiungere 'Trust Server Certificate=True' alla stringa di connessione o installare un certificato valido.";

        return numero switch
        {
            18456 => "Accesso negato: utente o password errati, oppure l'autenticazione SQL Server non è abilitata sul server (modalità mista).",
            18470 => "L'utente SQL è disabilitato.",
            18487 or 18488 => "La password dell'utente SQL è scaduta o deve essere cambiata.",
            18452 => "Accesso con autenticazione Windows non riuscito: l'account non è attendibile per il server. Usare un utente SQL o un account di dominio.",
            4060 => "Il database indicato in 'Initial Catalog' non esiste o l'utente non ha accesso a quel database.",
            53 or -1 or 2 or 40 or 26 => "Server SQL non raggiungibile: controllare il nome del server/istanza in 'Data Source', che il servizio SQL Server e SQL Browser siano avviati, che TCP/IP sia abilitato e che il firewall lo consenta.",
            -2 or 258 => "Tempo scaduto nel collegamento al server SQL: il server è lento o non raggiungibile.",
            229 or 262 or 300 => "L'utente SQL non ha i permessi necessari.",
            _ => "Controllare la stringa di connessione e il messaggio di errore."
        };
    }
}
