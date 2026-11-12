namespace GpOne.Desktop
{
    /// <summary>
    /// Messaggio (errore o informazione) in una casella di testo a sola lettura che va a capo:
    /// si può selezionare e copiare (anche tutto con il pulsante Copia).
    /// </summary>
    public partial class MessaggioForm : Form
    {
        /// <param name="errore">true = errore (icona e titolo d'errore), false = informazione.</param>
        public MessaggioForm(string messaggio, bool errore)
        {
            InitializeComponent();
            Tema.Applica(this);
            icona.Image = (errore ? SystemIcons.Error : SystemIcons.Information).ToBitmap();
            titolo.Text = errore ? "Si è verificato un errore" : "Informazione";
            // La TextBox va a capo solo con \r\n
            testo.Text = messaggio.ReplaceLineEndings("\r\n");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Niente testo selezionato all'apertura, cursore sul pulsante OK
            testo.Select(0, 0);
            ok.Focus();
        }

        private void Copia_Click(object? sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(testo.Text);
                copia.Text = "Copiato";
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                copia.Text = "Appunti non disponibili";
            }
        }
    }
}
