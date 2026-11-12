using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace GpOne.Api.Infrastruttura;

/// <summary>
/// Cifratura delle password (stringa di connessione GpOne, DB dei clienti, chiave API) con la Data Protection.
/// Le chiavi stanno in GpOne:CartellaDati/keys, protette con DPAPI: copiare l'app su un altro server richiede
/// di cifrare di nuovo le password (o di portare le chiavi).
/// </summary>
public class Cifratura(IDataProtectionProvider provider)
{
    public const string Prefisso = "ENC:";

    private readonly IDataProtector protector = provider.CreateProtector("GpOne.Password.v1");

    public string Cifra(string testo) => Prefisso + protector.Protect(testo);

    /// <summary>Decifra un valore "ENC:..."; un valore senza prefisso viene restituito così com'è (in chiaro).</summary>
    public string Decifra(string valore)
    {
        if (!valore.StartsWith(Prefisso, StringComparison.Ordinal))
            return valore;
        try
        {
            return protector.Unprotect(valore[Prefisso.Length..]);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "Impossibile decifrare la password: è stata cifrata con chiavi diverse (altro server o cartella chiavi). Cifrarla di nuovo con CifraPassword.bat.", ex);
        }
    }
}
