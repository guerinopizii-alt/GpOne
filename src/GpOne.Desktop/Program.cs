namespace GpOne.Desktop;

internal static class Program
{
    /// <summary>Scelta del database (profilo) → finestra delle domande; dal menu si può tornare alla scelta.</summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Ui.Errore(null, e.Exception.Message);

        Guid? ultimo = null;
        while (true)
        {
            Profilo profilo;
            try
            {
                using var scelta = new ProfiliForm(ultimo);
                if (scelta.ShowDialog() != DialogResult.OK || scelta.Scelto is null)
                    return;
                profilo = scelta.Scelto;
            }
            catch (InvalidOperationException ex)
            {
                // es. profili.json danneggiato
                Ui.Errore(null, ex.Message);
                return;
            }

            ultimo = profilo.Id;
            using var principale = new MainForm(profilo);
            Application.Run(principale);
            if (principale.Esito != EsitoPrincipale.CambiaProfilo)
                return;
        }
    }
}

public enum EsitoPrincipale
{
    /// <summary>Finestra chiusa: si esce dall'applicazione.</summary>
    Chiudi,
    /// <summary>Torna alla scelta del profilo.</summary>
    CambiaProfilo
}
