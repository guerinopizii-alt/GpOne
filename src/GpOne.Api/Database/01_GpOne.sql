/*
    GpOne - Schema del database applicativo
    Script idempotente: può essere rieseguito senza errori. Viene eseguito all'avvio dell'API.
    I dati dei clienti NON stanno qui: qui ci sono solo utenti, clienti (con la connessione al loro DB),
    la struttura abilitata per l'AI e lo storico delle domande (senza risultati).
*/

/* ---------- Utenti ---------- */
IF OBJECT_ID(N'dbo.Utenti', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Utenti
    (
        Id                  INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_Utenti PRIMARY KEY,
        Username            NVARCHAR(100)       NOT NULL,
        Email               NVARCHAR(256)       NULL,
        NomeCompleto        NVARCHAR(200)       NULL,
        PasswordHash        NVARCHAR(500)       NOT NULL,
        Amministratore      BIT                 NOT NULL CONSTRAINT DF_Utenti_Amministratore DEFAULT (0),
        Attivo              BIT                 NOT NULL CONSTRAINT DF_Utenti_Attivo DEFAULT (1),
        TentativiFalliti    INT                 NOT NULL CONSTRAINT DF_Utenti_TentativiFalliti DEFAULT (0),
        BloccatoFino        DATETIME2(0)        NULL,
        UltimoAccesso       DATETIME2(0)        NULL,
        DataCreazione       DATETIME2(0)        NOT NULL CONSTRAINT DF_Utenti_DataCreazione DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_Utenti_Username UNIQUE (Username)
    );
END
GO

/* ---------- Clienti ----------
   Il DB del cliente sta nella sua rete: lo raggiunge solo l'agente GpOne installato dal cliente,
   che si collega all'API con ClienteId + chiave. Qui si salva solo l'hash della chiave. */
IF OBJECT_ID(N'dbo.Clienti', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clienti
    (
        Id                      INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_Clienti PRIMARY KEY,
        Codice                  NVARCHAR(50)        NOT NULL,
        RagioneSociale          NVARCHAR(200)       NOT NULL,
        Attivo                  BIT                 NOT NULL CONSTRAINT DF_Clienti_Attivo DEFAULT (1),
        -- SHA-256 della chiave dell'agente: la chiave in chiaro si mostra una sola volta all'amministratore
        AgenteChiaveHash        VARBINARY(32)       NULL,
        AgenteUltimoContatto    DATETIME2(0)        NULL,
        AgenteVersione          NVARCHAR(50)        NULL,
        DataCreazione           DATETIME2(0)        NOT NULL CONSTRAINT DF_Clienti_DataCreazione DEFAULT (SYSUTCDATETIME()),
        DataModifica            DATETIME2(0)        NULL,
        CONSTRAINT UQ_Clienti_Codice UNIQUE (Codice)
    );
END
GO

/* ---------- Abbinamento utenti-clienti (gli amministratori vedono tutti i clienti) ---------- */
IF OBJECT_ID(N'dbo.UtentiClienti', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UtentiClienti
    (
        UtenteId    INT NOT NULL,
        ClienteId   INT NOT NULL,
        CONSTRAINT PK_UtentiClienti PRIMARY KEY (UtenteId, ClienteId),
        CONSTRAINT FK_UtentiClienti_Utenti  FOREIGN KEY (UtenteId)  REFERENCES dbo.Utenti (Id)  ON DELETE CASCADE,
        CONSTRAINT FK_UtentiClienti_Clienti FOREIGN KEY (ClienteId) REFERENCES dbo.Clienti (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_UtentiClienti_ClienteId ON dbo.UtentiClienti (ClienteId);
END
GO

/* ---------- Struttura del DB cliente nota all'AI (solo nomi, tipi e descrizioni: mai dati) ---------- */
IF OBJECT_ID(N'dbo.SchemaOggetti', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaOggetti
    (
        Id              INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_SchemaOggetti PRIMARY KEY,
        ClienteId       INT                 NOT NULL,
        SchemaNome      NVARCHAR(128)       NOT NULL,
        Nome            NVARCHAR(128)       NOT NULL,
        Tipo            NVARCHAR(10)        NOT NULL,   -- TABELLA, VISTA
        Abilitato       BIT                 NOT NULL CONSTRAINT DF_SchemaOggetti_Abilitato DEFAULT (0),
        Descrizione     NVARCHAR(1000)      NULL,
        CONSTRAINT FK_SchemaOggetti_Clienti FOREIGN KEY (ClienteId) REFERENCES dbo.Clienti (Id) ON DELETE CASCADE,
        CONSTRAINT UQ_SchemaOggetti UNIQUE (ClienteId, SchemaNome, Nome)
    );
END
GO

IF OBJECT_ID(N'dbo.SchemaColonne', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaColonne
    (
        Id              INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_SchemaColonne PRIMARY KEY,
        OggettoId       INT                 NOT NULL,
        Nome            NVARCHAR(128)       NOT NULL,
        TipoDati        NVARCHAR(128)       NOT NULL,
        Ordine          INT                 NOT NULL,
        Abilitata       BIT                 NOT NULL CONSTRAINT DF_SchemaColonne_Abilitata DEFAULT (1),
        Descrizione     NVARCHAR(1000)      NULL,
        CONSTRAINT FK_SchemaColonne_Oggetti FOREIGN KEY (OggettoId) REFERENCES dbo.SchemaOggetti (Id) ON DELETE CASCADE,
        CONSTRAINT UQ_SchemaColonne UNIQUE (OggettoId, Nome)
    );
END
GO

/* ---------- Motore AI di ogni cliente (riceve solo struttura e domanda, mai dati) ---------- */
IF OBJECT_ID(N'dbo.ConfigurazioniAi', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConfigurazioniAi
    (
        ClienteId       INT                 NOT NULL CONSTRAINT PK_ConfigurazioniAi PRIMARY KEY,
        Provider        NVARCHAR(20)        NOT NULL,   -- Claude, Gemini, GeminiVertex, Ollama
        Modello         NVARCHAR(100)       NOT NULL,
        Endpoint        NVARCHAR(500)       NULL,       -- Ollama: es. http://localhost:11434
        ApiKeyCifrata   NVARCHAR(MAX)       NULL,       -- Claude: chiave API cifrata, mai restituita
        Effort          NVARCHAR(10)        NULL,       -- Claude: low, medium, high, xhigh, max
        Istruzioni      NVARCHAR(MAX)       NULL,       -- indicazioni aggiuntive per l'AI su questo cliente
        MaxRighe        INT                 NOT NULL CONSTRAINT DF_ConfigurazioniAi_MaxRighe DEFAULT (1000),
        TimeoutSecondi  INT                 NOT NULL CONSTRAINT DF_ConfigurazioniAi_Timeout DEFAULT (30),
        CONSTRAINT FK_ConfigurazioniAi_Clienti FOREIGN KEY (ClienteId) REFERENCES dbo.Clienti (Id) ON DELETE CASCADE
    );
END
GO

/* ---------- Storico domande: testo, SQL ed esito. I risultati non vengono salvati ---------- */
IF OBJECT_ID(N'dbo.Domande', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Domande
    (
        Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Domande PRIMARY KEY,
        UtenteId        INT                 NULL,
        ClienteId       INT                 NOT NULL,
        Testo           NVARCHAR(2000)      NOT NULL,
        SqlGenerato     NVARCHAR(MAX)       NULL,
        Spiegazione     NVARCHAR(2000)      NULL,
        Grafico         NVARCHAR(1000)      NULL,   -- JSON {tipo, x, y}
        Esito           NVARCHAR(20)        NOT NULL,   -- Ok, SqlNonValido, ErroreSql, ErroreAi
        Errore          NVARCHAR(2000)      NULL,
        Righe           INT                 NULL,
        DurataMs        INT                 NULL,
        DataOra         DATETIME2(0)        NOT NULL CONSTRAINT DF_Domande_DataOra DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Domande_Utenti  FOREIGN KEY (UtenteId)  REFERENCES dbo.Utenti (Id)  ON DELETE SET NULL,
        CONSTRAINT FK_Domande_Clienti FOREIGN KEY (ClienteId) REFERENCES dbo.Clienti (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_Domande_Utente ON dbo.Domande (UtenteId, DataOra DESC);
END
GO
