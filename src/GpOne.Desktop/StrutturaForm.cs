using GpOne.Motore;

namespace GpOne.Desktop
{
    /// <summary>
    /// Struttura del database: si rilegge da INFORMATION_SCHEMA, si abilitano tabelle/viste e colonne e si scrivono
    /// le descrizioni. Solo ciò che è abilitato (con le descrizioni) finisce nel prompt dell'AI.
    /// </summary>
    public partial class StrutturaForm : Form
    {
        private readonly Profilo profilo;
        private List<OggettoConfigurato> oggetti;
        private bool inAggiornamento;
        private bool modificato;

        public StrutturaForm(Profilo profilo)
        {
            this.profilo = profilo;
            oggetti = Archivio.Struttura(profilo.Id);
            InitializeComponent();
            Tema.Applica(this);
            Text = $"Struttura del database – {profilo.Nome}";
            Popola();
        }

        private async void StrutturaForm_Shown(object? sender, EventArgs e)
        {
            if (oggetti.Count == 0 && Ui.Conferma(this, "La struttura di questo database non è ancora stata letta. Leggerla ora?"))
                await RileggiAsync();
        }

        private void StrutturaForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK && modificato && !Ui.Conferma(this, "Le modifiche alla struttura non sono state salvate. Uscire comunque?"))
                e.Cancel = true;
        }

        private void Filtro_Changed(object? sender, EventArgs e) => Popola();
        private void Albero_AfterSelect(object? sender, TreeViewEventArgs e) => MostraSelezione();
        private async void Rileggi_Click(object? sender, EventArgs e) => await RileggiAsync();
        private void Salva_Click(object? sender, EventArgs e) => Salva();

        private void Popola()
        {
            inAggiornamento = true;
            albero.BeginUpdate();
            albero.Nodes.Clear();
            var testo = filtro.Text.Trim();
            var nomeTabella = filtroTabelle.Text.Trim();
            foreach (var o in oggetti)
            {
                if (soloAbilitati.Checked && !o.Abilitato)
                    continue;
                // Filtro sul solo nome della tabella/vista (con o senza schema), in aggiunta a quello su tabelle e colonne
                if (nomeTabella.Length > 0 && !$"{o.SchemaNome}.{o.Nome}".Contains(nomeTabella, StringComparison.OrdinalIgnoreCase))
                    continue;
                var corrispondeOggetto = testo.Length == 0 || $"{o.SchemaNome}.{o.Nome}".Contains(testo, StringComparison.OrdinalIgnoreCase);
                var colonne = o.Colonne.Where(c => corrispondeOggetto || c.Nome.Contains(testo, StringComparison.OrdinalIgnoreCase)).ToList();
                if (!corrispondeOggetto && colonne.Count == 0)
                    continue;

                var nodo = new TreeNode(TestoOggetto(o)) { Tag = o, Checked = o.Abilitato };
                foreach (var c in colonne)
                    nodo.Nodes.Add(new TreeNode(TestoColonna(c)) { Tag = c, Checked = c.Abilitata, ForeColor = o.Abilitato ? SystemColors.WindowText : SystemColors.GrayText });
                if (testo.Length > 0 && !corrispondeOggetto)
                    nodo.Expand();
                albero.Nodes.Add(nodo);
            }
            albero.EndUpdate();
            inAggiornamento = false;
            AggiornaRiepilogo();
            MostraSelezione();
        }

        private static string TestoOggetto(OggettoConfigurato o) =>
            $"{o.SchemaNome}.{o.Nome}{(o.Tipo == "VISTA" ? "  (vista)" : "")}{(string.IsNullOrWhiteSpace(o.Descrizione) ? "" : "  – " + o.Descrizione)}";

        private static string TestoColonna(ColonnaConfigurata c) =>
            $"{c.Nome}  {c.TipoDati}{(string.IsNullOrWhiteSpace(c.Descrizione) ? "" : "  – " + c.Descrizione)}";

        private void Albero_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (inAggiornamento || e.Node is null)
                return;
            switch (e.Node.Tag)
            {
                case OggettoConfigurato o:
                    o.Abilitato = e.Node.Checked;
                    foreach (TreeNode figlio in e.Node.Nodes)
                        figlio.ForeColor = o.Abilitato ? SystemColors.WindowText : SystemColors.GrayText;
                    break;
                case ColonnaConfigurata c:
                    c.Abilitata = e.Node.Checked;
                    // Abilitare una colonna di una tabella disabilitata abilita anche la tabella
                    if (c.Abilitata && e.Node.Parent is { Tag: OggettoConfigurato { Abilitato: false } } padre)
                        padre.Checked = true;
                    break;
            }
            modificato = true;
            AggiornaRiepilogo();
        }

        private void MostraSelezione()
        {
            inAggiornamento = true;
            switch (albero.SelectedNode?.Tag)
            {
                case OggettoConfigurato o:
                    selezione.Text = $"{o.SchemaNome}.{o.Nome}\n{(o.Tipo == "VISTA" ? "Vista" : "Tabella")}, {o.Colonne.Count} colonne";
                    descrizione.Text = o.Descrizione;
                    descrizione.Enabled = true;
                    break;
                case ColonnaConfigurata c:
                    selezione.Text = $"{c.Nome}\n{c.TipoDati}";
                    descrizione.Text = c.Descrizione;
                    descrizione.Enabled = true;
                    break;
                default:
                    selezione.Text = "Seleziona una tabella o una colonna";
                    descrizione.Text = "";
                    descrizione.Enabled = false;
                    break;
            }
            inAggiornamento = false;
        }

        private void Descrizione_TextChanged(object? sender, EventArgs e)
        {
            if (inAggiornamento || albero.SelectedNode is not { } nodo)
                return;
            var testo = string.IsNullOrWhiteSpace(descrizione.Text) ? null : descrizione.Text.Trim();
            switch (nodo.Tag)
            {
                case OggettoConfigurato o:
                    o.Descrizione = testo;
                    nodo.Text = TestoOggetto(o);
                    break;
                case ColonnaConfigurata c:
                    c.Descrizione = testo;
                    nodo.Text = TestoColonna(c);
                    break;
            }
            modificato = true;
        }

        private void AggiornaRiepilogo()
        {
            var abilitati = oggetti.Where(o => o.Abilitato).ToList();
            riepilogo.Text = $"{abilitati.Count} di {oggetti.Count} tabelle/viste abilitate, {abilitati.Sum(o => o.Colonne.Count(c => c.Abilitata))} colonne";
        }

        private async Task RileggiAsync()
        {
            UseWaitCursor = true;
            Enabled = false;
            try
            {
                oggetti = await ServizioDomande.AllineaStrutturaAsync(profilo, oggetti);
                modificato = true;
                Popola();
            }
            catch (Exception ex)
            {
                Ui.Errore(this, $"Lettura della struttura non riuscita: {Ui.Messaggio(ex)}");
            }
            finally
            {
                Enabled = true;
                UseWaitCursor = false;
            }
        }

        private void Salva()
        {
            try
            {
                Archivio.SalvaStruttura(profilo.Id, oggetti);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Ui.Errore(this, $"Struttura non salvata: {ex.Message}");
            }
        }
    }
}
