using System.Text;

namespace GpOne.Core;

/// <summary>
/// Comando da riga di comando per cifrare una password (API: --cifra-password, agente: --cifra). Forme ammesse:
/// <list type="bullet">
/// <item><c>comando testo</c>: stampa il valore "ENC:..." (la password resta nella cronologia dei comandi);</item>
/// <item><c>comando</c>: chiede la password senza mostrarla (con conferma) e stampa il valore;</item>
/// <item><c>comando --file percorso</c>: come sopra ma scrive il valore nel file (usato dai CifraPassword.bat).</item>
/// </list>
/// Domande e messaggi vanno su stderr: su stdout esce solo il valore cifrato.
/// </summary>
public static class ComandoCifra
{
    public static bool Richiesto(string[] args, string comando) => args.Length > 0 && args[0] == comando;

    /// <returns>Codice di uscita: 0 se il valore è stato generato.</returns>
    public static int Esegui(string[] args, Func<string, string> cifra)
    {
        string? file = null, testo = null;
        switch (args)
        {
            case [_]:
                break;
            case [_, "--file", var percorso]:
                file = percorso;
                break;
            case [_, var valore] when !valore.StartsWith("--", StringComparison.Ordinal):
                testo = valore;
                break;
            default:
                Console.Error.WriteLine($"Uso: {args[0]} [password] | {args[0]} --file <percorso>");
                return 2;
        }

        testo ??= ChiediPassword();
        if (testo is null)
            return 1;

        var cifrato = cifra(testo);
        if (file is null)
        {
            Console.WriteLine(cifrato);
            return 0;
        }

        File.WriteAllText(file, cifrato + Environment.NewLine, new UTF8Encoding(false));
        Console.Error.WriteLine($"Valore cifrato scritto in {Path.GetFullPath(file)}");
        return 0;
    }

    private static string? ChiediPassword()
    {
        var password = LeggiNascosta("Password da cifrare: ");
        if (string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("Password vuota: nessun valore generato.");
            return null;
        }
        if (!Console.IsInputRedirected && LeggiNascosta("Ripeti la password: ") != password)
        {
            Console.Error.WriteLine("Le due password non coincidono: nessun valore generato.");
            return null;
        }
        return password;
    }

    private static string LeggiNascosta(string domanda)
    {
        Console.Error.Write(domanda);
        if (Console.IsInputRedirected)
            return Console.In.ReadLine() ?? string.Empty;

        var testo = new StringBuilder();
        while (true)
        {
            var tasto = Console.ReadKey(intercept: true);
            if (tasto.Key == ConsoleKey.Enter)
                break;
            if (tasto.Key == ConsoleKey.Backspace)
            {
                if (testo.Length > 0)
                {
                    testo.Length--;
                    Console.Error.Write("\b \b");
                }
                continue;
            }
            if (!char.IsControl(tasto.KeyChar))
            {
                testo.Append(tasto.KeyChar);
                Console.Error.Write('*');
            }
        }
        Console.Error.WriteLine();
        return testo.ToString();
    }
}
