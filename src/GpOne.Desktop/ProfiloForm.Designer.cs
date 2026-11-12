namespace GpOne.Desktop
{
    partial class ProfiloForm
    {
        /// <summary>Variabile di progettazione necessaria.</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>Pulire le risorse in uso.</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Codice generato da Progettazione Windows Form

        private void InitializeComponent()
        {
            modulo = new System.Windows.Forms.TableLayoutPanel();
            lblNome = new System.Windows.Forms.Label();
            nome = new System.Windows.Forms.TextBox();
            lblSezioneDatabase = new System.Windows.Forms.Label();
            lblServer = new System.Windows.Forms.Label();
            server = new System.Windows.Forms.TextBox();
            lblDatabase = new System.Windows.Forms.Label();
            database = new System.Windows.Forms.TextBox();
            windows = new System.Windows.Forms.CheckBox();
            lblUtente = new System.Windows.Forms.Label();
            utente = new System.Windows.Forms.TextBox();
            lblPassword = new System.Windows.Forms.Label();
            password = new System.Windows.Forms.TextBox();
            certificato = new System.Windows.Forms.CheckBox();
            prova = new GpOne.Desktop.Pulsante();
            lblSezioneAi = new System.Windows.Forms.Label();
            lblProvider = new System.Windows.Forms.Label();
            provider = new System.Windows.Forms.ComboBox();
            lblModello = new System.Windows.Forms.Label();
            pannelloModello = new System.Windows.Forms.TableLayoutPanel();
            modello = new System.Windows.Forms.ComboBox();
            caricaModelli = new GpOne.Desktop.Pulsante();
            lblEndpoint = new System.Windows.Forms.Label();
            endpoint = new System.Windows.Forms.TextBox();
            lblChiave = new System.Windows.Forms.Label();
            chiave = new System.Windows.Forms.TextBox();
            lblAccount = new System.Windows.Forms.Label();
            pannelloAccount = new System.Windows.Forms.FlowLayoutPanel();
            caricaAccount = new GpOne.Desktop.Pulsante();
            infoAccount = new System.Windows.Forms.Label();
            lblEffort = new System.Windows.Forms.Label();
            effort = new System.Windows.Forms.ComboBox();
            lblIstruzioni = new System.Windows.Forms.Label();
            istruzioni = new System.Windows.Forms.TextBox();
            lblMaxRighe = new System.Windows.Forms.Label();
            maxRighe = new System.Windows.Forms.NumericUpDown();
            lblTimeout = new System.Windows.Forms.Label();
            timeout = new System.Windows.Forms.NumericUpDown();
            anonimizzati = new System.Windows.Forms.CheckBox();
            lblMaxChiamate = new System.Windows.Forms.Label();
            maxChiamate = new System.Windows.Forms.NumericUpDown();
            lblRigheAnonimizzate = new System.Windows.Forms.Label();
            righeAnonimizzate = new System.Windows.Forms.NumericUpDown();
            barra = new System.Windows.Forms.FlowLayoutPanel();
            annulla = new GpOne.Desktop.Pulsante();
            ok = new GpOne.Desktop.Pulsante();
            modulo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)maxRighe).BeginInit();
            ((System.ComponentModel.ISupportInitialize)timeout).BeginInit();
            ((System.ComponentModel.ISupportInitialize)maxChiamate).BeginInit();
            ((System.ComponentModel.ISupportInitialize)righeAnonimizzate).BeginInit();
            pannelloAccount.SuspendLayout();
            pannelloModello.SuspendLayout();
            barra.SuspendLayout();
            SuspendLayout();
            //
            // modulo
            //
            modulo.AutoScroll = true;
            modulo.ColumnCount = 2;
            modulo.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            modulo.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            modulo.Controls.Add(lblNome, 0, 0);
            modulo.Controls.Add(nome, 1, 0);
            modulo.Controls.Add(lblSezioneDatabase, 0, 1);
            modulo.Controls.Add(lblServer, 0, 2);
            modulo.Controls.Add(server, 1, 2);
            modulo.Controls.Add(lblDatabase, 0, 3);
            modulo.Controls.Add(database, 1, 3);
            modulo.Controls.Add(windows, 0, 4);
            modulo.Controls.Add(lblUtente, 0, 5);
            modulo.Controls.Add(utente, 1, 5);
            modulo.Controls.Add(lblPassword, 0, 6);
            modulo.Controls.Add(password, 1, 6);
            modulo.Controls.Add(certificato, 0, 7);
            modulo.Controls.Add(prova, 0, 8);
            modulo.Controls.Add(lblSezioneAi, 0, 9);
            modulo.Controls.Add(lblProvider, 0, 10);
            modulo.Controls.Add(provider, 1, 10);
            modulo.Controls.Add(lblEndpoint, 0, 11);
            modulo.Controls.Add(endpoint, 1, 11);
            modulo.Controls.Add(lblChiave, 0, 12);
            modulo.Controls.Add(chiave, 1, 12);
            modulo.Controls.Add(lblAccount, 0, 13);
            modulo.Controls.Add(pannelloAccount, 1, 13);
            modulo.Controls.Add(lblEffort, 0, 14);
            modulo.Controls.Add(effort, 1, 14);
            modulo.Controls.Add(lblIstruzioni, 0, 15);
            modulo.Controls.Add(istruzioni, 1, 15);
            modulo.Controls.Add(lblMaxRighe, 0, 16);
            modulo.Controls.Add(maxRighe, 1, 16);
            modulo.Controls.Add(lblTimeout, 0, 17);
            modulo.Controls.Add(timeout, 1, 17);
            modulo.Controls.Add(lblModello, 0, 18);
            modulo.Controls.Add(pannelloModello, 1, 18);
            modulo.Controls.Add(anonimizzati, 0, 19);
            modulo.Controls.Add(lblMaxChiamate, 0, 20);
            modulo.Controls.Add(maxChiamate, 1, 20);
            modulo.Controls.Add(lblRigheAnonimizzate, 0, 21);
            modulo.Controls.Add(righeAnonimizzate, 1, 21);
            modulo.Dock = System.Windows.Forms.DockStyle.Fill;
            modulo.Location = new System.Drawing.Point(0, 0);
            modulo.Name = "modulo";
            modulo.Padding = new System.Windows.Forms.Padding(8);
            modulo.RowCount = 22;
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 76F));
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.RowStyles.Add(new System.Windows.Forms.RowStyle());
            modulo.SetColumnSpan(lblSezioneDatabase, 2);
            modulo.SetColumnSpan(windows, 2);
            modulo.SetColumnSpan(certificato, 2);
            modulo.SetColumnSpan(prova, 2);
            modulo.SetColumnSpan(lblSezioneAi, 2);
            modulo.SetColumnSpan(anonimizzati, 2);
            modulo.Size = new System.Drawing.Size(620, 591);
            modulo.TabIndex = 0;
            //
            // lblNome
            //
            lblNome.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblNome.AutoSize = true;
            lblNome.Name = "lblNome";
            lblNome.Text = "Nome";
            //
            // nome
            //
            nome.Dock = System.Windows.Forms.DockStyle.Fill;
            nome.Name = "nome";
            nome.TabIndex = 0;
            //
            // lblSezioneDatabase
            //
            lblSezioneDatabase.AutoSize = true;
            lblSezioneDatabase.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            lblSezioneDatabase.Margin = new System.Windows.Forms.Padding(3, 12, 3, 3);
            lblSezioneDatabase.Name = "lblSezioneDatabase";
            lblSezioneDatabase.Text = "Database del cliente (consigliato un utente SQL con il solo ruolo db_datareader)";
            //
            // lblServer
            //
            lblServer.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblServer.AutoSize = true;
            lblServer.Name = "lblServer";
            lblServer.Text = "Server";
            //
            // server
            //
            server.Dock = System.Windows.Forms.DockStyle.Fill;
            server.Name = "server";
            server.PlaceholderText = "es. SRVSQL\\ISTANZA";
            server.TabIndex = 1;
            //
            // lblDatabase
            //
            lblDatabase.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblDatabase.AutoSize = true;
            lblDatabase.Name = "lblDatabase";
            lblDatabase.Text = "Database";
            //
            // database
            //
            database.Dock = System.Windows.Forms.DockStyle.Fill;
            database.Name = "database";
            database.TabIndex = 2;
            //
            // windows
            //
            windows.AutoSize = true;
            windows.Name = "windows";
            windows.TabIndex = 3;
            windows.Text = "Autenticazione di Windows (utente corrente)";
            windows.CheckedChanged += Campi_Changed;
            //
            // lblUtente
            //
            lblUtente.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblUtente.AutoSize = true;
            lblUtente.Name = "lblUtente";
            lblUtente.Text = "Utente SQL";
            //
            // utente
            //
            utente.Dock = System.Windows.Forms.DockStyle.Fill;
            utente.Name = "utente";
            utente.TabIndex = 4;
            //
            // lblPassword
            //
            lblPassword.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblPassword.AutoSize = true;
            lblPassword.Name = "lblPassword";
            lblPassword.Text = "Password";
            //
            // password
            //
            password.Dock = System.Windows.Forms.DockStyle.Fill;
            password.Name = "password";
            password.TabIndex = 5;
            password.UseSystemPasswordChar = true;
            //
            // certificato
            //
            certificato.AutoSize = true;
            certificato.Name = "certificato";
            certificato.TabIndex = 6;
            certificato.Text = "Considera attendibile il certificato del server (certificato autofirmato)";
            //
            // prova
            //
            prova.AutoSize = true;
            prova.Name = "prova";
            prova.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
            prova.TabIndex = 7;
            prova.Text = "Prova collegamento";
            prova.Click += Prova_Click;
            //
            // lblSezioneAi
            //
            lblSezioneAi.AutoSize = true;
            lblSezioneAi.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            lblSezioneAi.Margin = new System.Windows.Forms.Padding(3, 12, 3, 3);
            lblSezioneAi.Name = "lblSezioneAi";
            lblSezioneAi.Text = "Motore AI (riceve la struttura abilitata e la domanda; i dati solo anonimizzati)";
            //
            // lblProvider
            //
            lblProvider.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblProvider.AutoSize = true;
            lblProvider.Name = "lblProvider";
            lblProvider.Text = "Motore";
            //
            // provider
            //
            provider.Dock = System.Windows.Forms.DockStyle.Fill;
            provider.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            provider.Items.AddRange(new object[] { "Claude", "Gemini", "GeminiVertex", "Ollama" });
            provider.Name = "provider";
            provider.TabIndex = 8;
            provider.SelectedIndexChanged += Campi_Changed;
            //
            // lblModello
            //
            lblModello.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblModello.AutoSize = true;
            lblModello.Name = "lblModello";
            lblModello.Text = "Modello";
            //
            // pannelloModello
            //
            pannelloModello.AutoSize = true;
            pannelloModello.ColumnCount = 2;
            pannelloModello.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            pannelloModello.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            pannelloModello.Controls.Add(modello, 0, 0);
            pannelloModello.Controls.Add(caricaModelli, 1, 0);
            pannelloModello.Dock = System.Windows.Forms.DockStyle.Fill;
            pannelloModello.Margin = new System.Windows.Forms.Padding(0);
            pannelloModello.Name = "pannelloModello";
            pannelloModello.RowCount = 1;
            pannelloModello.RowStyles.Add(new System.Windows.Forms.RowStyle());
            pannelloModello.TabIndex = 16;
            //
            // modello
            //
            modello.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            modello.Name = "modello";
            modello.TabIndex = 0;
            //
            // caricaModelli
            //
            caricaModelli.AutoSize = true;
            caricaModelli.Name = "caricaModelli";
            caricaModelli.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
            caricaModelli.TabIndex = 1;
            caricaModelli.Text = "Carica modelli";
            caricaModelli.Click += CaricaModelli_Click;
            //
            // lblEndpoint
            //
            lblEndpoint.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblEndpoint.AutoSize = true;
            lblEndpoint.Name = "lblEndpoint";
            lblEndpoint.Text = "Indirizzo Ollama";
            //
            // endpoint
            //
            endpoint.Dock = System.Windows.Forms.DockStyle.Fill;
            endpoint.Name = "endpoint";
            endpoint.PlaceholderText = "http://localhost:11434";
            endpoint.TabIndex = 9;
            endpoint.TextChanged += Campi_Changed;
            //
            // lblChiave
            //
            lblChiave.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblChiave.AutoSize = true;
            lblChiave.Name = "lblChiave";
            lblChiave.Text = "Chiave API";
            //
            // chiave
            //
            chiave.Dock = System.Windows.Forms.DockStyle.Fill;
            chiave.Name = "chiave";
            chiave.TabIndex = 10;
            chiave.UseSystemPasswordChar = true;
            chiave.TextChanged += Campi_Changed;
            //
            // lblAccount
            //
            lblAccount.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblAccount.AutoSize = true;
            lblAccount.Name = "lblAccount";
            lblAccount.Text = "Account di servizio";
            //
            // pannelloAccount
            //
            pannelloAccount.AutoSize = true;
            pannelloAccount.Controls.Add(caricaAccount);
            pannelloAccount.Controls.Add(infoAccount);
            pannelloAccount.Dock = System.Windows.Forms.DockStyle.Fill;
            pannelloAccount.Margin = new System.Windows.Forms.Padding(0);
            pannelloAccount.Name = "pannelloAccount";
            pannelloAccount.TabIndex = 11;
            pannelloAccount.WrapContents = false;
            //
            // caricaAccount
            //
            caricaAccount.AutoSize = true;
            caricaAccount.Name = "caricaAccount";
            caricaAccount.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
            caricaAccount.TabIndex = 0;
            caricaAccount.Text = "Carica file JSON…";
            caricaAccount.Click += CaricaAccount_Click;
            //
            // infoAccount
            //
            infoAccount.Anchor = System.Windows.Forms.AnchorStyles.Left;
            infoAccount.AutoSize = true;
            infoAccount.ForeColor = System.Drawing.SystemColors.GrayText;
            infoAccount.Name = "infoAccount";
            infoAccount.Text = "non impostato";
            //
            // lblEffort
            //
            lblEffort.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblEffort.AutoSize = true;
            lblEffort.Name = "lblEffort";
            lblEffort.Text = "Effort";
            //
            // effort
            //
            effort.Dock = System.Windows.Forms.DockStyle.Fill;
            effort.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            effort.Items.AddRange(new object[] { "low", "medium", "high", "xhigh", "max" });
            effort.Name = "effort";
            effort.TabIndex = 12;
            //
            // lblIstruzioni
            //
            lblIstruzioni.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblIstruzioni.AutoSize = true;
            lblIstruzioni.Name = "lblIstruzioni";
            lblIstruzioni.Text = "Indicazioni per l'AI";
            //
            // istruzioni
            //
            istruzioni.AcceptsReturn = true;
            istruzioni.Dock = System.Windows.Forms.DockStyle.Fill;
            istruzioni.Multiline = true;
            istruzioni.Name = "istruzioni";
            istruzioni.PlaceholderText = "es. Gli importi sono in euro; l'anno fiscale parte ad aprile.";
            istruzioni.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            istruzioni.TabIndex = 13;
            //
            // lblMaxRighe
            //
            lblMaxRighe.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblMaxRighe.AutoSize = true;
            lblMaxRighe.Name = "lblMaxRighe";
            lblMaxRighe.Text = "Righe massime";
            //
            // maxRighe
            //
            maxRighe.Dock = System.Windows.Forms.DockStyle.Fill;
            maxRighe.Increment = new decimal(new int[] { 100, 0, 0, 0 });
            maxRighe.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            maxRighe.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            maxRighe.Name = "maxRighe";
            maxRighe.TabIndex = 14;
            maxRighe.ThousandsSeparator = true;
            maxRighe.Value = new decimal(new int[] { 1000, 0, 0, 0 });
            //
            // lblTimeout
            //
            lblTimeout.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblTimeout.AutoSize = true;
            lblTimeout.Name = "lblTimeout";
            lblTimeout.Text = "Timeout query (s)";
            //
            // timeout
            //
            timeout.Dock = System.Windows.Forms.DockStyle.Fill;
            timeout.Maximum = new decimal(new int[] { 600, 0, 0, 0 });
            timeout.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            timeout.Name = "timeout";
            timeout.TabIndex = 15;
            timeout.Value = new decimal(new int[] { 30, 0, 0, 0 });
            //
            // anonimizzati
            //
            anonimizzati.AutoSize = true;
            anonimizzati.Checked = true;
            anonimizzati.CheckState = System.Windows.Forms.CheckState.Checked;
            anonimizzati.Name = "anonimizzati";
            anonimizzati.TabIndex = 17;
            anonimizzati.Text = "Se le query non bastano, invia all'AI risultati anonimizzati";
            //
            // lblMaxChiamate
            //
            lblMaxChiamate.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblMaxChiamate.AutoSize = true;
            lblMaxChiamate.Name = "lblMaxChiamate";
            lblMaxChiamate.Text = "Chiamate AI per domanda";
            //
            // maxChiamate
            //
            maxChiamate.Dock = System.Windows.Forms.DockStyle.Fill;
            maxChiamate.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            maxChiamate.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            maxChiamate.Name = "maxChiamate";
            maxChiamate.TabIndex = 18;
            maxChiamate.Value = new decimal(new int[] { 5, 0, 0, 0 });
            //
            // lblRigheAnonimizzate
            //
            lblRigheAnonimizzate.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblRigheAnonimizzate.AutoSize = true;
            lblRigheAnonimizzate.Name = "lblRigheAnonimizzate";
            lblRigheAnonimizzate.Text = "Righe anonimizzate max";
            //
            // righeAnonimizzate
            //
            righeAnonimizzate.Dock = System.Windows.Forms.DockStyle.Fill;
            righeAnonimizzate.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
            righeAnonimizzate.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            righeAnonimizzate.Name = "righeAnonimizzate";
            righeAnonimizzate.TabIndex = 19;
            righeAnonimizzate.Value = new decimal(new int[] { 50, 0, 0, 0 });
            //
            // barra
            //
            barra.AutoSize = true;
            barra.Controls.Add(annulla);
            barra.Controls.Add(ok);
            barra.Dock = System.Windows.Forms.DockStyle.Bottom;
            barra.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            barra.Location = new System.Drawing.Point(0, 591);
            barra.Name = "barra";
            barra.Padding = new System.Windows.Forms.Padding(8);
            barra.Size = new System.Drawing.Size(620, 49);
            barra.TabIndex = 1;
            //
            // annulla
            //
            annulla.AutoSize = true;
            annulla.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            annulla.Name = "annulla";
            annulla.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
            annulla.TabIndex = 1;
            annulla.Text = "Annulla";
            //
            // ok
            //
            ok.AutoSize = true;
            ok.Name = "ok";
            ok.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
            ok.TabIndex = 0;
            ok.Text = "OK";
            ok.Click += Ok_Click;
            //
            // ProfiloForm
            //
            AcceptButton = ok;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = annulla;
            ClientSize = new System.Drawing.Size(620, 640);
            Controls.Add(modulo);
            Controls.Add(barra);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ProfiloForm";
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Profilo";
            modulo.ResumeLayout(false);
            modulo.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)maxRighe).EndInit();
            ((System.ComponentModel.ISupportInitialize)timeout).EndInit();
            ((System.ComponentModel.ISupportInitialize)maxChiamate).EndInit();
            ((System.ComponentModel.ISupportInitialize)righeAnonimizzate).EndInit();
            pannelloAccount.ResumeLayout(false);
            pannelloAccount.PerformLayout();
            pannelloModello.ResumeLayout(false);
            pannelloModello.PerformLayout();
            barra.ResumeLayout(false);
            barra.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel modulo;
        private System.Windows.Forms.Label lblNome;
        private System.Windows.Forms.TextBox nome;
        private System.Windows.Forms.Label lblSezioneDatabase;
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.TextBox server;
        private System.Windows.Forms.Label lblDatabase;
        private System.Windows.Forms.TextBox database;
        private System.Windows.Forms.CheckBox windows;
        private System.Windows.Forms.Label lblUtente;
        private System.Windows.Forms.TextBox utente;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.TextBox password;
        private System.Windows.Forms.CheckBox certificato;
        private GpOne.Desktop.Pulsante prova;
        private System.Windows.Forms.Label lblSezioneAi;
        private System.Windows.Forms.Label lblProvider;
        private System.Windows.Forms.ComboBox provider;
        private System.Windows.Forms.Label lblModello;
        private System.Windows.Forms.TableLayoutPanel pannelloModello;
        private System.Windows.Forms.ComboBox modello;
        private GpOne.Desktop.Pulsante caricaModelli;
        private System.Windows.Forms.Label lblEndpoint;
        private System.Windows.Forms.TextBox endpoint;
        private System.Windows.Forms.Label lblChiave;
        private System.Windows.Forms.TextBox chiave;
        private System.Windows.Forms.Label lblAccount;
        private System.Windows.Forms.FlowLayoutPanel pannelloAccount;
        private GpOne.Desktop.Pulsante caricaAccount;
        private System.Windows.Forms.Label infoAccount;
        private System.Windows.Forms.Label lblEffort;
        private System.Windows.Forms.ComboBox effort;
        private System.Windows.Forms.Label lblIstruzioni;
        private System.Windows.Forms.TextBox istruzioni;
        private System.Windows.Forms.Label lblMaxRighe;
        private System.Windows.Forms.NumericUpDown maxRighe;
        private System.Windows.Forms.Label lblTimeout;
        private System.Windows.Forms.NumericUpDown timeout;
        private System.Windows.Forms.CheckBox anonimizzati;
        private System.Windows.Forms.Label lblMaxChiamate;
        private System.Windows.Forms.NumericUpDown maxChiamate;
        private System.Windows.Forms.Label lblRigheAnonimizzate;
        private System.Windows.Forms.NumericUpDown righeAnonimizzate;
        private System.Windows.Forms.FlowLayoutPanel barra;
        private GpOne.Desktop.Pulsante annulla;
        private GpOne.Desktop.Pulsante ok;
    }
}
