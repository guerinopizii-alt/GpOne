/*
    GpOne - Catena di query con segnaposto, chiarimenti e risultati anonimizzati
    - Domande.SqlGenerato può contenere più passi ("-- passo: NOME" prima di ogni SELECT, segnaposto {{NOME}}).
    - Domande.Chiarimento: domanda dell'AI per l'utente (esito 'Chiarimento').
    - Domande.DatiInviati: per rispondere sono stati mandati all'AI risultati anonimizzati (mai valori in chiaro).
    - ConfigurazioniAi: limite di chiamate all'AI per domanda e invio di risultati anonimizzati (consentito di default).
*/

IF COL_LENGTH(N'dbo.Domande', N'Chiarimento') IS NULL
    ALTER TABLE dbo.Domande ADD
        Chiarimento     NVARCHAR(2000)  NULL,
        DatiInviati     BIT             NOT NULL CONSTRAINT DF_Domande_DatiInviati DEFAULT (0);
GO

IF COL_LENGTH(N'dbo.ConfigurazioniAi', N'InvioDatiAnonimizzati') IS NULL
    ALTER TABLE dbo.ConfigurazioniAi ADD
        MaxChiamateAi           INT NOT NULL CONSTRAINT DF_ConfigurazioniAi_MaxChiamate DEFAULT (5),
        InvioDatiAnonimizzati   BIT NOT NULL CONSTRAINT DF_ConfigurazioniAi_Anonimizzati DEFAULT (1),
        MaxRigheAnonimizzate    INT NOT NULL CONSTRAINT DF_ConfigurazioniAi_RigheAnonimizzate DEFAULT (50);
GO
