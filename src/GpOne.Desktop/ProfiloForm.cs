using GpOne.Motore;

namespace GpOne.Desktop
{
    /// <summary>Collegamento al database del cliente e motore AI di un profilo.</summary>
    public partial class ProfiloForm : Form
    {
        private readonly Profilo profilo;

        /// <summary>JSON dell'account di servizio Vertex appena caricato, già cifrato (null = nessun file nuovo).</summary>
        private string? accountVertexCifrato;

        /// <param name="profilo">Profilo da modificare; viene aggiornato solo con OK.</param>
        public ProfiloForm(Profilo profilo)
        {
            this.profilo = profilo;
            InitializeComponent();
            Tema.Applica(this);

            Text = string.IsNullOrEmpty(profilo.Nome) ? "Nuovo profilo" : $"Profilo {profilo.Nome}";
            nome.Text = profilo.Nome;
            server.Text = profilo.Server;
            database.Text = profilo.Database;
            windows.Checked = profilo.AutenticazioneWindows;
            utente.Text = profilo.Utente;
            password.PlaceholderText = profilo.PasswordCifrata is null ? "" : "(invariata)";
            certificato.Checked = profilo.CertificatoAttendibile;
            providerMostrato = profilo.Ai.Provider;
            provider.SelectedItem = profilo.Ai.Provider;
            modello.Text = profilo.Ai.Modello;
            endpoint.Text = profilo.Ai.Endpoint;
            if (profilo.Ai.Provider == ProviderAi.GeminiVertex && profilo.ApiKeyCifrata is not null)
                MostraAccount(profilo.ApiKeyCifrata);
            effort.SelectedItem = profilo.Ai.Effort ?? "medium";
            istruzioni.Text = profilo.Ai.Istruzioni;
            maxRighe.Value = Math.Clamp(profilo.Ai.MaxRighe, ConfigurazioneAi.MinMaxRighe, ConfigurazioneAi.MaxMaxRighe);
            timeout.Value = Math.Clamp(profilo.Ai.TimeoutSecondi, ConfigurazioneAi.MinTimeout, ConfigurazioneAi.MaxTimeout);
            anonimizzati.Checked = profilo.Ai.InvioDatiAnonimizzati;
            maxChiamate.Value = Math.Clamp(profilo.Ai.MaxChiamateAi, ConfigurazioneAi.MinChiamateAi, ConfigurazioneAi.MaxMaxChiamateAi);
            righeAnonimizzate.Value = Math.Clamp(profilo.Ai.MaxRigheAnonimizzate, ConfigurazioneAi.MinRigheAnonimizzate, ConfigurazioneAi.MaxMaxRigheAnonimizzate);
            AggiornaCampi();
        }

        /// <summary>Motore a cui si riferiscono il modello e l'elenco mostrati (cambiando motore si svuotano).</summary>
        private string? providerMostrato;

        private void Campi_Changed(object? sender, EventArgs e) => AggiornaCampi();

        private async void CaricaModelli_Click(object? sender, EventArgs e) => await CaricaModelliAsync();

        /// <summary>Segreto in chiaro da usare per il motore scelto: quello appena inserito o quello salvato per lo stesso motore.</summary>
        private string? SegretoCorrente()
        {
            var p = ProviderScelto;
            if (p is ProviderAi.Claude or ProviderAi.Gemini && chiave.Text.Trim().Length > 0)
                return chiave.Text.Trim();
            if (p == ProviderAi.GeminiVertex && accountVertexCifrato is not null)
                return Segreti.Decifra(accountVertexCifrato);
            return SegretoSalvatoValido ? Segreti.Decifra(profilo.ApiKeyCifrata!) : null;
        }

        /// <summary>Si possono caricare i modelli se c'è la chiave (o l'account di servizio, o l'indirizzo di Ollama).</summary>
        private bool PuoCaricareModelli => ProviderScelto switch
        {
            ProviderAi.Ollama => endpoint.Text.Trim().Length > 0,
            ProviderAi.GeminiVertex => accountVertexCifrato is not null || SegretoSalvatoValido,
            _ => chiave.Text.Trim().Length > 0 || SegretoSalvatoValido
        };

        private async Task CaricaModelliAsync()
        {
            var p = ProviderScelto;
            var configurazione = new ConfigurazioneAi
            {
                Provider = p,
                Endpoint = string.IsNullOrWhiteSpace(endpoint.Text) ? null : endpoint.Text.Trim()
            };
            UseWaitCursor = true;
            caricaModelli.Enabled = false;
            try
            {
                configurazione.ApiKey = ProviderAi.RichiedeChiave(p) ? SegretoCorrente() : null;
                var elenco = await ServizioDomande.CaricaModelliAsync(configurazione);
                if (elenco.Count == 0)
                {
                    Ui.Info(this, $"Nessun modello utilizzabile trovato per {p}.");
                    return;
                }
                var attuale = modello.Text.Trim();
                modello.BeginUpdate();
                modello.Items.Clear();
                modello.Items.AddRange([.. elenco]);
                modello.EndUpdate();
                var trovato = elenco.FirstOrDefault(m => string.Equals(m, attuale, StringComparison.OrdinalIgnoreCase));
                modello.SelectedItem = trovato ?? elenco[0];
                modello.DroppedDown = trovato is null;
            }
            catch (Exception ex)
            {
                // Dettagli completi, copiabili, da passare a chi fa assistenza (mai la chiave: non è nelle eccezioni)
                Ui.Errore(this, "Impossibile caricare i modelli: " + Ui.Messaggio(ex),
                    $"Carica modelli ({p}, {DateTime.Now:yyyy-MM-dd HH:mm:ss})\n{ex}");
            }
            finally
            {
                UseWaitCursor = false;
                caricaModelli.Enabled = PuoCaricareModelli;
            }
        }

        private async void Prova_Click(object? sender, EventArgs e) => await ProvaAsync();

        private void Ok_Click(object? sender, EventArgs e) => Conferma();

        private string ProviderScelto => (string?)provider.SelectedItem ?? ProviderAi.Claude;

        /// <summary>
        /// Il segreto salvato vale solo per il motore con cui è stato inserito: cambiando motore
        /// (es. da Claude a Gemini) va inserita la chiave nuova.
        /// </summary>
        private bool SegretoSalvatoValido => profilo.ApiKeyCifrata is not null && profilo.Ai.Provider == ProviderScelto;

        /// <summary>Mostra solo i campi del motore scelto.</summary>
        private void AggiornaCampi()
        {
            utente.Enabled = password.Enabled = !windows.Checked;

            var p = ProviderScelto;
            var ollama = p == ProviderAi.Ollama;
            var vertex = p == ProviderAi.GeminiVertex;
            lblEndpoint.Visible = endpoint.Visible = ollama || vertex;
            lblEndpoint.Text = vertex ? "Regione Vertex" : "Indirizzo Ollama";
            endpoint.PlaceholderText = vertex ? "es. europe-west8 (vuoto = global)" : "http://localhost:11434";
            lblChiave.Visible = chiave.Visible = p is ProviderAi.Claude or ProviderAi.Gemini;
            lblAccount.Visible = pannelloAccount.Visible = vertex;
            lblEffort.Visible = effort.Visible = p == ProviderAi.Claude;
            chiave.PlaceholderText = SegretoSalvatoValido
                ? "(impostata: lasciare vuoto per non cambiarla)"
                : p == ProviderAi.Gemini ? "chiave di Google AI Studio" : "sk-ant-...";
            if (vertex && accountVertexCifrato is null && !SegretoSalvatoValido)
                infoAccount.Text = "non impostato";

            // Modello ed elenco caricato valgono solo per il motore con cui sono stati scelti
            if (providerMostrato != p)
            {
                modello.Items.Clear();
                modello.Text = p == profilo.Ai.Provider ? profilo.Ai.Modello : "";
                providerMostrato = p;
            }
            caricaModelli.Enabled = PuoCaricareModelli;
        }

        private void CaricaAccount_Click(object? sender, EventArgs e)
        {
            using var dialogo = new OpenFileDialog
            {
                Title = "Chiave JSON dell'account di servizio Google Cloud",
                Filter = "Chiave account di servizio (*.json)|*.json"
            };
            if (dialogo.ShowDialog(this) != DialogResult.OK)
                return;
            try
            {
                var json = File.ReadAllText(dialogo.FileName);
                AccountServizioGoogle.Leggi(json); // controlla che sia un account di servizio valido
                accountVertexCifrato = Segreti.Cifra(json);
                MostraAccount(accountVertexCifrato!);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Ui.Errore(this, ex.Message);
            }
        }

        /// <summary>Mostra account e progetto (mai la chiave privata).</summary>
        private void MostraAccount(string jsonCifrato)
        {
            try
            {
                var account = AccountServizioGoogle.Leggi(Segreti.Decifra(jsonCifrato)!);
                infoAccount.Text = $"{account.Email} (progetto {account.ProgettoId})";
            }
            catch (InvalidOperationException ex)
            {
                infoAccount.Text = ex.Message;
            }
        }

        /// <summary>Copia dei dati inseriti in un profilo (la password nuova, se scritta, viene cifrata subito).</summary>
        private Profilo Compila(Profilo destinazione)
        {
            destinazione.Nome = nome.Text.Trim();
            destinazione.Server = server.Text.Trim();
            destinazione.Database = database.Text.Trim();
            destinazione.AutenticazioneWindows = windows.Checked;
            destinazione.Utente = windows.Checked ? null : utente.Text.Trim();
            if (windows.Checked)
                destinazione.PasswordCifrata = null;
            else if (password.Text.Length > 0)
                destinazione.PasswordCifrata = Segreti.Cifra(password.Text);
            destinazione.CertificatoAttendibile = certificato.Checked;
            return destinazione;
        }

        private async Task ProvaAsync()
        {
            var prova = Compila(new Profilo { PasswordCifrata = profilo.PasswordCifrata });
            if (prova.Server.Length == 0 || prova.Database.Length == 0)
            {
                Ui.Errore(this, "Indicare server e database.");
                return;
            }
            UseWaitCursor = true;
            try
            {
                Ui.Info(this, await ServizioDomande.ProvaConnessioneAsync(prova));
            }
            catch (Exception ex)
            {
                Ui.Errore(this, Ui.Messaggio(ex));
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void Conferma()
        {
            var p = ProviderScelto;
            var ai = new ConfigurazioneAi
            {
                Provider = p,
                Modello = modello.Text.Trim(),
                Endpoint = p is ProviderAi.Ollama or ProviderAi.GeminiVertex && !string.IsNullOrWhiteSpace(endpoint.Text) ? endpoint.Text.Trim() : null,
                Effort = p == ProviderAi.Claude ? (string?)effort.SelectedItem : null,
                Istruzioni = string.IsNullOrWhiteSpace(istruzioni.Text) ? null : istruzioni.Text.Trim(),
                MaxRighe = (int)maxRighe.Value,
                TimeoutSecondi = (int)timeout.Value,
                InvioDatiAnonimizzati = anonimizzati.Checked,
                MaxChiamateAi = (int)maxChiamate.Value,
                MaxRigheAnonimizzate = (int)righeAnonimizzate.Value
            };

            // Segreto da salvare: nuovo (chiave scritta o file JSON caricato) oppure quello già salvato per lo stesso motore
            var nuovoSegreto = p switch
            {
                ProviderAi.GeminiVertex => accountVertexCifrato,
                ProviderAi.Claude or ProviderAi.Gemini when chiave.Text.Trim().Length > 0 => Segreti.Cifra(chiave.Text.Trim()),
                _ => null
            };
            var segreto = nuovoSegreto ?? (SegretoSalvatoValido ? profilo.ApiKeyCifrata : null);

            string? errore =
                nome.Text.Trim().Length == 0 ? "Indicare il nome del profilo." :
                server.Text.Trim().Length == 0 || database.Text.Trim().Length == 0 ? "Indicare server e database." :
                !windows.Checked && utente.Text.Trim().Length == 0 ? "Indicare l'utente SQL." :
                ai.Verifica() ??
                (ProviderAi.RichiedeChiave(p) && segreto is null
                    ? p == ProviderAi.GeminiVertex ? "Caricare il file JSON dell'account di servizio di Google Cloud." : $"Indicare la chiave API di {p}."
                    : null);
            if (errore is not null)
            {
                Ui.Errore(this, errore);
                return;
            }

            Compila(profilo);
            profilo.Ai = ai;
            profilo.ApiKeyCifrata = ProviderAi.RichiedeChiave(p) ? segreto : null;
            DialogResult = DialogResult.OK;
        }
    }
}
