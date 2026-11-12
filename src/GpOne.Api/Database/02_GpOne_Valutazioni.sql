/*
    GpOne - Valutazione delle risposte da parte dell'utente
    - Valutazione 'Corretta': la coppia domanda/SQL diventa un esempio per l'AI (solo testo della domanda e SQL, mai risultati).
    - Valutazione 'Sbagliata' con commento: si chiede all'AI una nuova query; la nuova domanda punta all'originale (DomandaOrigineId).
*/

IF COL_LENGTH(N'dbo.Domande', N'Valutazione') IS NULL
    ALTER TABLE dbo.Domande ADD
        Valutazione             NVARCHAR(20)    NULL,   -- Corretta, Sbagliata
        CommentoValutazione     NVARCHAR(1000)  NULL,
        DomandaOrigineId        BIGINT          NULL
            CONSTRAINT FK_Domande_Origine FOREIGN KEY REFERENCES dbo.Domande (Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Domande_Esempi' AND object_id = OBJECT_ID(N'dbo.Domande'))
    CREATE INDEX IX_Domande_Esempi ON dbo.Domande (ClienteId, Valutazione, DataOra DESC);
GO
