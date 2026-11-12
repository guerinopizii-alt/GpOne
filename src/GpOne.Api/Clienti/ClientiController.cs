using System.Security.Cryptography;
using Dapper;
using GpOne.Api.Agenti;
using GpOne.Api.Auth;
using GpOne.Api.Infrastruttura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace GpOne.Api.Clienti;

public class ClienteDto
{
    public int Id { get; set; }
    public string Codice { get; set; } = string.Empty;
    public string RagioneSociale { get; set; } = string.Empty;
    public bool Attivo { get; set; } = true;
    public List<int> UtentiIds { get; set; } = [];

    // Sola lettura
    public bool ChiaveAgenteGenerata { get; set; }
    public bool AgenteCollegato { get; set; }
    public DateTime? AgenteUltimoContatto { get; set; }
    public string? AgenteVersione { get; set; }
}

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController(ClientiAccesso accesso) : ControllerBase
{
    [HttpGet("clienti")]
    public Task<IReadOnlyList<ClienteBreve>> Clienti() => accesso.ClientiUtenteAsync(User);
}

[ApiController]
[Route("api/admin/clienti")]
[Authorize(Roles = TokenService.RuoloAmministratore)]
public class ClientiController(GpOneDatabase database, AgentiRegistro agenti) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ClienteDto>> Elenco()
    {
        await using var connection = await database.ApriAsync();
        var clienti = (await connection.QueryAsync<ClienteDto>("""
            SELECT Id, Codice, RagioneSociale, Attivo,
                   CAST(CASE WHEN AgenteChiaveHash IS NULL THEN 0 ELSE 1 END AS BIT) AS ChiaveAgenteGenerata,
                   AgenteUltimoContatto, AgenteVersione
            FROM dbo.Clienti ORDER BY RagioneSociale
            """)).ToList();
        var abbinamenti = (await connection.QueryAsync<(int UtenteId, int ClienteId)>("SELECT UtenteId, ClienteId FROM dbo.UtentiClienti"))
            .ToLookup(a => a.ClienteId, a => a.UtenteId);
        foreach (var cliente in clienti)
        {
            cliente.UtentiIds = [.. abbinamenti[cliente.Id]];
            cliente.AgenteCollegato = agenti.Collegato(cliente.Id);
            cliente.AgenteUltimoContatto = cliente.AgenteUltimoContatto is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null;
        }
        return clienti;
    }

    [HttpPost]
    public Task<IActionResult> Crea(ClienteDto cliente) => SalvaAsync(0, cliente);

    [HttpPut("{id:int}")]
    public Task<IActionResult> Modifica(int id, ClienteDto cliente) => SalvaAsync(id, cliente);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Elimina(int id)
    {
        await using var connection = await database.ApriAsync();
        await connection.ExecuteAsync("DELETE FROM dbo.Clienti WHERE Id = @id", new { id });
        return NoContent();
    }

    /// <summary>
    /// Genera (o rigenera) la chiave dell'agente: viene restituita una sola volta e va scritta nella
    /// configurazione dell'agente presso il cliente. Una nuova chiave invalida la precedente.
    /// </summary>
    [HttpPost("{id:int}/chiave-agente")]
    public async Task<IActionResult> ChiaveAgente(int id)
    {
        var chiave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        await using var connection = await database.ApriAsync();
        var aggiornati = await connection.ExecuteAsync(
            "UPDATE dbo.Clienti SET AgenteChiaveHash = @hash, DataModifica = SYSUTCDATETIME() WHERE Id = @id",
            new { id, hash = AgentiHub.HashChiave(chiave) });
        return aggiornati == 0 ? NotFound() : Ok(new { clienteId = id, chiave });
    }

    private async Task<IActionResult> SalvaAsync(int id, ClienteDto c)
    {
        if (string.IsNullOrWhiteSpace(c.Codice) || string.IsNullOrWhiteSpace(c.RagioneSociale))
            return Problem("Indicare codice e ragione sociale.", statusCode: 400);

        await using var connection = await database.ApriAsync();
        await using var transaction = connection.BeginTransaction();
        try
        {
            id = await connection.ExecuteScalarAsync<int>(id == 0
                ? """
                  INSERT INTO dbo.Clienti (Codice, RagioneSociale, Attivo)
                  OUTPUT INSERTED.Id
                  VALUES (@Codice, @RagioneSociale, @Attivo)
                  """
                : """
                  UPDATE dbo.Clienti
                  SET Codice = @Codice, RagioneSociale = @RagioneSociale, Attivo = @Attivo, DataModifica = SYSUTCDATETIME()
                  OUTPUT INSERTED.Id
                  WHERE Id = @Id
                  """, new { Id = id, Codice = c.Codice.Trim(), RagioneSociale = c.RagioneSociale.Trim(), c.Attivo }, transaction);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Problem("Codice cliente già usato da un altro cliente.", statusCode: 409);
        }
        if (id == 0)
            return NotFound();

        await connection.ExecuteAsync("DELETE FROM dbo.UtentiClienti WHERE ClienteId = @id", new { id }, transaction);
        await connection.ExecuteAsync("INSERT INTO dbo.UtentiClienti (UtenteId, ClienteId) VALUES (@UtenteId, @ClienteId)",
            c.UtentiIds.Distinct().Select(u => new { UtenteId = u, ClienteId = id }), transaction);
        transaction.Commit();
        return Ok(new { id });
    }
}
