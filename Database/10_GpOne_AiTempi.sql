/*
    GpOne - Tempi di preparazione dati e di risposta nel registro degli invii all'AI
    Script idempotente: può essere rieseguito senza errori.
*/

IF COL_LENGTH(N'dbo.AiLogInvii', N'MsDati') IS NULL
    ALTER TABLE dbo.AiLogInvii ADD
        MsDati          INT NULL,   -- lettura dei dati dal database
        MsPrimaRisposta INT NULL,   -- dall'invio al primo testo ricevuto dal motore
        MsTotale        INT NULL;   -- dall'invio della domanda alla fine della risposta
GO
