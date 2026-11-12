using System.Security.Cryptography;
using System.Text;

namespace GpOne.Agente;

/// <summary>Segreti in appsettings.json come "ENC:...", cifrati con DPAPI a livello macchina.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class Segreti
{
    private const string Prefisso = "ENC:";
    private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("GpOne.Agente.v1");

    public static string Cifra(string testo) =>
        Prefisso + Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(testo), Entropia, DataProtectionScope.LocalMachine));

    public static string Decifra(string valore)
    {
        if (!valore.StartsWith(Prefisso, StringComparison.Ordinal))
            return valore;
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(valore[Prefisso.Length..]), Entropia, DataProtectionScope.LocalMachine));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw new InvalidOperationException("Valore cifrato non valido: è stato cifrato su un altro computer? Rigenerarlo con --cifra.", ex);
        }
    }
}
