/*
    GpOne - Gestione utenti: permessi, log accessi, storico password, reimpostazione password
    Prerequisito: 01_GpOne_UtentiRuoli.sql (tabelle Utenti, Ruoli, UtentiRuoli).
    Script idempotente: può essere rieseguito senza errori.
    Viene eseguito dall'app (Sistema > Database) nel database del profilo di connessione.
*/

/* ---------- Utenti: cambio password obbligatorio ---------- */
IF COL_LENGTH(N'dbo.Utenti', N'CambioPasswordObbligatorio') IS NULL
    ALTER TABLE dbo.Utenti
        ADD CambioPasswordObbligatorio BIT NOT NULL
            CONSTRAINT DF_Utenti_CambioPasswordObbligatorio DEFAULT (0);
GO

/* ---------- Permessi ---------- */
IF OBJECT_ID(N'dbo.Permessi', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Permessi
    (
        Id              INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_Permessi PRIMARY KEY,
        Codice          NVARCHAR(100)       NOT NULL,
        Area            NVARCHAR(50)        NOT NULL,
        Descrizione     NVARCHAR(200)       NULL,
        CONSTRAINT UQ_Permessi_Codice UNIQUE (Codice)
    );
END
GO

/* ---------- Associazione ruoli-permessi (molti a molti) ---------- */
IF OBJECT_ID(N'dbo.RuoliPermessi', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RuoliPermessi
    (
        RuoloId         INT NOT NULL,
        PermessoId      INT NOT NULL,
        CONSTRAINT PK_RuoliPermessi PRIMARY KEY (RuoloId, PermessoId),
        CONSTRAINT FK_RuoliPermessi_Ruoli    FOREIGN KEY (RuoloId)    REFERENCES dbo.Ruoli (Id)    ON DELETE CASCADE,
        CONSTRAINT FK_RuoliPermessi_Permessi FOREIGN KEY (PermessoId) REFERENCES dbo.Permessi (Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_RuoliPermessi_PermessoId ON dbo.RuoliPermessi (PermessoId);
END
GO

/* ---------- Log degli accessi ---------- */
IF OBJECT_ID(N'dbo.LogAccessi', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LogAccessi
    (
        Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LogAccessi PRIMARY KEY,
        -- NULL se il nome utente indicato non esiste
        UtenteId        INT                 NULL,
        Username        NVARCHAR(100)       NOT NULL,
        -- Successo, CredenzialiErrate, Bloccato, Disattivato, Logout
        Esito           NVARCHAR(30)        NOT NULL,
        Applicazione    NVARCHAR(30)        NULL,   -- Web, Desktop
        IndirizzoIp     NVARCHAR(45)        NULL,
        DataOra         DATETIME2(0)        NOT NULL CONSTRAINT DF_LogAccessi_DataOra DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_LogAccessi_Utenti FOREIGN KEY (UtenteId) REFERENCES dbo.Utenti (Id) ON DELETE SET NULL
    );

    CREATE INDEX IX_LogAccessi_UtenteId_DataOra ON dbo.LogAccessi (UtenteId, DataOra DESC);
    CREATE INDEX IX_LogAccessi_DataOra ON dbo.LogAccessi (DataOra DESC);
END
GO

/* ---------- Storico password (per impedire il riutilizzo delle ultime password) ---------- */
IF OBJECT_ID(N'dbo.StoricoPassword', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StoricoPassword
    (
        Id              INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_StoricoPassword PRIMARY KEY,
        UtenteId        INT                 NOT NULL,
        PasswordHash    NVARCHAR(500)       NOT NULL,
        DataCreazione   DATETIME2(0)        NOT NULL CONSTRAINT DF_StoricoPassword_DataCreazione DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_StoricoPassword_Utenti FOREIGN KEY (UtenteId) REFERENCES dbo.Utenti (Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_StoricoPassword_UtenteId ON dbo.StoricoPassword (UtenteId, DataCreazione DESC);
END
GO

/* ---------- Token di reimpostazione password ---------- */
IF OBJECT_ID(N'dbo.TokenReimpostazionePassword', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TokenReimpostazionePassword
    (
        Id              INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_TokenReimpostazionePassword PRIMARY KEY,
        UtenteId        INT                 NOT NULL,
        -- SHA-256 del token: il token in chiaro viene inviato all'utente e mai salvato
        TokenHash       VARBINARY(32)       NOT NULL,
        Scadenza        DATETIME2(0)        NOT NULL,
        UsatoIl         DATETIME2(0)        NULL,
        DataCreazione   DATETIME2(0)        NOT NULL CONSTRAINT DF_TokenReimpostazionePassword_DataCreazione DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_TokenReimpostazionePassword_Utenti FOREIGN KEY (UtenteId) REFERENCES dbo.Utenti (Id) ON DELETE CASCADE,
        CONSTRAINT UQ_TokenReimpostazionePassword_TokenHash UNIQUE (TokenHash)
    );

    CREATE INDEX IX_TokenReimpostazionePassword_UtenteId ON dbo.TokenReimpostazionePassword (UtenteId);
END
GO

/* ---------- Vista: permessi effettivi di ogni utente ---------- */
CREATE OR ALTER VIEW dbo.vw_PermessiUtente
AS
SELECT DISTINCT
        u.Id        AS UtenteId,
        u.Username,
        p.Codice    AS Permesso,
        p.Area
FROM    dbo.Utenti u
JOIN    dbo.UtentiRuoli ur   ON ur.UtenteId = u.Id
JOIN    dbo.Ruoli r          ON r.Id = ur.RuoloId AND r.Attivo = 1
JOIN    dbo.RuoliPermessi rp ON rp.RuoloId = r.Id
JOIN    dbo.Permessi p       ON p.Id = rp.PermessoId
WHERE   u.Attivo = 1;
GO

/* ---------- Permessi predefiniti ---------- */
INSERT INTO dbo.Permessi (Codice, Area, Descrizione)
SELECT v.Codice, v.Area, v.Descrizione
FROM (VALUES
        (N'Utenti.Visualizza',       N'Utenti',       N'Visualizzare l''elenco degli utenti'),
        (N'Utenti.Gestisci',         N'Utenti',       N'Creare, modificare e disattivare utenti'),
        (N'Ruoli.Gestisci',          N'Utenti',       N'Gestire ruoli e permessi'),
        (N'Connessioni.Gestisci',    N'Sistema',      N'Gestire i profili di connessione al database'),
        (N'Impostazioni.Gestisci',   N'Sistema',      N'Modificare le impostazioni dell''applicazione'),
        (N'LogAccessi.Visualizza',   N'Sistema',      N'Consultare il log degli accessi'),
        (N'Clienti.Visualizza',      N'Gestione',     N'Consultare i clienti'),
        (N'Clienti.Gestisci',        N'Gestione',     N'Creare e modificare i clienti'),
        (N'Documenti.Visualizza',    N'Gestione',     N'Consultare i documenti'),
        (N'Documenti.Gestisci',      N'Gestione',     N'Caricare e modificare i documenti'),
        (N'Report.Visualizza',       N'Analisi',      N'Consultare i report'),
        (N'Copilot.Usa',             N'Generale',     N'Usare l''assistente Copilot')
     ) AS v (Codice, Area, Descrizione)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Permessi p WHERE p.Codice = v.Codice);
GO

/* ---------- Assegnazione permessi ai ruoli ---------- */
INSERT INTO dbo.RuoliPermessi (RuoloId, PermessoId)
SELECT r.Id, p.Id
FROM dbo.Ruoli r
JOIN dbo.Permessi p ON
       r.Codice = N'Amministratore'                                   -- tutti i permessi
    OR (r.Codice = N'Utente'  AND p.Codice IN (N'Clienti.Visualizza', N'Clienti.Gestisci',
                                               N'Documenti.Visualizza', N'Documenti.Gestisci',
                                               N'Report.Visualizza', N'Copilot.Usa'))
    OR (r.Codice = N'Lettore' AND p.Codice IN (N'Clienti.Visualizza', N'Documenti.Visualizza',
                                               N'Report.Visualizza'))
WHERE NOT EXISTS (SELECT 1 FROM dbo.RuoliPermessi rp WHERE rp.RuoloId = r.Id AND rp.PermessoId = p.Id);
GO

/* ---------- Admin con password predefinita: cambio obbligatorio ---------- */
UPDATE dbo.Utenti
SET CambioPasswordObbligatorio = 1
WHERE Username = N'Admin'
  AND UltimoAccesso IS NULL;
GO
