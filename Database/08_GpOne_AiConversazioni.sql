/*
    GpOne - Storico delle conversazioni con GpOne AI (ogni utente vede solo le proprie)
    Script idempotente: può essere rieseguito senza errori.
*/

IF OBJECT_ID(N'dbo.AiConversazioni', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiConversazioni
    (
        Id              INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_AiConversazioni PRIMARY KEY,
        UtenteId        INT                 NOT NULL,
        Titolo          NVARCHAR(200)       NOT NULL,
        -- Motore AI in uso alla creazione (descrizione leggibile)
        Motore          NVARCHAR(200)       NULL,
        DataCreazione   DATETIME2(0)        NOT NULL CONSTRAINT DF_AiConversazioni_DataCreazione DEFAULT (SYSUTCDATETIME()),
        DataModifica    DATETIME2(0)        NOT NULL CONSTRAINT DF_AiConversazioni_DataModifica DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_AiConversazioni_Utenti FOREIGN KEY (UtenteId) REFERENCES dbo.Utenti (Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_AiConversazioni_UtenteId ON dbo.AiConversazioni (UtenteId, DataModifica DESC);
END
GO

IF OBJECT_ID(N'dbo.AiMessaggi', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiMessaggi
    (
        Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AiMessaggi PRIMARY KEY,
        ConversazioneId INT                 NOT NULL,
        -- User / Assistant
        Ruolo           NVARCHAR(20)        NOT NULL,
        Testo           NVARCHAR(MAX)       NOT NULL,
        -- Dati letti dal database e mostrati all'utente insieme alla risposta
        Dettagli        NVARCHAR(MAX)       NULL,
        DataOra         DATETIME2(0)        NOT NULL,
        CONSTRAINT FK_AiMessaggi_AiConversazioni FOREIGN KEY (ConversazioneId) REFERENCES dbo.AiConversazioni (Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_AiMessaggi_ConversazioneId ON dbo.AiMessaggi (ConversazioneId, Id);
END
GO
