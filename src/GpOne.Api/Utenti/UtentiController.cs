using Dapper;
using GpOne.Api.Auth;
using GpOne.Api.Infrastruttura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace GpOne.Api.Utenti;

public class UtenteDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? NomeCompleto { get; set; }
    public bool Amministratore { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime? BloccatoFino { get; set; }
    public DateTime? UltimoAccesso { get; set; }
    /// <summary>Solo in scrittura: obbligatoria per un nuovo utente, vuota in modifica = invariata.</summary>
    public string? Password { get; set; }
    public List<int> ClientiIds { get; set; } = [];
}

/// <summary>Gestione utenti, riservata agli amministratori. Non si può disattivare o declassare il proprio utente.</summary>
[ApiController]
[Route("api/admin/utenti")]
[Authorize(Roles = TokenService.RuoloAmministratore)]
public class UtentiController(GpOneDatabase database) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<UtenteDto>> Elenco()
    {
        await using var connection = await database.ApriAsync();
        var utenti = (await connection.QueryAsync<UtenteDto>("""
            SELECT Id, Username, Email, NomeCompleto, Amministratore, Attivo, BloccatoFino, UltimoAccesso
            FROM dbo.Utenti ORDER BY Username
            """)).ToList();
        var abbinamenti = (await connection.QueryAsync<(int UtenteId, int ClienteId)>("SELECT UtenteId, ClienteId FROM dbo.UtentiClienti"))
            .ToLookup(a => a.UtenteId, a => a.ClienteId);
        foreach (var utente in utenti)
        {
            utente.ClientiIds = [.. abbinamenti[utente.Id]];
            utente.BloccatoFino = utente.BloccatoFino is { } b ? DateTime.SpecifyKind(b, DateTimeKind.Utc) : null;
            utente.UltimoAccesso = utente.UltimoAccesso is { } u ? DateTime.SpecifyKind(u, DateTimeKind.Utc) : null;
        }
        return utenti;
    }

    [HttpPost]
    public Task<IActionResult> Crea(UtenteDto utente) => SalvaAsync(0, utente);

    [HttpPut("{id:int}")]
    public Task<IActionResult> Modifica(int id, UtenteDto utente) => SalvaAsync(id, utente);

    [HttpPost("{id:int}/sblocca")]
    public async Task<IActionResult> Sblocca(int id)
    {
        await using var connection = await database.ApriAsync();
        await connection.ExecuteAsync("UPDATE dbo.Utenti SET BloccatoFino = NULL, TentativiFalliti = 0 WHERE Id = @id", new { id });
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Elimina(int id)
    {
        if (id == User.UtenteId())
            return Problem("Non puoi eliminare il tuo utente.", statusCode: 400);
        await using var connection = await database.ApriAsync();
        await connection.ExecuteAsync("DELETE FROM dbo.Utenti WHERE Id = @id", new { id });
        return NoContent();
    }

    private async Task<IActionResult> SalvaAsync(int id, UtenteDto u)
    {
        if (string.IsNullOrWhiteSpace(u.Username))
            return Problem("Indicare il nome utente.", statusCode: 400);
        if (id == 0 && string.IsNullOrEmpty(u.Password))
            return Problem("Indicare la password del nuovo utente.", statusCode: 400);
        if (!string.IsNullOrEmpty(u.Password) && AuthController.VerificaPassword(u.Password) is { } errore)
            return Problem(errore, statusCode: 400);
        if (id == User.UtenteId() && (!u.Attivo || !u.Amministratore))
            return Problem("Non puoi disattivare il tuo utente né togliergli il ruolo di amministratore.", statusCode: 400);

        await using var connection = await database.ApriAsync();
        await using var transaction = connection.BeginTransaction();
        var parametri = new
        {
            Id = id,
            Username = u.Username.Trim(),
            Email = string.IsNullOrWhiteSpace(u.Email) ? null : u.Email.Trim(),
            NomeCompleto = string.IsNullOrWhiteSpace(u.NomeCompleto) ? null : u.NomeCompleto.Trim(),
            u.Amministratore,
            u.Attivo,
            Hash = string.IsNullOrEmpty(u.Password) ? null : AuthController.Hasher.HashPassword(null!, u.Password)
        };
        try
        {
            id = await connection.ExecuteScalarAsync<int>(id == 0
                ? """
                  INSERT INTO dbo.Utenti (Username, Email, NomeCompleto, Amministratore, Attivo, PasswordHash)
                  OUTPUT INSERTED.Id
                  VALUES (@Username, @Email, @NomeCompleto, @Amministratore, @Attivo, @Hash)
                  """
                : """
                  UPDATE dbo.Utenti
                  SET Username = @Username, Email = @Email, NomeCompleto = @NomeCompleto, Amministratore = @Amministratore,
                      Attivo = @Attivo, PasswordHash = COALESCE(@Hash, PasswordHash)
                  OUTPUT INSERTED.Id
                  WHERE Id = @Id
                  """, parametri, transaction);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Problem("Nome utente già usato da un altro utente.", statusCode: 409);
        }
        if (id == 0)
            return NotFound();

        await connection.ExecuteAsync("DELETE FROM dbo.UtentiClienti WHERE UtenteId = @id", new { id }, transaction);
        await connection.ExecuteAsync("INSERT INTO dbo.UtentiClienti (UtenteId, ClienteId) VALUES (@UtenteId, @ClienteId)",
            u.ClientiIds.Distinct().Select(c => new { UtenteId = id, ClienteId = c }), transaction);
        transaction.Commit();
        return Ok(new { id });
    }
}
