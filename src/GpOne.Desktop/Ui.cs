namespace GpOne.Desktop;

/// <summary>Messaggi all'utente e controlli creati a runtime (le finestre sono disegnate con il Designer).</summary>
internal static class Ui
{
    /// <summary>Pulsante creato a runtime (es. quelli di valutazione, che cambiano a seconda della risposta).</summary>
    public static Pulsante Pulsante(string testo, EventHandler click, StilePulsante stile = StilePulsante.Secondario, string? icona = null)
    {
        var b = new Pulsante { Text = testo, AutoSize = true, Stile = stile, Icona = icona ?? "", Margin = new Padding(0, 0, 8, 0) };
        b.Click += click;
        return b;
    }

    /// <summary>Errore in una casella di testo copiabile; <paramref name="dettagli"/> (es. l'eccezione completa) sotto il messaggio.</summary>
    public static void Errore(IWin32Window? owner, string messaggio, string? dettagli = null) =>
        Mostra(owner, string.IsNullOrWhiteSpace(dettagli) ? messaggio : $"{messaggio}\n\n--- Dettagli ---\n{dettagli}", errore: true);

    public static void Info(IWin32Window? owner, string messaggio) => Mostra(owner, messaggio, errore: false);

    private static void Mostra(IWin32Window? owner, string messaggio, bool errore)
    {
        using var finestra = new MessaggioForm(messaggio, errore);
        if (owner is null)
        {
            finestra.StartPosition = FormStartPosition.CenterScreen;
            finestra.ShowDialog();
        }
        else
            finestra.ShowDialog(owner);
    }

    public static bool Conferma(IWin32Window? owner, string messaggio) =>
        MessageBox.Show(owner, messaggio, "GpOne", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    /// <summary>Messaggio leggibile per l'utente.</summary>
    public static string Messaggio(Exception ex) => ex switch
    {
        OperationCanceledException => "Operazione annullata.",
        _ => ex.Message
    };
}
