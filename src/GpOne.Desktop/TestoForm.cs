namespace GpOne.Desktop
{
    /// <summary>Richiesta di un testo libero (es. il commento a una risposta sbagliata). Ctrl+Invio conferma.</summary>
    public partial class TestoForm : Form
    {
        public string Valore => campo.Text.Trim();

        public TestoForm(string titolo, string domanda, string esempio)
        {
            InitializeComponent();
            Tema.Applica(this);
            Text = titolo;
            this.domanda.Text = domanda;
            campo.PlaceholderText = esempio;
        }

        private void Campo_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && e.Control)
            {
                e.SuppressKeyPress = true;
                DialogResult = DialogResult.OK;
            }
        }
    }
}
