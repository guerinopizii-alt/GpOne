/*
    GpOne - Struttura utenti e ruoli
    Script idempotente: può essere rieseguito senza errori.
    Viene eseguito dall'app (Sistema > Database) nel database del profilo di connessione.
    Esecuzione manuale: aprire il database di destinazione in SSMS ed eseguire lo script.
*/

/* ---------- Ruoli ---------- */
IF OBJECT_ID(N'dbo.Ruoli', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ruoli
    (
        Id              INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_Ruoli PRIMARY KEY,
        Codice          NVARCHAR(50)        NOT NULL,
        Descrizione     NVARCHAR(200)       NULL,
        Attivo          BIT                 NOT NULL CONSTRAINT DF_Ruoli_Attivo DEFAULT (1),
        DataCreazione   DATETIME2(0)        NOT NULL CONSTRAINT DF_Ruoli_DataCreazione DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_Ruoli_Codice UNIQUE (Codice)
    );
END
GO

/* ---------- Utenti ---------- */
IF OBJECT_ID(N'dbo.Utenti', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Utenti
    (
        Id                  INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_Utenti PRIMARY KEY,
        Username            NVARCHAR(100)       NOT NULL,
        Email               NVARCHAR(256)       NOT NULL,
        Nome                NVARCHAR(100)       NULL,
        Cognome             NVARCHAR(100)       NULL,
        -- Hash della password generato dall'applicazione (es. PBKDF2); mai la password in chiaro
        PasswordHash        NVARCHAR(500)       NULL,
        Attivo              BIT                 NOT NULL CONSTRAINT DF_Utenti_Attivo DEFAULT (1),
        TentativiFalliti    INT                 NOT NULL CONSTRAINT DF_Utenti_TentativiFalliti DEFAULT (0),
        BloccatoFino        DATETIME2(0)        NULL,
        UltimoAccesso       DATETIME2(0)        NULL,
        DataCreazione       DATETIME2(0)        NOT NULL CONSTRAINT DF_Utenti_DataCreazione DEFAULT (SYSUTCDATETIME()),
        DataModifica        DATETIME2(0)        NULL,
        CONSTRAINT UQ_Utenti_Username UNIQUE (Username),
        CONSTRAINT UQ_Utenti_Email UNIQUE (Email)
    );
END
GO

/* ---------- Associazione utenti-ruoli (molti a molti) ---------- */
IF OBJECT_ID(N'dbo.UtentiRuoli', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UtentiRuoli
    (
        UtenteId        INT             NOT NULL,
        RuoloId         INT             NOT NULL,
        DataAssegnazione DATETIME2(0)   NOT NULL CONSTRAINT DF_UtentiRuoli_DataAssegnazione DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_UtentiRuoli PRIMARY KEY (UtenteId, RuoloId),
        CONSTRAINT FK_UtentiRuoli_Utenti FOREIGN KEY (UtenteId) REFERENCES dbo.Utenti (Id) ON DELETE CASCADE,
        CONSTRAINT FK_UtentiRuoli_Ruoli  FOREIGN KEY (RuoloId)  REFERENCES dbo.Ruoli (Id)  ON DELETE CASCADE
    );

    CREATE INDEX IX_UtentiRuoli_RuoloId ON dbo.UtentiRuoli (RuoloId);
END
GO

/* ---------- Vista di comodo ---------- */
CREATE OR ALTER VIEW dbo.vw_UtentiRuoli
AS
SELECT  u.Id        AS UtenteId,
        u.Username,
        u.Email,
        u.Nome,
        u.Cognome,
        u.Attivo    AS UtenteAttivo,
        r.Id        AS RuoloId,
        r.Codice    AS Ruolo,
        r.Descrizione AS DescrizioneRuolo
FROM    dbo.Utenti u
JOIN    dbo.UtentiRuoli ur ON ur.UtenteId = u.Id
JOIN    dbo.Ruoli r        ON r.Id = ur.RuoloId;
GO

/* ---------- Ruoli predefiniti ---------- */
INSERT INTO dbo.Ruoli (Codice, Descrizione)
SELECT v.Codice, v.Descrizione
FROM (VALUES
        (N'Amministratore', N'Accesso completo, gestione utenti e impostazioni'),
        (N'Utente',         N'Accesso operativo alle funzionalità'),
        (N'Lettore',        N'Accesso in sola lettura')
     ) AS v (Codice, Descrizione)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Ruoli r WHERE r.Codice = v.Codice);
GO

/* ---------- Utente amministratore iniziale ----------
   Username: Admin   Password: admin
   L'hash è nel formato ASP.NET Identity V3 (PBKDF2-HMACSHA512, 100.000 iterazioni).
   Cambiare la password al primo accesso.
*/
IF NOT EXISTS (SELECT 1 FROM dbo.Utenti WHERE Username = N'Admin')
    INSERT INTO dbo.Utenti (Username, Email, Nome, Cognome, PasswordHash)
    VALUES (N'Admin', N'admin@localhost', N'Amministratore', NULL,
            N'AQAAAAIAAYagAAAAENXvFiMlpTN7wW4Bm2MeOykgL1CZMry5zkLIPySAUUThqWZlFM1LqtoQ+iGgaAz6YQ==');
GO

INSERT INTO dbo.UtentiRuoli (UtenteId, RuoloId)
SELECT u.Id, r.Id
FROM dbo.Utenti u
CROSS JOIN dbo.Ruoli r
WHERE u.Username = N'Admin'
  AND r.Codice = N'Amministratore'
  AND NOT EXISTS (SELECT 1 FROM dbo.UtentiRuoli ur WHERE ur.UtenteId = u.Id AND ur.RuoloId = r.Id);
GO
