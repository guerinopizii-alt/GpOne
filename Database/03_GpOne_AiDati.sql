/*
    GpOne - Oggetti del database consultabili dall'assistente AI
    L'amministratore li abilita da Sistema > Dati per AI; l'AI può generare solo SELECT su questi oggetti.
    Script idempotente: può essere rieseguito senza errori.
*/

IF OBJECT_ID(N'dbo.AiOggettiConsentiti', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiOggettiConsentiti
    (
        Id              INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_AiOggettiConsentiti PRIMARY KEY,
        SchemaNome      SYSNAME             NOT NULL,
        OggettoNome     SYSNAME             NOT NULL,
        -- Descrizione per l'AI: cosa contiene la tabella/vista e come interpretarne i campi
        Descrizione     NVARCHAR(1000)      NULL,
        DataCreazione   DATETIME2(0)        NOT NULL CONSTRAINT DF_AiOggettiConsentiti_DataCreazione DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_AiOggettiConsentiti UNIQUE (SchemaNome, OggettoNome)
    );
END
GO
