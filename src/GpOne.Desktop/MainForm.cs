namespace GpOne.Desktop
{
    /// <summary>
    /// Domande in linguaggio naturale sul database del profilo: a sinistra la conversazione e lo storico,
    /// a destra la risposta (spiegazione, tabella, grafico, SQL), in basso la domanda.
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly ServizioDomande servizio;
        private CancellationTokenSource? annullamento;

        public EsitoPrincipale Esito { get; private set; } = EsitoPrincipale.Chiudi;

        public MainForm(Profilo profilo)
        {
            servizio = new ServizioDomande(profilo);
            InitializeComponent();
            Tema.Applica(this);
            Tema.StileGriglia(griglia);
            griglia.DataBindingComplete += (_, _) => griglia.ClearSelection();
            benvenuto.SuggerimentoScelto += async (_, suggerimento) =>
            {
                testo.Text = suggerimento;
                await ChiediAsync();
            };

            AggiornaProfilo();
            MostraTurno(null);
        }

        /// <summary>Testi che dipendono dal profilo (nome, database, invio di dati anonimizzati).</summary>
        private void AggiornaProfilo()
        {
            var p = servizio.Profilo;
            Text = $"GpOne – {p.Nome}";
            intestazione.Sottotitolo = string.IsNullOrWhiteSpace(p.Database) ? p.Nome : $"{p.Nome} · {p.Database}";
            benvenuto.NomeDatabase = p.Nome;
            benvenuto.Privacy = privacy.Text = p.Ai.InvioDatiAnonimizzati
                ? "L'AI vede la struttura del database: se servono dati, le vengono inviati solo anonimizzati."
                : "L'AI vede solo la struttura del database: i dati non le vengono mai inviati.";
            testo.PlaceholderText = $"Chiedi qualcosa su {p.Nome}…   (Invio per inviare, Maiusc+Invio per andare a capo)";
        }

        private bool Occupato => annullamento is not null;

        // ---- Eventi ----

        private void MainForm_Shown(object? sender, EventArgs e)
        {
            AggiornaStorico();
            if (Archivio.Struttura(servizio.Profilo.Id).Count(o => o.Abilitato) == 0)
            {
                Ui.Info(this, "Prima di fare domande bisogna leggere la struttura del database e abilitare le tabelle che l'AI può usare.");
                ApriStruttura();
            }
            testo.Focus();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e) => annullamento?.Cancel();

        private void CambiaDatabaseMenu_Click(object? sender, EventArgs e)
        {
            Esito = EsitoPrincipale.CambiaProfilo;
            Close();
        }

        private void ConnessioneMenu_Click(object? sender, EventArgs e) => ModificaProfilo();
        private void StrutturaMenu_Click(object? sender, EventArgs e) => ApriStruttura();
        private void EsciMenu_Click(object? sender, EventArgs e) => Close();
        private void NuovaConversazioneMenu_Click(object? sender, EventArgs e) => NuovaConversazione();
        private void AggiornaStoricoMenu_Click(object? sender, EventArgs e) => AggiornaStorico();

        private void ElencoTurni_SelectedIndexChanged(object? sender, EventArgs e) => MostraTurno(elencoTurni.SelectedItem as Turno);

        private async void ElencoStorico_DoubleClick(object? sender, EventArgs e) => await ApriDaStoricoAsync();

        private async void ElencoStorico_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                await ApriDaStoricoAsync();
        }

        private async void Testo_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                await ChiediAsync();
            }
        }

        private async void Invia_Click(object? sender, EventArgs e) => await ChiediAsync();
        private void Annulla_Click(object? sender, EventArgs e) => annullamento?.Cancel();
        private async void Excel_Click(object? sender, EventArgs e) => await EsportaAsync("xlsx");
        private async void Csv_Click(object? sender, EventArgs e) => await EsportaAsync("csv");

        /// <summary>Le etichette agganciate in alto vanno a capo entro la larghezza del pannello.</summary>
        private void ContenutoRisposta_Resize(object? sender, EventArgs e)
        {
            spiegazione.MaximumSize = new Size(Math.Max(100, contenutoRisposta.ClientSize.Width - 8), 0);
            AdattaAltezzaErrore();
        }

        /// <summary>Il riquadro dell'errore si allunga con il testo (a capo), fino a un massimo oltre il quale scorre.</summary>
        private void AdattaAltezzaErrore()
        {
            if (errore.Text.Length == 0)
                return;
            var larghezza = Math.Max(100, pannelloErrore.ClientSize.Width - pannelloErrore.Padding.Horizontal
                                          - copiaErrore.Width - SystemInformation.VerticalScrollBarWidth);
            var misura = TextRenderer.MeasureText(errore.Text, errore.Font, new Size(larghezza, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            var altezza = Math.Clamp(misura.Height + 6, errore.Font.Height + 6, 220);
            errore.ScrollBars = misura.Height + 6 > 220 ? ScrollBars.Vertical : ScrollBars.None;
            pannelloErrore.Height = Math.Max(altezza, copiaErrore.PreferredSize.Height) + pannelloErrore.Padding.Vertical;
        }

        private void CopiaErrore_Click(object? sender, EventArgs e)
        {
            var turno = elencoTurni.SelectedItem as Turno;
            var testo = turno?.ErroreDettagli is { } dettagli
                ? $"{errore.Text}\r\n\r\n--- Dettagli ---\r\n{dettagli}"
                : errore.Text;
            try
            {
                Clipboard.SetText(testo.ReplaceLineEndings("\r\n"));
                stato.Text = "Errore copiato negli appunti.";
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                stato.Text = "Appunti non disponibili.";
            }
        }

        // ---- Domande ----

        private Task ChiediAsync()
        {
            var domandaTesto = testo.Text.Trim();
            if (domandaTesto.Length == 0 || Occupato)
                return Task.CompletedTask;
            testo.Clear();
            return EseguiAsync(new Turno(domandaTesto), ct => servizio.ChiediAsync(domandaTesto, ct));
        }

        private async Task ApriDaStoricoAsync()
        {
            if (elencoStorico.SelectedItem is not DomandaSalvata voce || Occupato)
                return;
            schedeSinistra.SelectedTab = schedaConversazione;
            await EseguiAsync(new Turno(voce.Testo), ct => servizio.ApriAsync(voce.Id, ct));
        }

        private async Task RiprovaAsync(Turno turno)
        {
            if (turno.Risposta is not { } precedente || Occupato)
                return;
            using var dialogo = new TestoForm("Risposta sbagliata",
                "Cosa c'è di sbagliato? Il commento va all'AI insieme alla query, mai i risultati.",
                "es. il fatturato va calcolato senza le note di credito");
            if (dialogo.ShowDialog(this) != DialogResult.OK)
                return;
            var commento = dialogo.Valore;
            await EseguiAsync(new Turno(precedente.Domanda, string.IsNullOrEmpty(commento) ? "Risposta sbagliata, riprova." : commento),
                ct => servizio.RiprovaAsync(precedente.Id, commento, ct));
        }

        private async Task RispondiChiarimentoAsync(Turno turno)
        {
            if (turno.Risposta is not { Chiarimento: { } chiarimento } precedente || Occupato)
                return;
            using var dialogo = new TestoForm("Chiarimento", chiarimento, "es. il 2025");
            if (dialogo.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialogo.Valore))
                return;
            var risposta = dialogo.Valore;
            await EseguiAsync(new Turno(precedente.Domanda, $"Chiarimento: {risposta}"),
                ct => servizio.RispondiChiarimentoAsync(precedente.Id, risposta, ct));
        }

        private async Task EseguiAsync(Turno turno, Func<CancellationToken, Task<RispostaDomanda>> chiamata)
        {
            elencoTurni.Items.Add(turno);
            schedeSinistra.SelectedTab = schedaConversazione;
            elencoTurni.SelectedItem = turno;

            annullamento = new CancellationTokenSource();
            AggiornaOccupato("Preparo la query e la eseguo sul database…");
            try
            {
                var token = annullamento.Token;
                turno.Risposta = await Task.Run(() => chiamata(token));
            }
            catch (Exception ex)
            {
                turno.Errore = Ui.Messaggio(ex);
                turno.ErroreDettagli = ex is OperationCanceledException ? null : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}";
            }
            finally
            {
                annullamento.Dispose();
                annullamento = null;
                AggiornaOccupato(null);
            }

            if (IsDisposed)
                return;
            AggiornaVoce(turno);
            if (elencoTurni.SelectedItem == turno)
                MostraTurno(turno);
            AggiornaStorico();
            testo.Focus();
        }

        private void Valuta(Turno turno, bool corretta)
        {
            if (turno.Risposta is not { } r)
                return;
            try
            {
                servizio.Valuta(r.Id, corretta);
                turno.Risposta = r with { Valutazione = corretta ? Valutazioni.Corretta : null };
                AggiornaVoce(turno);
                MostraTurno(turno);
                AggiornaStorico();
            }
            catch (Exception ex)
            {
                Ui.Errore(this, Ui.Messaggio(ex));
            }
        }

        private void NuovaConversazione()
        {
            if (Occupato)
                return;
            elencoTurni.Items.Clear();
            MostraTurno(null);
            testo.Focus();
        }

        private void AggiornaOccupato(string? messaggio)
        {
            if (IsDisposed)
                return;
            stato.Text = messaggio ?? "";
            invia.Enabled = !Occupato;
            annulla.Visible = Occupato;
            UseWaitCursor = Occupato;
            barraValutazione.Enabled = !Occupato;
            if (Occupato)
                MostraTurno(elencoTurni.SelectedItem as Turno);
        }

        private void AggiornaVoce(Turno turno)
        {
            var indice = elencoTurni.Items.IndexOf(turno);
            if (indice >= 0)
                elencoTurni.Items[indice] = turno; // ridisegna il testo (icona di stato)
        }

        private void AggiornaStorico()
        {
            try
            {
                var selezionato = (elencoStorico.SelectedItem as DomandaSalvata)?.Id;
                elencoStorico.BeginUpdate();
                elencoStorico.Items.Clear();
                foreach (var voce in servizio.Storico())
                    elencoStorico.Items.Add(voce);
                elencoStorico.SelectedItem = elencoStorico.Items.Cast<DomandaSalvata>().FirstOrDefault(v => v.Id == selezionato);
                elencoStorico.EndUpdate();
            }
            catch (Exception ex)
            {
                stato.Text = $"Storico non disponibile: {Ui.Messaggio(ex)}";
            }
        }

        // ---- Visualizzazione ----

        private void MostraTurno(Turno? turno)
        {
            benvenuto.Visible = turno is null;
            contenutoRisposta.Visible = turno is not null;
            if (turno is null)
                return;

            contenutoRisposta.SuspendLayout();
            if (turno.Correzione is { } correzione)
            {
                domanda.Etichetta = correzione.StartsWith("Chiarimento: ", StringComparison.Ordinal) ? "Chiarimento" : "Correzione";
                domanda.Text = correzione.StartsWith("Chiarimento: ", StringComparison.Ordinal) ? correzione["Chiarimento: ".Length..] : correzione;
            }
            else
            {
                domanda.Etichetta = "";
                domanda.Text = turno.Domanda;
            }

            var r = turno.Risposta;
            spiegazione.ForeColor = r?.Chiarimento is not null ? Tema.ViolaScuro : turno.InAttesa ? Tema.TestoSecondario : Tema.Testo;
            spiegazione.Font = r?.Chiarimento is not null ? Tema.Semigrassetto(11f) : Tema.Carattere(11f);
            spiegazione.Text = turno.InAttesa ? "Preparo la query e la eseguo sul database…"
                : r?.Chiarimento is { } chiarimento ? $"L'AI chiede: {chiarimento}"
                : r?.Spiegazione ?? "";
            spiegazione.Visible = spiegazione.Text.Length > 0;
            errore.Text = (turno.Errore ?? r?.Errore ?? "").ReplaceLineEndings("\r\n");
            pannelloErrore.Visible = errore.Text.Length > 0;
            AdattaAltezzaErrore();

            var conRisultato = r?.Sql is not null && r.Chiarimento is null;
            schedaRisultato.Visible = conRisultato;
            if (r is not null && conRisultato)
            {
                info.Text = $"{r.Righe.Count:N0} righe";
                troncato.Visible = r.Troncato;
                durata.Text = $"{r.DurataMs / 1000.0:N1} s";
                anonimi.Visible = r.DatiInviati;
                excel.Enabled = csv.Enabled = r.Colonne.Count > 0;
                sql.Text = r.Sql!.ReplaceLineEndings("\r\n");
                Presentazione.MostraTabella(griglia, r.Colonne, r.Righe);

                schedeRisposta.TabPages.Clear();
                if (r.Colonne.Count > 0)
                    schedeRisposta.TabPages.Add(schedaTabella);
                var conGrafico = r.Grafico is not null && r.Righe.Count > 0 && Presentazione.MostraGrafico(grafico, r.Grafico, r.Colonne, r.Righe);
                if (conGrafico)
                    schedeRisposta.TabPages.Add(schedaGrafico);
                schedeRisposta.TabPages.Add(schedaSql);
                schedeRisposta.SelectedTab = conGrafico ? schedaGrafico : schedeRisposta.TabPages[0];
            }

            MostraValutazione(turno);
            contenutoRisposta.ResumeLayout(true);
            contenutoRisposta.Invalidate(true);
        }

        /// <summary>Pulsanti di valutazione: cambiano a seconda dello stato della risposta, quindi si creano qui.</summary>
        private void MostraValutazione(Turno turno)
        {
            barraValutazione.Controls.Clear();
            if (turno.Risposta is not { } r)
                return;
            if (r.Chiarimento is not null)
            {
                barraValutazione.Controls.Add(Ui.Pulsante("Rispondi…", async (_, _) => await RispondiChiarimentoAsync(turno),
                    StilePulsante.Primario, Tema.Icona.Modifica));
                return;
            }
            if (r.Valutazione == Valutazioni.Corretta)
            {
                barraValutazione.Controls.Add(new Etichetta
                {
                    Text = "Confermata: diventa un esempio per le prossime domande", Icona = Tema.Icona.Conferma,
                    Variante = VarianteEtichetta.Ok, Margin = new Padding(0, 6, 10, 6)
                });
                barraValutazione.Controls.Add(Ui.Pulsante("Annulla conferma", (_, _) => Valuta(turno, false), StilePulsante.Testo));
                return;
            }
            barraValutazione.Controls.Add(new Label
            {
                Text = "È corretta?", AutoSize = true, BackColor = Color.Transparent, ForeColor = Tema.TestoSecondario,
                Font = Tema.Carattere(10f), Margin = new Padding(0, 10, 10, 0)
            });
            if (r.Confermabile)
                barraValutazione.Controls.Add(Ui.Pulsante("Sì, confermala", (_, _) => Valuta(turno, true), StilePulsante.Secondario, Tema.Icona.MiPiace));
            barraValutazione.Controls.Add(Ui.Pulsante("No, correggila…", async (_, _) => await RiprovaAsync(turno), StilePulsante.Pericolo, Tema.Icona.NonMiPiace));
        }

        // ---- Export, profilo, struttura ----

        private async Task EsportaAsync(string formato)
        {
            if ((elencoTurni.SelectedItem as Turno)?.Risposta is not { Risultato: not null } r)
                return;
            using var dialogo = new SaveFileDialog
            {
                FileName = $"GpOne_{Path.GetInvalidFileNameChars().Aggregate(servizio.Profilo.Nome, (s, c) => s.Replace(c, '_'))}_{DateTime.Now:yyyyMMdd_HHmm}.{formato}",
                Filter = formato == "xlsx" ? "Cartella di lavoro Excel (*.xlsx)|*.xlsx" : "CSV separato da punto e virgola (*.csv)|*.csv"
            };
            if (dialogo.ShowDialog(this) != DialogResult.OK)
                return;
            try
            {
                UseWaitCursor = true;
                await Task.Run(() =>
                {
                    if (formato == "xlsx")
                        Presentazione.EsportaExcel(dialogo.FileName, r.Domanda, r.Colonne, r.Righe);
                    else
                        Presentazione.EsportaCsv(dialogo.FileName, r.Colonne, r.Righe);
                });
                stato.Text = $"Esportato in {dialogo.FileName}";
            }
            catch (Exception ex)
            {
                Ui.Errore(this, $"Esportazione non riuscita: {ex.Message}");
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void ModificaProfilo()
        {
            using var form = new ProfiloForm(servizio.Profilo);
            if (form.ShowDialog(this) != DialogResult.OK)
                return;
            var profili = Archivio.Profili();
            var indice = profili.FindIndex(p => p.Id == servizio.Profilo.Id);
            if (indice >= 0)
                profili[indice] = servizio.Profilo;
            else
                profili.Add(servizio.Profilo);
            Archivio.SalvaProfili(profili);
            AggiornaProfilo();
        }

        private void ApriStruttura()
        {
            using var form = new StrutturaForm(servizio.Profilo);
            form.ShowDialog(this);
        }
    }
}
