using Dapper;
using GpOne.Api.Agenti;
using GpOne.Api.Infrastruttura;
using GpOne.Core;
using GpOne.Motore;

namespace GpOne.Api.Schema;

// OggettoConfigurato e ColonnaConfigurata sono in GpOne.Motore (condivisi con GpOne.Desktop).

/// <summary>
/// Struttura del DB cliente nota a GpOne: letta tramite l'agente (solo INFORMATION_SCHEMA, nessun dato),
/// poi l'amministratore abilita tabelle/colonne e scrive le descrizioni che finiscono nel prompt dell'AI.
/// </summary>
public class SchemaService(GpOneDatabase database, AgenteClient agente)
{
    public async Task<List<OggettoConfigurato>> ElencoAsync(int clienteId, bool soloAbilitati = false)
    {
        await using var connection = await database.ApriAsync();
        var oggetti = (await connection.QueryAsync<OggettoConfigurato>($"""
            SELECT Id, SchemaNome, Nome, Tipo, Abilitato, Descrizione
            FROM dbo.SchemaOggetti WHERE ClienteId = @clienteId {(soloAbilitati ? "AND Abilitato = 1" : "")}
            ORDER BY SchemaNome, Nome
            """, new { clienteId })).ToDictionary(o => o.Id);
        var colonne = await connection.QueryAsync<ColonnaConfigurata>($"""
            SELECT c.Id, c.OggettoId, c.Nome, c.TipoDati, c.Ordine, c.Abilitata, c.Descrizione
            FROM dbo.SchemaColonne c JOIN dbo.SchemaOggetti o ON o.Id = c.OggettoId
            WHERE o.ClienteId = @clienteId {(soloAbilitati ? "AND o.Abilitato = 1 AND c.Abilitata = 1" : "")}
            ORDER BY c.OggettoId, c.Ordine
            """, new { clienteId });
        foreach (var colonna in colonne)
            if (oggetti.TryGetValue(colonna.OggettoId, out var oggetto))
                oggetto.Colonne.Add(colonna);
        return [.. oggetti.Values];
    }

    /// <summary>Oggetti e colonne abilitati, per il validatore SQL.</summary>
    public async Task<IReadOnlyList<OggettoConsentito>> ConsentitiAsync(int clienteId) =>
        Struttura.Consentiti(await ElencoAsync(clienteId, soloAbilitati: true));

    /// <summary>
    /// Legge la struttura dal DB del cliente tramite l'agente e la allinea: aggiunge oggetti e colonne nuovi
    /// (oggetti disabilitati, colonne abilitate), aggiorna i tipi, elimina ciò che non esiste più.
    /// Abilitazioni e descrizioni esistenti restano.
    /// </summary>
    public async Task<int> AggiornaDaAgenteAsync(int clienteId, CancellationToken cancellationToken = default)
    {
        var letti = await agente.LeggiSchemaAsync(clienteId, cancellationToken);

        await using var connection = await database.ApriAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var esistenti = (await connection.QueryAsync<(int Id, string SchemaNome, string Nome)>(
            "SELECT Id, SchemaNome, Nome FROM dbo.SchemaOggetti WHERE ClienteId = @clienteId", new { clienteId }, transaction))
            .ToDictionary(o => (o.SchemaNome.ToLowerInvariant(), o.Nome.ToLowerInvariant()), o => o.Id);

        foreach (var oggetto in letti)
        {
            var chiave = (oggetto.Schema.ToLowerInvariant(), oggetto.Nome.ToLowerInvariant());
            if (esistenti.Remove(chiave, out var id))
                await connection.ExecuteAsync("UPDATE dbo.SchemaOggetti SET Tipo = @Tipo WHERE Id = @id", new { id, oggetto.Tipo }, transaction);
            else
                id = await connection.ExecuteScalarAsync<int>("""
                    INSERT INTO dbo.SchemaOggetti (ClienteId, SchemaNome, Nome, Tipo) OUTPUT INSERTED.Id
                    VALUES (@clienteId, @Schema, @Nome, @Tipo)
                    """, new { clienteId, oggetto.Schema, oggetto.Nome, oggetto.Tipo }, transaction);

            await connection.ExecuteAsync("""
                DELETE FROM dbo.SchemaColonne WHERE OggettoId = @id AND Nome NOT IN @nomi
                """, new { id, nomi = oggetto.Colonne.Select(c => c.Nome).DefaultIfEmpty("").ToArray() }, transaction);
            foreach (var colonna in oggetto.Colonne)
                await connection.ExecuteAsync("""
                    MERGE dbo.SchemaColonne AS t
                    USING (SELECT @id AS OggettoId, @Nome AS Nome) AS s ON t.OggettoId = s.OggettoId AND t.Nome = s.Nome
                    WHEN MATCHED THEN UPDATE SET TipoDati = @TipoDati, Ordine = @Ordine
                    WHEN NOT MATCHED THEN INSERT (OggettoId, Nome, TipoDati, Ordine) VALUES (@id, @Nome, @TipoDati, @Ordine);
                    """, new { id, colonna.Nome, colonna.TipoDati, colonna.Ordine }, transaction);
        }

        if (esistenti.Count > 0)
            await connection.ExecuteAsync("DELETE FROM dbo.SchemaOggetti WHERE Id IN @ids", new { ids = esistenti.Values.ToArray() }, transaction);

        await transaction.CommitAsync(cancellationToken);
        return letti.Count;
    }

    /// <summary>Salva abilitazioni e descrizioni (solo degli oggetti del cliente indicato).</summary>
    public async Task SalvaAsync(int clienteId, IEnumerable<OggettoConfigurato> oggetti)
    {
        await using var connection = await database.ApriAsync();
        await using var transaction = connection.BeginTransaction();
        foreach (var oggetto in oggetti)
        {
            var aggiornati = await connection.ExecuteAsync("""
                UPDATE dbo.SchemaOggetti SET Abilitato = @Abilitato, Descrizione = NULLIF(LTRIM(RTRIM(@Descrizione)), '')
                WHERE Id = @Id AND ClienteId = @clienteId
                """, new { oggetto.Id, oggetto.Abilitato, oggetto.Descrizione, clienteId }, transaction);
            if (aggiornati == 0)
                continue;
            foreach (var colonna in oggetto.Colonne)
                await connection.ExecuteAsync("""
                    UPDATE dbo.SchemaColonne SET Abilitata = @Abilitata, Descrizione = NULLIF(LTRIM(RTRIM(@Descrizione)), '')
                    WHERE Id = @Id AND OggettoId = @OggettoId
                    """, new { colonna.Id, colonna.Abilitata, colonna.Descrizione, OggettoId = oggetto.Id }, transaction);
        }
        await transaction.CommitAsync();
    }
}
