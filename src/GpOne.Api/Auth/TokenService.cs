using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;

namespace GpOne.Api.Auth;

public record UtenteAutenticato(int Id, string Username, string? NomeCompleto, bool Amministratore);

/// <summary>Emissione dei token JWT. La chiave di firma è generata al primo avvio e salvata cifrata in CartellaDati.</summary>
public class TokenService
{
    public const string Emittente = "GpOne";
    public const string RuoloAmministratore = "Amministratore";

    private readonly TimeSpan durata;

    public TokenService(string cartellaDati, IDataProtectionProvider dataProtection, IConfiguration configuration)
    {
        durata = TimeSpan.FromMinutes(configuration.GetValue("GpOne:DurataTokenMinuti", 480));

        var protector = dataProtection.CreateProtector("GpOne.Jwt.v1");
        var file = Path.Combine(cartellaDati, "jwt.key");
        byte[] chiave;
        if (File.Exists(file))
            chiave = protector.Unprotect(File.ReadAllBytes(file));
        else
        {
            chiave = RandomNumberGenerator.GetBytes(64);
            Directory.CreateDirectory(cartellaDati);
            File.WriteAllBytes(file, protector.Protect(chiave));
        }
        ChiaveFirma = new SymmetricSecurityKey(chiave);
    }

    public SymmetricSecurityKey ChiaveFirma { get; }

    public (string Token, DateTime Scadenza) Crea(UtenteAutenticato utente)
    {
        var scadenza = DateTime.UtcNow.Add(durata);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, utente.Id.ToString()),
            new(ClaimTypes.NameIdentifier, utente.Id.ToString()),
            new(ClaimTypes.Name, utente.Username)
        };
        if (utente.Amministratore)
            claims.Add(new Claim(ClaimTypes.Role, RuoloAmministratore));

        var token = new JwtSecurityToken(
            issuer: Emittente,
            audience: Emittente,
            claims: claims,
            expires: scadenza,
            signingCredentials: new SigningCredentials(ChiaveFirma, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), scadenza);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static int UtenteId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());

    public static bool Amministratore(this ClaimsPrincipal user) => user.IsInRole(TokenService.RuoloAmministratore);
}
