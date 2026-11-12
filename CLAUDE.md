# GpOne

Applicazione web che risponde a domande in linguaggio naturale sui dati dei clienti
**senza che l'AI acceda ai dati in chiaro**: l'AI riceve la struttura (tabelle/colonne abilitate e descritte) e la domanda,
genera una o più SELECT collegate da segnaposto `{{NOME}}` (risolti ed eseguiti in locale da `CatenaSql`); i risultati vanno all'utente.
Solo se le query non bastano, e se il cliente/profilo lo consente (`InvioDatiAnonimizzati`), l'AI può ricevere un risultato anonimizzato da `Anonimizzatore`.

## Struttura
- `src/GpOne.Api` – ASP.NET Core .NET 9 (controller, JWT, Dapper, SignalR hub `/hub/agenti`). Script SQL in `Database/`, eseguiti all'avvio.
- `src/GpOne.Core` – contratti API↔agente (`ProtocolloAgente`), `SqlValidator` (ScriptDom) ed `EsecutoreSql` (lettura struttura, SELECT in transazione annullata), condivisi.
- `src/GpOne.Motore` – logica condivisa da API e desktop: `PromptBuilder`, generatori Claude/Gemini/Ollama, `CicloDomanda` (macchina a stati: genera → valida → esegui catena, con chiarimenti, risultati anonimizzati, limite di chiamate), `CatenaSql`, `Anonimizzatore`, `ConfigurazioneAi`, `OggettoConfigurato`.
- `src/GpOne.Desktop` – WinForms .NET 9 autonomo: si collega direttamente al SQL Server del cliente (niente API né agente). Profili, struttura e storico in `%LOCALAPPDATA%\GpOne\Desktop` (JSON), segreti con DPAPI utente. Finestre con il Designer di Visual Studio (`*.Designer.cs`: solo codice serializzabile, logica ed eventi nel `.cs`).
- `src/GpOne.Agente` – servizio Windows installato dal cliente: connessione SignalR in uscita, esegue le SELECT sul DB locale.
- `src/GpOne.Web` – Angular 22 + Angular Material; `ng build` scrive in `src/GpOne.Api/wwwroot`.
- `tests/GpOne.Api.Tests` – xUnit.

## Regole
- Codice, commenti, UI e messaggi in italiano.
- All'AI possono arrivare solo dati anonimizzati, e solo tramite `Anonimizzatore` (testi a token `Valore_NNN`, numeri e date in chiaro, righe limitate): mai righe o valori in chiaro, mai i valori sostituiti ai segnaposto, mai messaggi d'errore non ripuliti (`PromptBuilder.RipulisciErrore`). Con `InvioDatiAnonimizzati` disattivato non arriva nessun dato.
- Ogni SQL generato (ogni passo della catena, prima e dopo la sostituzione dei segnaposto) passa da `SqlValidator` sia nell'API/desktop (con oggetti/colonne abilitati) sia in `EsecutoreSql`. I segnaposto si sostituiscono solo con `CatenaSql.LiteraleSql`, mai concatenando testo.
- Segreti mai in chiaro nei file: `ENC:` con Data Protection (API), DPAPI macchina (agente) o DPAPI utente (desktop).
- Build e test (`dotnet build`, `dotnet test`, `ng build`) si possono eseguire per verificare le modifiche.
