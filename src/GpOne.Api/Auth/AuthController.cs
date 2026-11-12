using Dapper;
using GpOne.Api.Infrastruttura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GpOne.Api.Auth;

public record LoginRichiesta(string Username, string Password);
public record PrimoAmministratoreRichiesta(string Username, string Password, string? Email, string? NomeCompleto);
public record CambiaPasswordRichiesta(string PasswordAttuale, string NuovaPassword);
public record LoginRisposta(string Token, DateTime Scadenza, UtenteAutenticato Utente);

[ApiController]
[Route("api/auth")]
public class AuthController(GpOneDatabase database, TokenService tokens) : ControllerBase
{
    private const int MaxTentativi = 5;
    private const int MinutiBlocco = 15;
    private const string CredenzialiNonValide = "Nome utente o password non validi.";

    public static readonly PasswordHasher<object> Hasher = new();

    public static string? VerificaPassword(string password) =>
        password.Length < 8 ? "La password deve contenere almeno 8 caratteri." : null;

    /// <summary>Primo avvio: nessun utente presente, si deve creare l'amministratore.</summary>
    [HttpGet("stato")]
    public async Task<object> Stato()
    {
        await using var connection = await database.ApriAsync();
        return new { primoAvvio = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Utenti") == 0 };
    }

    [HttpPost("primo-amministratore")]
    public async Task<ActionResult<LoginRisposta>> PrimoAmministratore(PrimoAmministratoreRichiesta richiesta)
    {
        if (string.IsNullOrWhiteSpace(richiesta.Username))
            return Problem("Indicare il nome utente.", statusCode: 400);
        if (VerificaPassword(richiesta.Password) is { } errore)
            return Problem(errore, statusCode: 400);

        await using var connection = await database.ApriAsync();
        var creati = await connection.ExecuteAsync("""
            INSERT INTO dbo.Utenti (Username, Email, NomeCompleto, PasswordHash, Amministratore)
            SELECT @Username, @Email, @NomeCompleto, @Hash, 1
            WHERE NOT EXISTS (SELECT 1 FROM dbo.Utenti WITH (UPDLOCK, HOLDLOCK))
            """, new { Username = richiesta.Username.Trim(), richiesta.Email, richiesta.NomeCompleto, Hash = Hasher.HashPassword(null!, richiesta.Password) });
        if (creati == 0)
            return Problem("Esiste già almeno un utente.", statusCode: 409);

        return await Login(new LoginRichiesta(richiesta.Username.Trim(), richiesta.Password));
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginRisposta>> Login(LoginRichiesta richiesta)
    {
        await using var connection = await database.ApriAsync();
        var utente = await connection.QuerySingleOrDefaultAsync<UtenteDb>(
            "SELECT Id, Username, NomeCompleto, PasswordHash, Amministratore, Attivo, BloccatoFino FROM dbo.Utenti WHERE Username = @Username",
            new { Username = richiesta.Username?.Trim() });
        if (utente is null)
            return Problem(CredenzialiNonValide, statusCode: 401);

        if (utente.BloccatoFino > DateTime.UtcNow)
            return Problem($"Utente bloccato per troppi tentativi falliti. Riprovare dopo le {DateTime.SpecifyKind(utente.BloccatoFino.Value, DateTimeKind.Utc).ToLocalTime():HH:mm}.", statusCode: 401);

        var verifica = Hasher.VerifyHashedPassword(null!, utente.PasswordHash, richiesta.Password ?? string.Empty);
        if (verifica == PasswordVerificationResult.Failed)
        {
            // Le espressioni a destra usano i valori precedenti all'UPDATE
            await connection.ExecuteAsync("""
                UPDATE dbo.Utenti
                SET BloccatoFino = CASE WHEN TentativiFalliti + 1 >= @Max THEN DATEADD(MINUTE, @Minuti, SYSUTCDATETIME()) ELSE BloccatoFino END,
                    TentativiFalliti = CASE WHEN TentativiFalliti + 1 >= @Max THEN 0 ELSE TentativiFalliti + 1 END
                WHERE Id = @Id
                """, new { utente.Id, Max = MaxTentativi, Minuti = MinutiBlocco });
            return Problem(CredenzialiNonValide, statusCode: 401);
        }
        if (!utente.Attivo)
            return Problem("Utente disattivato.", statusCode: 401);

        await connection.ExecuteAsync("""
            UPDATE dbo.Utenti
            SET TentativiFalliti = 0, BloccatoFino = NULL, UltimoAccesso = SYSUTCDATETIME(),
                PasswordHash = COALESCE(@NuovoHash, PasswordHash)
            WHERE Id = @Id
            """, new
        {
            utente.Id,
            NuovoHash = verifica == PasswordVerificationResult.SuccessRehashNeeded ? Hasher.HashPassword(null!, richiesta.Password!) : null
        });

        var autenticato = new UtenteAutenticato(utente.Id, utente.Username, utente.NomeCompleto, utente.Amministratore);
        var (token, scadenza) = tokens.Crea(autenticato);
        return new LoginRisposta(token, scadenza, autenticato);
    }

    [Authorize]
    [HttpPost("cambia-password")]
    public async Task<IActionResult> CambiaPassword(CambiaPasswordRichiesta richiesta)
    {
        if (VerificaPassword(richiesta.NuovaPassword) is { } errore)
            return Problem(errore, statusCode: 400);

        await using var connection = await database.ApriAsync();
        var id = User.UtenteId();
        var hash = await connection.ExecuteScalarAsync<string>("SELECT PasswordHash FROM dbo.Utenti WHERE Id = @id", new { id });
        if (hash is null || Hasher.VerifyHashedPassword(null!, hash, richiesta.PasswordAttuale) == PasswordVerificationResult.Failed)
            return Problem("La password attuale non è corretta.", statusCode: 400);

        await connection.ExecuteAsync("UPDATE dbo.Utenti SET PasswordHash = @Hash WHERE Id = @id",
            new { id, Hash = Hasher.HashPassword(null!, richiesta.NuovaPassword) });
        return NoContent();
    }

    private sealed record UtenteDb(int Id, string Username, string? NomeCompleto, string PasswordHash, bool Amministratore, bool Attivo, DateTime? BloccatoFino);
}
