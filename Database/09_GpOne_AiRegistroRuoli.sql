/*
    GpOne - Registro degli invii all'AI e visibilità delle domande preparate per ruolo
    Script idempotente: può essere rieseguito senza errori.
*/

IF OBJECT_ID(N'dbo.AiLogInvii', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiLogInvii
    (
        Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AiLogInvii PRIMARY KEY,
        UtenteId        INT                 NULL,
        Username        NVARCHAR(100)       NOT NULL,
        Motore          NVARCHAR(200)       NOT NULL,
        Domanda         NVARCHAR(MAX)       NOT NULL,
        -- Query eseguite per leggere i dati inviati (NULL se non sono stati inviati dati)
        Query           NVARCHAR(MAX)       NULL,
        Caratteri       INT                 NOT NULL,
        DataOra         DATETIME2(0)        NOT NULL CONSTRAINT DF_AiLogInvii_DataOra DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_AiLogInvii_Utenti FOREIGN KEY (UtenteId) REFERENCES dbo.Utenti (Id) ON DELETE SET NULL
    );

    CREATE INDEX IX_AiLogInvii_DataOra ON dbo.AiLogInvii (DataOra DESC);
END
GO

-- Ruoli che vedono una domanda preparata; nessuna riga = visibile a tutti
IF OBJECT_ID(N'dbo.AiDomandeRuoli', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiDomandeRuoli
    (
        DomandaId       INT NOT NULL,
        RuoloId         INT NOT NULL,
        CONSTRAINT PK_AiDomandeRuoli PRIMARY KEY (DomandaId, RuoloId),
        CONSTRAINT FK_AiDomandeRuoli_AiDomande FOREIGN KEY (DomandaId) REFERENCES dbo.AiDomande (Id) ON DELETE CASCADE,
        CONSTRAINT FK_AiDomandeRuoli_Ruoli FOREIGN KEY (RuoloId) REFERENCES dbo.Ruoli (Id) ON DELETE CASCADE
    );
END
GO
