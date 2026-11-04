/*
    GpOne - Domande preparate per l'assistente AI, con le tabelle e i dati da inviare
    Script idempotente: può essere rieseguito senza errori.
*/

IF OBJECT_ID(N'dbo.AiDomande', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiDomande
    (
        Id              INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_AiDomande PRIMARY KEY,
        Titolo          NVARCHAR(200)       NOT NULL,
        Testo           NVARCHAR(MAX)       NOT NULL,
        Attiva          BIT                 NOT NULL CONSTRAINT DF_AiDomande_Attiva DEFAULT (1),
        DataCreazione   DATETIME2(0)        NOT NULL CONSTRAINT DF_AiDomande_DataCreazione DEFAULT (SYSUTCDATETIME()),
        DataModifica    DATETIME2(0)        NULL
    );
END
GO

IF OBJECT_ID(N'dbo.AiDomandeFonti', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiDomandeFonti
    (
        Id              INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_AiDomandeFonti PRIMARY KEY,
        DomandaId       INT                 NOT NULL,
        Ordine          INT                 NOT NULL CONSTRAINT DF_AiDomandeFonti_Ordine DEFAULT (0),
        SchemaNome      SYSNAME             NOT NULL,
        OggettoNome     SYSNAME             NOT NULL,
        -- Colonne separate da virgola; NULL = tutte
        Colonne         NVARCHAR(MAX)       NULL,
        -- Condizione WHERE (senza la parola WHERE) e ORDER BY (senza le parole ORDER BY)
        Filtro          NVARCHAR(MAX)       NULL,
        Ordinamento     NVARCHAR(500)       NULL,
        MaxRighe        INT                 NOT NULL CONSTRAINT DF_AiDomandeFonti_MaxRighe DEFAULT (200),
        CONSTRAINT FK_AiDomandeFonti_AiDomande FOREIGN KEY (DomandaId) REFERENCES dbo.AiDomande (Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_AiDomandeFonti_DomandaId ON dbo.AiDomandeFonti (DomandaId, Ordine);
END
GO
