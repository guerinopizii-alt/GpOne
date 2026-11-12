namespace GpOne.Desktop
{
    partial class MainForm
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
            menu = new MenuStrip();
            menuProfilo = new ToolStripMenuItem();
            cambiaDatabaseMenu = new ToolStripMenuItem();
            connessioneMenu = new ToolStripMenuItem();
            strutturaMenu = new ToolStripMenuItem();
            separatoreMenu = new ToolStripSeparator();
            esciMenu = new ToolStripMenuItem();
            menuConversazione = new ToolStripMenuItem();
            nuovaConversazioneMenu = new ToolStripMenuItem();
            aggiornaStoricoMenu = new ToolStripMenuItem();
            divisore = new SplitContainer();
            schedeSinistra = new SchedeTab();
            schedaConversazione = new TabPage();
            elencoTurni = new ElencoVoci();
            schedaStorico = new TabPage();
            elencoStorico = new ElencoVoci();
            lblStorico = new Label();
            pannelloNuova = new Panel();
            nuova = new Pulsante();
            intestazione = new Intestazione();
            pannelloRisposta = new PannelloSfondo();
            contenutoRisposta = new PannelloTrasparente();
            schedaRisultato = new Scheda();
            schedeRisposta = new SchedeTab();
            schedaTabella = new TabPage();
            griglia = new DataGridView();
            schedaGrafico = new TabPage();
            grafico = new ScottPlot.WinForms.FormsPlot();
            schedaSql = new TabPage();
            riquadroSql = new Scheda();
            sql = new TextBox();
            barraInfo = new FlowLayoutPanel();
            info = new Etichetta();
            troncato = new Etichetta();
            durata = new Etichetta();
            anonimi = new Etichetta();
            excel = new Pulsante();
            csv = new Pulsante();
            pannelloErrore = new Scheda();
            errore = new TextBox();
            copiaErrore = new Pulsante();
            spiegazione = new Label();
            domanda = new Fumetto();
            barraValutazione = new RigaTrasparente();
            benvenuto = new PannelloBenvenuto();
            compositore = new PannelloTrasparente();
            casella = new Contorno();
            testo = new TextBox();
            pulsantiInvio = new FlowLayoutPanel();
            annulla = new Pulsante();
            invia = new Pulsante();
            barraStato = new StatusStrip();
            stato = new ToolStripStatusLabel();
            privacy = new ToolStripStatusLabel();
            menu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)divisore).BeginInit();
            divisore.Panel1.SuspendLayout();
            divisore.Panel2.SuspendLayout();
            divisore.SuspendLayout();
            schedeSinistra.SuspendLayout();
            schedaConversazione.SuspendLayout();
            schedaStorico.SuspendLayout();
            pannelloNuova.SuspendLayout();
            pannelloRisposta.SuspendLayout();
            contenutoRisposta.SuspendLayout();
            schedaRisultato.SuspendLayout();
            schedeRisposta.SuspendLayout();
            schedaTabella.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)griglia).BeginInit();
            schedaGrafico.SuspendLayout();
            schedaSql.SuspendLayout();
            riquadroSql.SuspendLayout();
            barraInfo.SuspendLayout();
            pannelloErrore.SuspendLayout();
            compositore.SuspendLayout();
            casella.SuspendLayout();
            pulsantiInvio.SuspendLayout();
            barraStato.SuspendLayout();
            SuspendLayout();
            // 
            // menu
            // 
            menu.BackColor = Color.White;
            menu.ImageScalingSize = new Size(20, 20);
            menu.Items.AddRange(new ToolStripItem[] { menuProfilo, menuConversazione });
            menu.Location = new Point(0, 0);
            menu.Name = "menu";
            menu.Padding = new Padding(10, 4, 0, 4);
            menu.Size = new Size(1371, 32);
            menu.TabIndex = 1;
            // 
            // menuProfilo
            // 
            menuProfilo.DropDownItems.AddRange(new ToolStripItem[] { cambiaDatabaseMenu, connessioneMenu, strutturaMenu, separatoreMenu, esciMenu });
            menuProfilo.Name = "menuProfilo";
            menuProfilo.Size = new Size(67, 24);
            menuProfilo.Text = "&Profilo";
            // 
            // cambiaDatabaseMenu
            // 
            cambiaDatabaseMenu.Name = "cambiaDatabaseMenu";
            cambiaDatabaseMenu.Size = new Size(269, 26);
            cambiaDatabaseMenu.Text = "&Cambia database…";
            cambiaDatabaseMenu.Click += CambiaDatabaseMenu_Click;
            // 
            // connessioneMenu
            // 
            connessioneMenu.Name = "connessioneMenu";
            connessioneMenu.Size = new Size(269, 26);
            connessioneMenu.Text = "C&onnessione e motore AI…";
            connessioneMenu.Click += ConnessioneMenu_Click;
            // 
            // strutturaMenu
            // 
            strutturaMenu.Name = "strutturaMenu";
            strutturaMenu.Size = new Size(269, 26);
            strutturaMenu.Text = "&Struttura del database…";
            strutturaMenu.Click += StrutturaMenu_Click;
            // 
            // separatoreMenu
            // 
            separatoreMenu.Name = "separatoreMenu";
            separatoreMenu.Size = new Size(266, 6);
            // 
            // esciMenu
            // 
            esciMenu.Name = "esciMenu";
            esciMenu.Size = new Size(269, 26);
            esciMenu.Text = "&Esci";
            esciMenu.Click += EsciMenu_Click;
            // 
            // menuConversazione
            // 
            menuConversazione.DropDownItems.AddRange(new ToolStripItem[] { nuovaConversazioneMenu, aggiornaStoricoMenu });
            menuConversazione.Name = "menuConversazione";
            menuConversazione.Size = new Size(119, 24);
            menuConversazione.Text = "&Conversazione";
            // 
            // nuovaConversazioneMenu
            // 
            nuovaConversazioneMenu.Name = "nuovaConversazioneMenu";
            nuovaConversazioneMenu.ShortcutKeys = Keys.Control | Keys.N;
            nuovaConversazioneMenu.Size = new Size(286, 26);
            nuovaConversazioneMenu.Text = "&Nuova conversazione";
            nuovaConversazioneMenu.Click += NuovaConversazioneMenu_Click;
            // 
            // aggiornaStoricoMenu
            // 
            aggiornaStoricoMenu.Name = "aggiornaStoricoMenu";
            aggiornaStoricoMenu.ShortcutKeys = Keys.F5;
            aggiornaStoricoMenu.Size = new Size(286, 26);
            aggiornaStoricoMenu.Text = "&Aggiorna storico";
            aggiornaStoricoMenu.Click += AggiornaStoricoMenu_Click;
            // 
            // divisore
            // 
            divisore.BackColor = Color.FromArgb(228, 222, 242);
            divisore.Dock = DockStyle.Fill;
            divisore.FixedPanel = FixedPanel.Panel1;
            divisore.Location = new Point(0, 32);
            divisore.Margin = new Padding(0);
            divisore.Name = "divisore";
            // 
            // divisore.Panel1
            // 
            divisore.Panel1.BackColor = Color.White;
            divisore.Panel1.Controls.Add(schedeSinistra);
            divisore.Panel1.Controls.Add(pannelloNuova);
            divisore.Panel1.Controls.Add(intestazione);
            // 
            // divisore.Panel2
            // 
            divisore.Panel2.BackColor = Color.FromArgb(248, 246, 255);
            divisore.Panel2.Controls.Add(pannelloRisposta);
            divisore.Size = new Size(1371, 955);
            divisore.SplitterDistance = 330;
            divisore.SplitterWidth = 1;
            divisore.TabIndex = 0;
            // 
            // schedeSinistra
            // 
            schedeSinistra.Controls.Add(schedaConversazione);
            schedeSinistra.Controls.Add(schedaStorico);
            schedeSinistra.Dock = DockStyle.Fill;
            schedeSinistra.ItemSize = new Size(164, 42);
            schedeSinistra.Location = new Point(0, 136);
            schedeSinistra.Name = "schedeSinistra";
            schedeSinistra.Riempi = true;
            schedeSinistra.SelectedIndex = 0;
            schedeSinistra.Size = new Size(330, 819);
            schedeSinistra.TabIndex = 2;
            // 
            // schedaConversazione
            // 
            schedaConversazione.BackColor = Color.White;
            schedaConversazione.Controls.Add(elencoTurni);
            schedaConversazione.Location = new Point(0, 46);
            schedaConversazione.Name = "schedaConversazione";
            schedaConversazione.Padding = new Padding(0, 6, 0, 0);
            schedaConversazione.Size = new Size(330, 773);
            schedaConversazione.TabIndex = 0;
            schedaConversazione.Tag = "";
            schedaConversazione.Text = "Conversazione";
            // 
            // elencoTurni
            // 
            elencoTurni.Dock = DockStyle.Fill;
            elencoTurni.Location = new Point(0, 6);
            elencoTurni.Name = "elencoTurni";
            elencoTurni.Size = new Size(330, 767);
            elencoTurni.TabIndex = 0;
            elencoTurni.SelectedIndexChanged += ElencoTurni_SelectedIndexChanged;
            // 
            // schedaStorico
            // 
            schedaStorico.BackColor = Color.White;
            schedaStorico.Controls.Add(elencoStorico);
            schedaStorico.Controls.Add(lblStorico);
            schedaStorico.Location = new Point(0, 46);
            schedaStorico.Name = "schedaStorico";
            schedaStorico.Padding = new Padding(0, 6, 0, 0);
            schedaStorico.Size = new Size(330, 773);
            schedaStorico.TabIndex = 1;
            schedaStorico.Tag = "";
            schedaStorico.Text = "Storico";
            // 
            // elencoStorico
            // 
            elencoStorico.Dock = DockStyle.Fill;
            elencoStorico.Location = new Point(0, 6);
            elencoStorico.Name = "elencoStorico";
            elencoStorico.Size = new Size(330, 711);
            elencoStorico.TabIndex = 0;
            elencoStorico.DoubleClick += ElencoStorico_DoubleClick;
            elencoStorico.KeyDown += ElencoStorico_KeyDown;
            // 
            // lblStorico
            // 
            lblStorico.Dock = DockStyle.Bottom;
            lblStorico.ForeColor = Color.FromArgb(104, 100, 128);
            lblStorico.Location = new Point(0, 717);
            lblStorico.Name = "lblStorico";
            lblStorico.Padding = new Padding(16, 6, 16, 10);
            lblStorico.Size = new Size(330, 56);
            lblStorico.TabIndex = 1;
            lblStorico.Text = "Doppio clic per riaprire: la query viene rieseguita, senza chiamare l'AI.";
            // 
            // pannelloNuova
            // 
            pannelloNuova.BackColor = Color.White;
            pannelloNuova.Controls.Add(nuova);
            pannelloNuova.Dock = DockStyle.Top;
            pannelloNuova.Location = new Point(0, 76);
            pannelloNuova.Name = "pannelloNuova";
            pannelloNuova.Padding = new Padding(16, 6, 16, 12);
            pannelloNuova.Size = new Size(330, 60);
            pannelloNuova.TabIndex = 1;
            // 
            // nuova
            // 
            nuova.Dock = DockStyle.Fill;
            nuova.FlatStyle = FlatStyle.Flat;
            nuova.Icona = "";
            nuova.Location = new Point(16, 6);
            nuova.Name = "nuova";
            nuova.Size = new Size(298, 42);
            nuova.Stile = StilePulsante.Primario;
            nuova.TabIndex = 0;
            nuova.Text = "Nuova conversazione";
            nuova.Click += NuovaConversazioneMenu_Click;
            // 
            // intestazione
            // 
            intestazione.Dock = DockStyle.Top;
            intestazione.Location = new Point(0, 0);
            intestazione.Name = "intestazione";
            intestazione.Size = new Size(330, 76);
            intestazione.TabIndex = 0;
            // 
            // pannelloRisposta
            // 
            pannelloRisposta.BackColor = Color.FromArgb(248, 246, 255);
            pannelloRisposta.Controls.Add(contenutoRisposta);
            pannelloRisposta.Controls.Add(benvenuto);
            pannelloRisposta.Controls.Add(compositore);
            pannelloRisposta.Dock = DockStyle.Fill;
            pannelloRisposta.Location = new Point(0, 0);
            pannelloRisposta.Name = "pannelloRisposta";
            pannelloRisposta.Padding = new Padding(32, 22, 32, 0);
            pannelloRisposta.Size = new Size(1040, 955);
            pannelloRisposta.TabIndex = 0;
            // 
            // contenutoRisposta
            // 
            contenutoRisposta.BackColor = Color.Transparent;
            contenutoRisposta.Controls.Add(schedaRisultato);
            contenutoRisposta.Controls.Add(pannelloErrore);
            contenutoRisposta.Controls.Add(spiegazione);
            contenutoRisposta.Controls.Add(domanda);
            contenutoRisposta.Controls.Add(barraValutazione);
            contenutoRisposta.Dock = DockStyle.Fill;
            contenutoRisposta.Location = new Point(32, 22);
            contenutoRisposta.Name = "contenutoRisposta";
            contenutoRisposta.Size = new Size(976, 815);
            contenutoRisposta.TabIndex = 0;
            contenutoRisposta.Resize += ContenutoRisposta_Resize;
            // 
            // schedaRisultato
            // 
            schedaRisultato.Controls.Add(schedeRisposta);
            schedaRisultato.Controls.Add(barraInfo);
            schedaRisultato.Dock = DockStyle.Fill;
            schedaRisultato.Location = new Point(0, 170);
            schedaRisultato.Name = "schedaRisultato";
            schedaRisultato.Padding = new Padding(16, 12, 16, 16);
            schedaRisultato.Size = new Size(976, 631);
            schedaRisultato.TabIndex = 3;
            // 
            // schedeRisposta
            // 
            schedeRisposta.Controls.Add(schedaTabella);
            schedeRisposta.Controls.Add(schedaGrafico);
            schedeRisposta.Controls.Add(schedaSql);
            schedeRisposta.Dock = DockStyle.Fill;
            schedeRisposta.ItemSize = new Size(130, 42);
            schedeRisposta.Location = new Point(16, 55);
            schedeRisposta.Name = "schedeRisposta";
            schedeRisposta.SelectedIndex = 0;
            schedeRisposta.Size = new Size(944, 560);
            schedeRisposta.TabIndex = 1;
            // 
            // schedaTabella
            // 
            schedaTabella.BackColor = Color.White;
            schedaTabella.Controls.Add(griglia);
            schedaTabella.Location = new Point(0, 46);
            schedaTabella.Name = "schedaTabella";
            schedaTabella.Padding = new Padding(0, 8, 0, 0);
            schedaTabella.Size = new Size(944, 514);
            schedaTabella.TabIndex = 0;
            schedaTabella.Tag = "";
            schedaTabella.Text = "Tabella";
            // 
            // griglia
            // 
            griglia.AllowUserToAddRows = false;
            griglia.AllowUserToDeleteRows = false;
            griglia.AllowUserToResizeRows = false;
            griglia.BackgroundColor = Color.White;
            griglia.BorderStyle = BorderStyle.None;
            griglia.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            griglia.Dock = DockStyle.Fill;
            griglia.Location = new Point(0, 8);
            griglia.Name = "griglia";
            griglia.ReadOnly = true;
            griglia.RowHeadersVisible = false;
            griglia.RowHeadersWidth = 51;
            griglia.Size = new Size(944, 506);
            griglia.TabIndex = 0;
            // 
            // schedaGrafico
            // 
            schedaGrafico.BackColor = Color.White;
            schedaGrafico.Controls.Add(grafico);
            schedaGrafico.Location = new Point(0, 46);
            schedaGrafico.Name = "schedaGrafico";
            schedaGrafico.Padding = new Padding(0, 8, 0, 0);
            schedaGrafico.Size = new Size(944, 514);
            schedaGrafico.TabIndex = 1;
            schedaGrafico.Tag = "";
            schedaGrafico.Text = "Grafico";
            // 
            // grafico
            // 
            grafico.Dock = DockStyle.Fill;
            grafico.Location = new Point(0, 8);
            grafico.Name = "grafico";
            grafico.Size = new Size(944, 506);
            grafico.TabIndex = 0;
            // 
            // schedaSql
            // 
            schedaSql.BackColor = Color.White;
            schedaSql.Controls.Add(riquadroSql);
            schedaSql.Location = new Point(0, 46);
            schedaSql.Name = "schedaSql";
            schedaSql.Padding = new Padding(0, 8, 0, 0);
            schedaSql.Size = new Size(944, 514);
            schedaSql.TabIndex = 2;
            schedaSql.Tag = "";
            schedaSql.Text = "SQL";
            // 
            // riquadroSql
            // 
            riquadroSql.Colore = Color.FromArgb(30, 27, 58);
            riquadroSql.ColoreBordo = Color.FromArgb(30, 27, 58);
            riquadroSql.Controls.Add(sql);
            riquadroSql.Dock = DockStyle.Fill;
            riquadroSql.Location = new Point(0, 8);
            riquadroSql.Name = "riquadroSql";
            riquadroSql.Padding = new Padding(16, 14, 12, 14);
            riquadroSql.Raggio = 14;
            riquadroSql.Size = new Size(944, 506);
            riquadroSql.TabIndex = 0;
            // 
            // sql
            // 
            sql.BackColor = Color.FromArgb(30, 27, 58);
            sql.BorderStyle = BorderStyle.None;
            sql.Dock = DockStyle.Fill;
            sql.Font = new Font("Consolas", 10.5F);
            sql.ForeColor = Color.FromArgb(233, 227, 255);
            sql.Location = new Point(16, 14);
            sql.Multiline = true;
            sql.Name = "sql";
            sql.ReadOnly = true;
            sql.ScrollBars = ScrollBars.Both;
            sql.Size = new Size(916, 478);
            sql.TabIndex = 0;
            sql.WordWrap = false;
            // 
            // barraInfo
            // 
            barraInfo.AutoSize = true;
            barraInfo.BackColor = Color.White;
            barraInfo.Controls.Add(info);
            barraInfo.Controls.Add(troncato);
            barraInfo.Controls.Add(durata);
            barraInfo.Controls.Add(anonimi);
            barraInfo.Controls.Add(excel);
            barraInfo.Controls.Add(csv);
            barraInfo.Dock = DockStyle.Top;
            barraInfo.Location = new Point(16, 12);
            barraInfo.Name = "barraInfo";
            barraInfo.Padding = new Padding(0, 0, 0, 2);
            barraInfo.Size = new Size(944, 43);
            barraInfo.TabIndex = 0;
            // 
            // info
            // 
            info.Icona = "";
            info.Location = new Point(0, 6);
            info.Margin = new Padding(0, 6, 8, 6);
            info.Name = "info";
            info.Size = new Size(44, 26);
            info.TabIndex = 0;
            // 
            // troncato
            // 
            troncato.Icona = "";
            troncato.Location = new Point(52, 6);
            troncato.Margin = new Padding(0, 6, 8, 6);
            troncato.Name = "troncato";
            troncato.Size = new Size(98, 26);
            troncato.TabIndex = 1;
            troncato.Text = "troncato";
            troncato.Variante = VarianteEtichetta.Attenzione;
            // 
            // durata
            // 
            durata.Icona = "";
            durata.Location = new Point(158, 6);
            durata.Margin = new Padding(0, 6, 8, 6);
            durata.Name = "durata";
            durata.Size = new Size(44, 26);
            durata.TabIndex = 2;
            durata.Variante = VarianteEtichetta.Neutro;
            // 
            // anonimi
            // 
            anonimi.Icona = "";
            anonimi.Location = new Point(210, 6);
            anonimi.Margin = new Padding(0, 6, 8, 6);
            anonimi.Name = "anonimi";
            anonimi.Size = new Size(241, 26);
            anonimi.TabIndex = 3;
            anonimi.Text = "dati anonimizzati inviati all'AI";
            anonimi.Variante = VarianteEtichetta.Attenzione;
            // 
            // excel
            // 
            excel.AutoSize = true;
            excel.FlatStyle = FlatStyle.Flat;
            excel.Icona = "";
            excel.Location = new Point(467, 0);
            excel.Margin = new Padding(8, 0, 0, 0);
            excel.Name = "excel";
            excel.Size = new Size(95, 41);
            excel.Stile = StilePulsante.Testo;
            excel.TabIndex = 4;
            excel.Text = "Excel";
            excel.Click += Excel_Click;
            // 
            // csv
            // 
            csv.AutoSize = true;
            csv.FlatStyle = FlatStyle.Flat;
            csv.Icona = "";
            csv.Location = new Point(562, 0);
            csv.Margin = new Padding(0);
            csv.Name = "csv";
            csv.Size = new Size(87, 41);
            csv.Stile = StilePulsante.Testo;
            csv.TabIndex = 5;
            csv.Text = "CSV";
            csv.Click += Csv_Click;
            // 
            // pannelloErrore
            // 
            pannelloErrore.Colore = Color.FromArgb(255, 240, 245);
            pannelloErrore.ColoreBordo = Color.FromArgb(255, 200, 218);
            pannelloErrore.Controls.Add(errore);
            pannelloErrore.Controls.Add(copiaErrore);
            pannelloErrore.Dock = DockStyle.Top;
            pannelloErrore.Location = new Point(0, 104);
            pannelloErrore.Name = "pannelloErrore";
            pannelloErrore.Padding = new Padding(16, 12, 10, 24);
            pannelloErrore.Raggio = 14;
            pannelloErrore.Rientro = new Padding(0, 0, 0, 12);
            pannelloErrore.Size = new Size(976, 66);
            pannelloErrore.TabIndex = 2;
            // 
            // errore
            // 
            errore.BackColor = Color.FromArgb(255, 240, 245);
            errore.BorderStyle = BorderStyle.None;
            errore.Dock = DockStyle.Fill;
            errore.Font = new Font("Segoe UI", 10F);
            errore.ForeColor = Color.FromArgb(194, 24, 91);
            errore.Location = new Point(16, 12);
            errore.Multiline = true;
            errore.Name = "errore";
            errore.ReadOnly = true;
            errore.Size = new Size(850, 30);
            errore.TabIndex = 0;
            // 
            // copiaErrore
            // 
            copiaErrore.Dock = DockStyle.Right;
            copiaErrore.FlatStyle = FlatStyle.Flat;
            copiaErrore.Icona = "";
            copiaErrore.Location = new Point(866, 12);
            copiaErrore.Name = "copiaErrore";
            copiaErrore.Size = new Size(100, 30);
            copiaErrore.Stile = StilePulsante.Testo;
            copiaErrore.TabIndex = 1;
            copiaErrore.Text = "Copia";
            copiaErrore.Click += CopiaErrore_Click;
            // 
            // spiegazione
            // 
            spiegazione.AutoSize = true;
            spiegazione.BackColor = Color.Transparent;
            spiegazione.Dock = DockStyle.Top;
            spiegazione.Font = new Font("Segoe UI", 11F);
            spiegazione.ForeColor = Color.FromArgb(28, 27, 41);
            spiegazione.Location = new Point(0, 63);
            spiegazione.Name = "spiegazione";
            spiegazione.Padding = new Padding(2, 2, 0, 14);
            spiegazione.Size = new Size(2, 41);
            spiegazione.TabIndex = 1;
            // 
            // domanda
            // 
            domanda.Dock = DockStyle.Top;
            domanda.Location = new Point(0, 0);
            domanda.Name = "domanda";
            domanda.Size = new Size(976, 63);
            domanda.TabIndex = 0;
            // 
            // barraValutazione
            // 
            barraValutazione.AutoSize = true;
            barraValutazione.Dock = DockStyle.Bottom;
            barraValutazione.Location = new Point(0, 801);
            barraValutazione.Name = "barraValutazione";
            barraValutazione.Padding = new Padding(0, 10, 0, 4);
            barraValutazione.Size = new Size(976, 14);
            barraValutazione.TabIndex = 4;
            // 
            // benvenuto
            // 
            benvenuto.Dock = DockStyle.Fill;
            benvenuto.Location = new Point(32, 22);
            benvenuto.Name = "benvenuto";
            benvenuto.Size = new Size(976, 815);
            benvenuto.TabIndex = 1;
            // 
            // compositore
            // 
            compositore.BackColor = Color.Transparent;
            compositore.Controls.Add(casella);
            compositore.Dock = DockStyle.Bottom;
            compositore.Location = new Point(32, 837);
            compositore.Name = "compositore";
            compositore.Padding = new Padding(0, 8, 0, 18);
            compositore.Size = new Size(976, 118);
            compositore.TabIndex = 2;
            // 
            // casella
            // 
            casella.Controls.Add(testo);
            casella.Controls.Add(pulsantiInvio);
            casella.Dock = DockStyle.Fill;
            casella.Location = new Point(0, 8);
            casella.Name = "casella";
            casella.Padding = new Padding(22, 14, 14, 16);
            casella.Size = new Size(976, 92);
            casella.TabIndex = 0;
            // 
            // testo
            // 
            testo.BackColor = Color.White;
            testo.BorderStyle = BorderStyle.None;
            testo.Dock = DockStyle.Fill;
            testo.Font = new Font("Segoe UI", 11.5F);
            testo.Location = new Point(22, 14);
            testo.Multiline = true;
            testo.Name = "testo";
            testo.Size = new Size(706, 62);
            testo.TabIndex = 0;
            testo.KeyDown += Testo_KeyDown;
            // 
            // pulsantiInvio
            // 
            pulsantiInvio.AutoSize = true;
            pulsantiInvio.BackColor = Color.White;
            pulsantiInvio.Controls.Add(annulla);
            pulsantiInvio.Controls.Add(invia);
            pulsantiInvio.Dock = DockStyle.Right;
            pulsantiInvio.Location = new Point(728, 14);
            pulsantiInvio.Name = "pulsantiInvio";
            pulsantiInvio.Padding = new Padding(8, 12, 0, 0);
            pulsantiInvio.Size = new Size(234, 62);
            pulsantiInvio.TabIndex = 1;
            pulsantiInvio.WrapContents = false;
            // 
            // annulla
            // 
            annulla.AutoSize = true;
            annulla.FlatStyle = FlatStyle.Flat;
            annulla.Icona = "";
            annulla.Location = new Point(8, 12);
            annulla.Margin = new Padding(0, 0, 8, 0);
            annulla.Name = "annulla";
            annulla.Size = new Size(114, 41);
            annulla.TabIndex = 1;
            annulla.Text = "Annulla";
            annulla.Visible = false;
            annulla.Click += Annulla_Click;
            // 
            // invia
            // 
            invia.AutoSize = true;
            invia.FlatStyle = FlatStyle.Flat;
            invia.Icona = "";
            invia.Location = new Point(130, 12);
            invia.Margin = new Padding(0);
            invia.MinimumSize = new Size(112, 42);
            invia.Name = "invia";
            invia.Size = new Size(112, 42);
            invia.Stile = StilePulsante.Primario;
            invia.TabIndex = 0;
            invia.Text = "Chiedi";
            invia.Click += Invia_Click;
            // 
            // barraStato
            // 
            barraStato.BackColor = Color.White;
            barraStato.ImageScalingSize = new Size(20, 20);
            barraStato.Items.AddRange(new ToolStripItem[] { stato, privacy });
            barraStato.Location = new Point(0, 987);
            barraStato.Name = "barraStato";
            barraStato.Padding = new Padding(10, 0, 16, 0);
            barraStato.Size = new Size(1371, 26);
            barraStato.SizingGrip = false;
            barraStato.TabIndex = 2;
            // 
            // stato
            // 
            stato.ForeColor = Color.FromArgb(104, 100, 128);
            stato.Name = "stato";
            stato.Size = new Size(1076, 20);
            stato.Spring = true;
            stato.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // privacy
            // 
            privacy.ForeColor = Color.FromArgb(15, 143, 110);
            privacy.Name = "privacy";
            privacy.Size = new Size(269, 20);
            privacy.Text = "L'AI vede solo la struttura del database.";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1371, 1013);
            Controls.Add(divisore);
            Controls.Add(menu);
            Controls.Add(barraStato);
            MainMenuStrip = menu;
            Margin = new Padding(3, 4, 3, 4);
            MinimumSize = new Size(1026, 731);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "GpOne";
            FormClosing += MainForm_FormClosing;
            Shown += MainForm_Shown;
            menu.ResumeLayout(false);
            menu.PerformLayout();
            divisore.Panel1.ResumeLayout(false);
            divisore.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)divisore).EndInit();
            divisore.ResumeLayout(false);
            schedeSinistra.ResumeLayout(false);
            schedaConversazione.ResumeLayout(false);
            schedaStorico.ResumeLayout(false);
            pannelloNuova.ResumeLayout(false);
            pannelloRisposta.ResumeLayout(false);
            contenutoRisposta.ResumeLayout(false);
            contenutoRisposta.PerformLayout();
            schedaRisultato.ResumeLayout(false);
            schedaRisultato.PerformLayout();
            schedeRisposta.ResumeLayout(false);
            schedaTabella.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)griglia).EndInit();
            schedaGrafico.ResumeLayout(false);
            schedaSql.ResumeLayout(false);
            riquadroSql.ResumeLayout(false);
            riquadroSql.PerformLayout();
            barraInfo.ResumeLayout(false);
            barraInfo.PerformLayout();
            pannelloErrore.ResumeLayout(false);
            pannelloErrore.PerformLayout();
            compositore.ResumeLayout(false);
            casella.ResumeLayout(false);
            casella.PerformLayout();
            pulsantiInvio.ResumeLayout(false);
            pulsantiInvio.PerformLayout();
            barraStato.ResumeLayout(false);
            barraStato.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip menu;
        private System.Windows.Forms.ToolStripMenuItem menuProfilo;
        private System.Windows.Forms.ToolStripMenuItem cambiaDatabaseMenu;
        private System.Windows.Forms.ToolStripMenuItem connessioneMenu;
        private System.Windows.Forms.ToolStripMenuItem strutturaMenu;
        private System.Windows.Forms.ToolStripSeparator separatoreMenu;
        private System.Windows.Forms.ToolStripMenuItem esciMenu;
        private System.Windows.Forms.ToolStripMenuItem menuConversazione;
        private System.Windows.Forms.ToolStripMenuItem nuovaConversazioneMenu;
        private System.Windows.Forms.ToolStripMenuItem aggiornaStoricoMenu;
        private System.Windows.Forms.SplitContainer divisore;
        private GpOne.Desktop.SchedeTab schedeSinistra;
        private System.Windows.Forms.TabPage schedaConversazione;
        private GpOne.Desktop.ElencoVoci elencoTurni;
        private System.Windows.Forms.TabPage schedaStorico;
        private GpOne.Desktop.ElencoVoci elencoStorico;
        private System.Windows.Forms.Label lblStorico;
        private System.Windows.Forms.Panel pannelloNuova;
        private GpOne.Desktop.Pulsante nuova;
        private GpOne.Desktop.Intestazione intestazione;
        private GpOne.Desktop.PannelloSfondo pannelloRisposta;
        private GpOne.Desktop.PannelloTrasparente contenutoRisposta;
        private GpOne.Desktop.Scheda schedaRisultato;
        private GpOne.Desktop.SchedeTab schedeRisposta;
        private System.Windows.Forms.TabPage schedaTabella;
        private System.Windows.Forms.DataGridView griglia;
        private System.Windows.Forms.TabPage schedaGrafico;
        private ScottPlot.WinForms.FormsPlot grafico;
        private System.Windows.Forms.TabPage schedaSql;
        private GpOne.Desktop.Scheda riquadroSql;
        private System.Windows.Forms.TextBox sql;
        private System.Windows.Forms.FlowLayoutPanel barraInfo;
        private GpOne.Desktop.Etichetta info;
        private GpOne.Desktop.Etichetta troncato;
        private GpOne.Desktop.Etichetta durata;
        private GpOne.Desktop.Etichetta anonimi;
        private GpOne.Desktop.Pulsante excel;
        private GpOne.Desktop.Pulsante csv;
        private GpOne.Desktop.Scheda pannelloErrore;
        private System.Windows.Forms.TextBox errore;
        private GpOne.Desktop.Pulsante copiaErrore;
        private System.Windows.Forms.Label spiegazione;
        private GpOne.Desktop.Fumetto domanda;
        private GpOne.Desktop.RigaTrasparente barraValutazione;
        private GpOne.Desktop.PannelloBenvenuto benvenuto;
        private GpOne.Desktop.PannelloTrasparente compositore;
        private GpOne.Desktop.Contorno casella;
        private System.Windows.Forms.TextBox testo;
        private System.Windows.Forms.FlowLayoutPanel pulsantiInvio;
        private GpOne.Desktop.Pulsante annulla;
        private GpOne.Desktop.Pulsante invia;
        private System.Windows.Forms.StatusStrip barraStato;
        private System.Windows.Forms.ToolStripStatusLabel stato;
        private System.Windows.Forms.ToolStripStatusLabel privacy;
    }
}
