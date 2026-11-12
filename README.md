# GpOne

Domande in italiano sui dati di un cliente; l'AI vede solo la struttura del database, mai i dati.

```
Browser (Angular) ──► GpOne.Api ──► AI (Claude/Ollama): struttura + domanda → SELECT
                          │  ▲
                 SignalR  ▼  │ risultati (non salvati, mai all'AI)
                     GpOne.Agente (rete del cliente) ──► SQL Server del cliente
```

## Avvio in sviluppo

1. **API** – in `src/GpOne.Api/appsettings.json` imposta `ConnectionStrings:GpOne`. Per cifrare la password:
   esegui `src/GpOne.Api/CifraPassword.bat`, scrivi la password e incolla il valore `ENC:...` di `password-cifrata.txt`
   dopo `Password=` (poi cancella il file).
   Avvia con `dotnet run --project src/GpOne.Api --launch-profile https` (crea il DB e le tabelle se mancano).
2. **Frontend** – `cd src/GpOne.Web && npm start`, poi apri http://localhost:4200 (proxy verso l'API).
   Al primo accesso si crea l'utente amministratore.
3. **Cliente** – in *Clienti*: nuovo cliente, scegli il motore AI (Claude con chiave API, oppure Ollama) e genera la chiave dell'agente.
4. **Agente** (presso il cliente) – in `src/GpOne.Agente/appsettings.json`: `GpOneAgente` (ServerUrl, ClienteId, Chiave) e
   `ConnectionStrings:Cliente` (utente SQL consigliato con solo `db_datareader`). Chiave e password si cifrano con
   `CifraPassword.bat` (nella cartella dell'agente, sul computer dell'agente). Installazione come servizio:
   `sc.exe create "GpOne Agente" binPath= "C:\GpOne.Agente\GpOne.Agente.exe" start= auto`.
5. **Struttura** – in *Clienti* › icona schema: *Rileggi dal database*, abilita tabelle/colonne e scrivi le descrizioni.
6. **Chiedi** – scegli il cliente e fai le domande: tabella, grafico, SQL, export Excel/CSV.

## Desktop autonomo (GpOne.Desktop)
Applicazione Windows (WinForms) per chi lavora nella rete del cliente: si collega **direttamente** al SQL Server,
senza API né agente. La logica è la stessa del web (`GpOne.Motore`): all'AI vanno solo la struttura abilitata e la domanda.

```
GpOne.Desktop ──► AI (Claude/Ollama): struttura + domanda → SELECT
      │
      ▼ SELECT validata, in transazione annullata (risultati solo a video, mai all'AI)
SQL Server del cliente
```

1. Avvia `dotnet run --project src/GpOne.Desktop` (o `GpOne.Desktop.exe` dopo `dotnet publish -c Release`).
2. **Nuovo profilo**: server, database, autenticazione (Windows o utente SQL, consigliato il solo `db_datareader`),
   *Prova collegamento*; motore AI e righe massime/timeout. Motori: Claude (chiave API), Gemini (chiave di Google AI Studio),
   Gemini su Vertex AI (file JSON di un account di servizio con ruolo *Vertex AI User* e regione, es. `europe-west8`), Ollama.
3. **Struttura del database**: *Rileggi dal database*, abilita tabelle/colonne e scrivi le descrizioni.
4. Fai le domande: tabella, grafico, SQL, export Excel/CSV; conferma le risposte giuste (diventano esempi) o correggi quelle sbagliate.

Profili, struttura e storico (solo domande e SQL, mai risultati) sono in `%LOCALAPPDATA%\GpOne\Desktop`.
Password SQL e chiave API sono cifrate con DPAPI per l'utente Windows: un profilo copiato su un altro PC o utente
richiede di reinserirle. Utenti, permessi e storico non sono condivisi con il web.

## Log
API e agente scrivono un log giornaliero nella cartella `Logs` accanto all'eseguibile (in sviluppo
`bin\Debug\net9.0\Logs` per l'API, `bin\Debug\net9.0-windows\Logs` per l'agente), conservato 30 giorni.
All'avvio controllano la configurazione e scrivono messaggi chiari: password non decifrabile, utente/password SQL errati,
server non raggiungibile, chiave dell'agente rifiutata, utente SQL con permessi più ampi della lettura.
Se il database GpOne non è utilizzabile l'API non parte; l'agente si ferma solo se manca la sua configurazione.

## Produzione
`cd src/GpOne.Web && npx ng build` (output in `src/GpOne.Api/wwwroot`), poi `dotnet publish src/GpOne.Api -c Release`.
Imposta `GpOne:CartellaDati` su una cartella fuori dal sito: contiene le chiavi di cifratura e la chiave JWT
(senza di esse password e chiavi API cifrate non sono più leggibili).
