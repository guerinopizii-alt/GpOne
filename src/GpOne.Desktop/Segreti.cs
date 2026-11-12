using System.Security.Cryptography;
using System.Text;

namespace GpOne.Desktop;

/// <summary>
/// Password SQL e chiave API salvate come "ENC:...", cifrate con DPAPI per l'utente Windows corrente:
/// leggibili solo da questo utente su questo computer.
/// </summary>
public static class Segreti
{
    private const string Prefisso = "ENC:";
    private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("GpOne.Desktop.v1");

    public static string? Cifra(string? testo) => string.IsNullOrEmpty(testo)
        ? null
        : Prefisso + Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(testo), Entropia, DataProtectionScope.CurrentUser));

    public static string? Decifra(string? valore)
    {
        if (string.IsNullOrEmpty(valore))
            return null;
        if (!valore.StartsWith(Prefisso, StringComparison.Ordinal))
            throw new InvalidOperationException("Valore non cifrato nel profilo: reinserirlo dalle impostazioni del profilo.");
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(valore[Prefisso.Length..]), Entropia, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw new InvalidOperationException(
                "Password o chiave API non leggibili: il profilo è stato creato da un altro utente Windows o su un altro computer. Reinserirle dalle impostazioni del profilo.", ex);
        }
    }
}
