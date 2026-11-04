/*
    GpOne - Domande AI: raggruppamenti (GROUP BY) e totali (SUM, COUNT, AVG, MIN, MAX) per ogni tabella
    Script idempotente: può essere rieseguito senza errori.
*/

IF COL_LENGTH(N'dbo.AiDomandeFonti', N'Raggruppamento') IS NULL
    -- Colonne di raggruppamento separate da virgola; NULL = nessun raggruppamento
    ALTER TABLE dbo.AiDomandeFonti ADD Raggruppamento NVARCHAR(MAX) NULL;
GO

IF COL_LENGTH(N'dbo.AiDomandeFonti', N'Aggregati') IS NULL
    -- Totali in JSON: [{"Funzione":"SUM","Colonna":"Totale","Alias":"Fatturato"}]
    ALTER TABLE dbo.AiDomandeFonti ADD Aggregati NVARCHAR(MAX) NULL;
GO
