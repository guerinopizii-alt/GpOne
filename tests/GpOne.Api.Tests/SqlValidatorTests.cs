using GpOne.Core;

namespace GpOne.Api.Tests;

public class SqlValidatorTests
{
    private static readonly OggettoConsentito[] Consentiti =
    [
        new("dbo", "Fatture", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Data", "Importo", "ClienteId" }),
        new("dbo", "Anagrafiche", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "RagioneSociale" })
    ];

    [Theory]
    [InlineData("SELECT YEAR(f.Data) AS Anno, SUM(f.Importo) AS Totale FROM dbo.Fatture f GROUP BY YEAR(f.Data) ORDER BY Anno")]
    [InlineData("SELECT a.RagioneSociale, COUNT(*) AS Numero FROM dbo.Fatture f JOIN dbo.Anagrafiche a ON a.Id = f.ClienteId GROUP BY a.RagioneSociale")]
    [InlineData("WITH Mesi AS (SELECT MONTH(Data) AS Mese, Importo FROM dbo.Fatture) SELECT Mese, SUM(Importo) AS Totale FROM Mesi GROUP BY Mese")]
    [InlineData("SELECT TOP (10) Id, Importo FROM Fatture ORDER BY Importo DESC")]
    [InlineData("SELECT Id, Importo FROM dbo.Fatture WITH (NOLOCK)")]
    public void AccettaSelectSuOggettiAbilitati(string sql) =>
        Assert.True(SqlValidator.Valida(sql, Consentiti).Valida, SqlValidator.Valida(sql, Consentiti).Errore);

    [Theory]
    [InlineData("DELETE FROM dbo.Fatture")]
    [InlineData("UPDATE dbo.Fatture SET Importo = 0")]
    [InlineData("SELECT Id FROM dbo.Fatture; DROP TABLE dbo.Fatture")]
    [InlineData("SELECT Id INTO dbo.Copia FROM dbo.Fatture")]
    [InlineData("EXEC sp_who")]
    [InlineData("SELECT * FROM dbo.Fatture")]
    [InlineData("SELECT Id FROM dbo.Stipendi")]
    [InlineData("SELECT Id, Iban FROM dbo.Anagrafiche")]
    [InlineData("SELECT Id FROM AltroDb.dbo.Fatture")]
    [InlineData("SELECT name FROM sys.objects")]
    [InlineData("SELECT * FROM OPENROWSET('SQLNCLI', 'Server=x;', 'SELECT 1')")]
    [InlineData("SELECT dbo.MiaFunzione(Id) AS X FROM dbo.Fatture")]
    [InlineData("SELECT @@VERSION AS V")]
    [InlineData("SELEC Id FROM dbo.Fatture")]
    [InlineData("INSERT INTO dbo.Fatture (Importo) VALUES (1)")]
    [InlineData("MERGE dbo.Fatture AS t USING dbo.Anagrafiche AS s ON t.ClienteId = s.Id WHEN MATCHED THEN DELETE;")]
    [InlineData("WITH x AS (SELECT Id FROM dbo.Fatture) DELETE FROM x")]
    [InlineData("SELECT Id FROM dbo.Fatture\nGO\nDELETE FROM dbo.Fatture")]
    [InlineData("SELECT Id FROM dbo.Fatture WITH (UPDLOCK)")]
    [InlineData("SELECT Id FROM dbo.Fatture WITH (TABLOCKX, HOLDLOCK)")]
    [InlineData("SELECT Id FROM dbo.Fatture OPTION (TABLE HINT(dbo.Fatture, XLOCK))")]
    [InlineData("SELECT NEXT VALUE FOR dbo.Contatore AS N FROM dbo.Fatture")]
    [InlineData("DECLARE @x INT; SELECT Id FROM dbo.Fatture")]
    [InlineData("SET NOCOUNT ON; SELECT Id FROM dbo.Fatture")]
    [InlineData("BEGIN TRAN; SELECT Id FROM dbo.Fatture")]
    public void RifiutaQueryNonConsentite(string sql) =>
        Assert.False(SqlValidator.Valida(sql, Consentiti).Valida);

    [Fact]
    public void LatoAgenteControllaSoloLaStruttura()
    {
        Assert.True(SqlValidator.Valida("SELECT Qualsiasi FROM dbo.Tabella").Valida);
        Assert.False(SqlValidator.Valida("TRUNCATE TABLE dbo.Tabella").Valida);
    }
}
