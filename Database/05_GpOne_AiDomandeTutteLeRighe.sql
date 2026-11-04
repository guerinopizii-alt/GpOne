/*
    GpOne - Domande AI: numero di righe facoltativo (NULL = tutte le righe)
    Script idempotente: può essere rieseguito senza errori.
*/

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_AiDomandeFonti_MaxRighe')
    ALTER TABLE dbo.AiDomandeFonti DROP CONSTRAINT DF_AiDomandeFonti_MaxRighe;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.AiDomandeFonti') AND name = N'MaxRighe' AND is_nullable = 0)
    ALTER TABLE dbo.AiDomandeFonti ALTER COLUMN MaxRighe INT NULL;
GO
