/*
    GpOne - Configurazioni AI nel database e abilitazione degli utenti
    Ogni utente può usare GpOne AI solo se ha una configurazione assegnata (Utenti.AiConfigurazioneId).
    La chiave API è salvata cifrata dall'applicazione (ASP.NET Core Data Protection).
    Script idempotente: può essere rieseguito senza errori.
*/

IF OBJECT_ID(N'dbo.AiConfigurazioni', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiConfigurazioni
    (
        Id                  INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_AiConfigurazioni PRIMARY KEY,
        Nome                NVARCHAR(100)       NOT NULL,
        -- Proposta come predefinita quando si abilita un utente
        Predefinita         BIT                 NOT NULL CONSTRAINT DF_AiConfigurazioni_Predefinita DEFAULT (0),
        Provider            NVARCHAR(50)        NOT NULL,
        Endpoint            NVARCHAR(500)       NULL,
        Modello             NVARCHAR(200)       NULL,
        SystemPrompt        NVARCHAR(MAX)       NULL,
        ApiKeyCifrata       NVARCHAR(MAX)       NULL,
        TenantId            NVARCHAR(100)       NULL,
        ClientId            NVARCHAR(100)       NULL,
        DatiLiberi          BIT                 NOT NULL CONSTRAINT DF_AiConfigurazioni_DatiLiberi DEFAULT (0),
        Regione             NVARCHAR(50)        NULL,
        ProgettoId          NVARCHAR(200)       NULL,
        MaxCaratteriDati    INT                 NOT NULL CONSTRAINT DF_AiConfigurazioni_MaxCaratteriDati DEFAULT (60000),
        MaxTokenMotore      INT                 NOT NULL CONSTRAINT DF_AiConfigurazioni_MaxTokenMotore DEFAULT (0),
        DataCreazione       DATETIME2(0)        NOT NULL CONSTRAINT DF_AiConfigurazioni_DataCreazione DEFAULT (SYSUTCDATETIME()),
        DataModifica        DATETIME2(0)        NULL,
        CONSTRAINT UQ_AiConfigurazioni_Nome UNIQUE (Nome)
    );
END
GO

IF COL_LENGTH(N'dbo.Utenti', N'AiConfigurazioneId') IS NULL
    ALTER TABLE dbo.Utenti ADD AiConfigurazioneId INT NULL
        CONSTRAINT FK_Utenti_AiConfigurazioni REFERENCES dbo.AiConfigurazioni (Id) ON DELETE SET NULL;
GO
