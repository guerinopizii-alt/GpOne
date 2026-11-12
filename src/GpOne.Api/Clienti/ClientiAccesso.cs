using System.Security.Claims;
using Dapper;
using GpOne.Api.Auth;
using GpOne.Api.Infrastruttura;

namespace GpOne.Api.Clienti;

public record ClienteBreve(int Id, string Codice, string RagioneSociale);

/// <summary>
/// Clienti visibili da un utente (amministratori: tutti quelli attivi).
/// Il cliente scelto dal frontend arriva nell'header X-Cliente-Id e viene sempre verificato qui.
/// </summary>
public class ClientiAccesso(GpOneDatabase database)
{
    public const string HeaderCliente = "X-Cliente-Id";

    public async Task<IReadOnlyList<ClienteBreve>> ClientiUtenteAsync(ClaimsPrincipal user)
    {
        await using var connection = await database.ApriAsync();
        var sql = user.Amministratore()
            ? "SELECT Id, Codice, RagioneSociale FROM dbo.Clienti WHERE Attivo = 1 ORDER BY RagioneSociale"
            : """
              SELECT c.Id, c.Codice, c.RagioneSociale
              FROM dbo.UtentiClienti uc JOIN dbo.Clienti c ON c.Id = uc.ClienteId
              WHERE uc.UtenteId = @UtenteId AND c.Attivo = 1
              ORDER BY c.RagioneSociale
              """;
        return [.. await connection.QueryAsync<ClienteBreve>(sql, new { UtenteId = user.UtenteId() })];
    }

    /// <summary>Cliente dell'header X-Cliente-Id, se l'utente può accedervi; altrimenti null.</summary>
    public async Task<ClienteBreve?> ClienteRichiestaAsync(HttpContext context)
    {
        if (!int.TryParse(context.Request.Headers[HeaderCliente], out var clienteId))
            return null;
        return (await ClientiUtenteAsync(context.User)).FirstOrDefault(c => c.Id == clienteId);
    }
}
