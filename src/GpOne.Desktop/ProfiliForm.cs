namespace GpOne.Desktop
{
    /// <summary>Scelta del database su cui fare domande (un profilo per database/cliente).</summary>
    public partial class ProfiliForm : Form
    {
        private readonly List<Profilo> profili = [];

        public Profilo? Scelto { get; private set; }

        public ProfiliForm(Guid? ultimo)
        {
            InitializeComponent();
            Tema.Applica(this);
            titolo.Font = Tema.Titolo(20f);
            logo.Sottotitolo = "Chiedi ai tuoi dati";
            contenuto.Resize += (_, _) => AdattaTesti();
            AdattaTesti();
            profili = Archivio.Profili();
            Aggiorna(profili.FirstOrDefault(p => p.Id == ultimo) ?? profili.FirstOrDefault());
        }

        private void ProfiliForm_Shown(object? sender, EventArgs e)
        {
            if (profili.Count == 0)
                Nuovo();
            elenco.Focus();
        }

        private void Elenco_DoubleClick(object? sender, EventArgs e) => Apri();
        private void Elenco_SelectedIndexChanged(object? sender, EventArgs e) => AggiornaComandi();
        private void Apri_Click(object? sender, EventArgs e) => Apri();
        private void Nuovo_Click(object? sender, EventArgs e) => Nuovo();
        private void Modifica_Click(object? sender, EventArgs e) => Modifica();
        private void Elimina_Click(object? sender, EventArgs e) => Elimina();
        private void Esci_Click(object? sender, EventArgs e) => Close();
        private void VoceDatabase_Click(object? sender, EventArgs e) => elenco.Focus();

        private void Struttura_Click(object? sender, EventArgs e)
        {
            if (elenco.SelectedItem is not Profilo profilo)
                return;
            using var form = new StrutturaForm(profilo);
            form.ShowDialog(this);
        }

        /// <summary>Titolo e sottotitolo vanno a capo entro la larghezza dell'area.</summary>
        private void AdattaTesti()
        {
            var massima = new Size(Math.Max(200, contenuto.ClientSize.Width - contenuto.Padding.Horizontal), 0);
            titolo.MaximumSize = sottotitolo.MaximumSize = massima;
        }

        /// <summary>Le azioni sul database scelto si abilitano solo con un database selezionato.</summary>
        private void AggiornaComandi()
        {
            var scelto = elenco.SelectedItem is Profilo;
            modifica.Enabled = struttura.Enabled = elimina.Enabled = apri.Enabled = scelto;
        }

        private void Aggiorna(Profilo? selezionato)
        {
            elenco.BeginUpdate();
            elenco.Items.Clear();
            foreach (var p in profili.OrderBy(p => p.Nome, StringComparer.CurrentCultureIgnoreCase))
                elenco.Items.Add(p);
            elenco.SelectedItem = selezionato;
            elenco.EndUpdate();
            AggiornaComandi();
        }

        private void Nuovo()
        {
            var profilo = new Profilo();
            using var form = new ProfiloForm(profilo);
            if (form.ShowDialog(this) != DialogResult.OK)
                return;
            profili.Add(profilo);
            Salva(profilo);
        }

        private void Modifica()
        {
            if (elenco.SelectedItem is not Profilo profilo)
                return;
            using var form = new ProfiloForm(profilo);
            if (form.ShowDialog(this) == DialogResult.OK)
                Salva(profilo);
        }

        private void Elimina()
        {
            if (elenco.SelectedItem is not Profilo profilo ||
                !Ui.Conferma(this, $"Eliminare il profilo \"{profilo.Nome}\" con la sua struttura e lo storico delle domande?"))
                return;
            profili.Remove(profilo);
            Archivio.SalvaProfili(profili);
            Archivio.EliminaCartellaProfilo(profilo.Id);
            Aggiorna(profili.FirstOrDefault());
        }

        private void Salva(Profilo selezionato)
        {
            try
            {
                Archivio.SalvaProfili(profili);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Ui.Errore(this, $"Profilo non salvato: {ex.Message}");
            }
            Aggiorna(selezionato);
        }

        private void Apri()
        {
            if (elenco.SelectedItem is not Profilo profilo)
                return;
            Scelto = profilo;
            DialogResult = DialogResult.OK;
        }
    }
}
