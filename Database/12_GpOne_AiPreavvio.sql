/*
    GpOne - Preavvio di Gp-One AI subito dopo l'accesso (caricamento del modello Ollama e della struttura delle tabelle)
    Script idempotente: può essere rieseguito senza errori.
*/

IF COL_LENGTH(N'dbo.AiConfigurazioni', N'Preavvio') IS NULL
    ALTER TABLE dbo.AiConfigurazioni ADD
        Preavvio BIT NOT NULL CONSTRAINT DF_AiConfigurazioni_Preavvio DEFAULT (0);
GO
