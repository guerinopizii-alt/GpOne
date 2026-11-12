namespace GpOne.Desktop
{
    partial class ProfiliForm
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
            laterale = new Panel();
            voci = new FlowLayoutPanel();
            voceDatabase = new VoceMenu();
            lblAzioni = new Label();
            nuovo = new VoceMenu();
            modifica = new VoceMenu();
            struttura = new VoceMenu();
            elimina = new VoceMenu();
            piede = new Panel();
            notaPrivacy = new Label();
            esci = new VoceMenu();
            logo = new Intestazione();
            contenuto = new PannelloSfondo();
            schedaElenco = new Scheda();
            elenco = new ElencoVoci();
            barraApri = new PannelloTrasparente();
            suggerimento = new Label();
            apri = new Pulsante();
            sottotitolo = new Label();
            titolo = new Label();
            separatore = new Panel();
            laterale.SuspendLayout();
            voci.SuspendLayout();
            piede.SuspendLayout();
            contenuto.SuspendLayout();
            schedaElenco.SuspendLayout();
            barraApri.SuspendLayout();
            SuspendLayout();
            // 
            // laterale
            // 
            laterale.BackColor = Color.White;
            laterale.Controls.Add(voci);
            laterale.Controls.Add(piede);
            laterale.Controls.Add(logo);
            laterale.Dock = DockStyle.Left;
            laterale.Location = new Point(0, 0);
            laterale.Margin = new Padding(3, 4, 3, 4);
            laterale.Name = "laterale";
            laterale.Padding = new Padding(0, 16, 0, 0);
            laterale.Size = new Size(297, 800);
            laterale.TabIndex = 0;
            // 
            // voci
            // 
            voci.Controls.Add(voceDatabase);
            voci.Controls.Add(lblAzioni);
            voci.Controls.Add(nuovo);
            voci.Controls.Add(modifica);
            voci.Controls.Add(struttura);
            voci.Controls.Add(elimina);
            voci.Dock = DockStyle.Fill;
            voci.FlowDirection = FlowDirection.TopDown;
            voci.Location = new Point(0, 117);
            voci.Margin = new Padding(3, 4, 3, 4);
            voci.Name = "voci";
            voci.Padding = new Padding(0, 11, 0, 0);
            voci.Size = new Size(297, 536);
            voci.TabIndex = 0;
            voci.WrapContents = false;
            // 
            // voceDatabase
            // 
            voceDatabase.Icona = "";
            voceDatabase.Location = new Point(0, 11);
            voceDatabase.Margin = new Padding(0);
            voceDatabase.Name = "voceDatabase";
            voceDatabase.Selezionata = true;
            voceDatabase.Size = new Size(297, 61);
            voceDatabase.TabIndex = 0;
            voceDatabase.Text = "Database";
            voceDatabase.Click += VoceDatabase_Click;
            // 
            // lblAzioni
            // 
            lblAzioni.AutoSize = true;
            lblAzioni.Font = new Font("Segoe UI Semibold", 8.25F);
            lblAzioni.ForeColor = Color.FromArgb(104, 100, 128);
            lblAzioni.Location = new Point(30, 96);
            lblAzioni.Margin = new Padding(30, 24, 0, 8);
            lblAzioni.Name = "lblAzioni";
            lblAzioni.Size = new Size(56, 19);
            lblAzioni.TabIndex = 1;
            lblAzioni.Text = "AZIONI";
            // 
            // nuovo
            // 
            nuovo.Icona = "";
            nuovo.Location = new Point(0, 123);
            nuovo.Margin = new Padding(0);
            nuovo.Name = "nuovo";
            nuovo.Size = new Size(297, 61);
            nuovo.TabIndex = 2;
            nuovo.Text = "Nuovo database…";
            nuovo.Click += Nuovo_Click;
            // 
            // modifica
            // 
            modifica.Icona = "";
            modifica.Location = new Point(0, 184);
            modifica.Margin = new Padding(0);
            modifica.Name = "modifica";
            modifica.Size = new Size(297, 61);
            modifica.TabIndex = 3;
            modifica.Text = "Connessione e motore AI…";
            modifica.Click += Modifica_Click;
            // 
            // struttura
            // 
            struttura.Icona = "";
            struttura.Location = new Point(0, 245);
            struttura.Margin = new Padding(0);
            struttura.Name = "struttura";
            struttura.Size = new Size(297, 61);
            struttura.TabIndex = 4;
            struttura.Text = "Struttura del database…";
            struttura.Click += Struttura_Click;
            // 
            // elimina
            // 
            elimina.Icona = "";
            elimina.Location = new Point(0, 306);
            elimina.Margin = new Padding(0);
            elimina.Name = "elimina";
            elimina.Pericolo = true;
            elimina.Size = new Size(297, 61);
            elimina.TabIndex = 5;
            elimina.Text = "Elimina";
            elimina.Click += Elimina_Click;
            // 
            // piede
            // 
            piede.Controls.Add(notaPrivacy);
            piede.Controls.Add(esci);
            piede.Dock = DockStyle.Bottom;
            piede.Location = new Point(0, 653);
            piede.Margin = new Padding(3, 4, 3, 4);
            piede.Name = "piede";
            piede.Padding = new Padding(0, 0, 0, 13);
            piede.Size = new Size(297, 147);
            piede.TabIndex = 1;
            // 
            // notaPrivacy
            // 
            notaPrivacy.Dock = DockStyle.Fill;
            notaPrivacy.ForeColor = Color.FromArgb(15, 143, 110);
            notaPrivacy.Location = new Point(0, 0);
            notaPrivacy.Name = "notaPrivacy";
            notaPrivacy.Padding = new Padding(27, 8, 18, 0);
            notaPrivacy.Size = new Size(297, 73);
            notaPrivacy.TabIndex = 0;
            notaPrivacy.Text = "L'AI vede solo la struttura delle tabelle abilitate: se servono dati, le vengono inviati solo anonimizzati.";
            // 
            // esci
            // 
            esci.Dock = DockStyle.Bottom;
            esci.Icona = "";
            esci.Location = new Point(0, 73);
            esci.Margin = new Padding(3, 4, 3, 4);
            esci.Name = "esci";
            esci.Size = new Size(297, 61);
            esci.TabIndex = 1;
            esci.Text = "Chiudi";
            esci.Click += Esci_Click;
            // 
            // logo
            // 
            logo.Dock = DockStyle.Top;
            logo.Location = new Point(0, 16);
            logo.Margin = new Padding(3, 4, 3, 4);
            logo.Name = "logo";
            logo.Size = new Size(297, 101);
            logo.TabIndex = 2;
            // 
            // contenuto
            // 
            contenuto.BackColor = Color.FromArgb(248, 246, 255);
            contenuto.Controls.Add(schedaElenco);
            contenuto.Controls.Add(barraApri);
            contenuto.Controls.Add(sottotitolo);
            contenuto.Controls.Add(titolo);
            contenuto.Dock = DockStyle.Fill;
            contenuto.Location = new Point(298, 0);
            contenuto.Margin = new Padding(3, 4, 3, 4);
            contenuto.Name = "contenuto";
            contenuto.Padding = new Padding(46, 48, 46, 32);
            contenuto.Size = new Size(776, 800);
            contenuto.TabIndex = 1;
            // 
            // schedaElenco
            // 
            schedaElenco.Controls.Add(elenco);
            schedaElenco.Dock = DockStyle.Fill;
            schedaElenco.Location = new Point(46, 153);
            schedaElenco.Margin = new Padding(3, 4, 3, 4);
            schedaElenco.Name = "schedaElenco";
            schedaElenco.Padding = new Padding(11, 16, 11, 16);
            schedaElenco.Size = new Size(684, 522);
            schedaElenco.TabIndex = 0;
            // 
            // elenco
            // 
            elenco.Dock = DockStyle.Fill;
            elenco.Location = new Point(11, 16);
            elenco.Margin = new Padding(3, 4, 3, 4);
            elenco.Name = "elenco";
            elenco.Size = new Size(662, 490);
            elenco.TabIndex = 0;
            elenco.TestoVuoto = "Nessun database configurato.\r\nCrea il primo con «Nuovo database…» nel menu a sinistra.";
            elenco.SelectedIndexChanged += Elenco_SelectedIndexChanged;
            elenco.DoubleClick += Elenco_DoubleClick;
            // 
            // barraApri
            // 
            barraApri.Controls.Add(suggerimento);
            barraApri.Controls.Add(apri);
            barraApri.Dock = DockStyle.Bottom;
            barraApri.Location = new Point(46, 675);
            barraApri.Margin = new Padding(3, 4, 3, 4);
            barraApri.Name = "barraApri";
            barraApri.Padding = new Padding(0, 29, 0, 0);
            barraApri.Size = new Size(684, 93);
            barraApri.TabIndex = 1;
            // 
            // suggerimento
            // 
            suggerimento.BackColor = Color.Transparent;
            suggerimento.Dock = DockStyle.Fill;
            suggerimento.ForeColor = Color.FromArgb(104, 100, 128);
            suggerimento.Location = new Point(0, 29);
            suggerimento.Name = "suggerimento";
            suggerimento.Size = new Size(524, 64);
            suggerimento.TabIndex = 1;
            suggerimento.Text = "Doppio clic su un database per aprirlo.";
            suggerimento.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // apri
            // 
            apri.Dock = DockStyle.Right;
            apri.FlatStyle = FlatStyle.Flat;
            apri.Icona = "";
            apri.Location = new Point(524, 29);
            apri.Margin = new Padding(3, 4, 3, 4);
            apri.Name = "apri";
            apri.Size = new Size(160, 64);
            apri.Stile = StilePulsante.Primario;
            apri.TabIndex = 0;
            apri.Text = "Apri";
            apri.Click += Apri_Click;
            // 
            // sottotitolo
            // 
            sottotitolo.AutoSize = true;
            sottotitolo.BackColor = Color.Transparent;
            sottotitolo.Dock = DockStyle.Top;
            sottotitolo.Font = new Font("Segoe UI", 10.5F);
            sottotitolo.ForeColor = Color.FromArgb(104, 100, 128);
            sottotitolo.Location = new Point(46, 94);
            sottotitolo.Name = "sottotitolo";
            sottotitolo.Padding = new Padding(2, 5, 0, 29);
            sottotitolo.Size = new Size(696, 59);
            sottotitolo.TabIndex = 2;
            sottotitolo.Text = "Scegli il database del cliente: le domande e lo storico sono separati per ogni database.";
            // 
            // titolo
            // 
            titolo.AutoSize = true;
            titolo.BackColor = Color.Transparent;
            titolo.Dock = DockStyle.Top;
            titolo.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
            titolo.ForeColor = Color.FromArgb(28, 27, 41);
            titolo.Location = new Point(46, 48);
            titolo.Name = "titolo";
            titolo.Size = new Size(634, 46);
            titolo.TabIndex = 3;
            titolo.Text = "Su quale database vuoi fare domande?";
            // 
            // separatore
            // 
            separatore.BackColor = Color.FromArgb(228, 222, 242);
            separatore.Dock = DockStyle.Left;
            separatore.Location = new Point(297, 0);
            separatore.Margin = new Padding(3, 4, 3, 4);
            separatore.Name = "separatore";
            separatore.Size = new Size(1, 800);
            separatore.TabIndex = 2;
            // 
            // ProfiliForm
            // 
            AcceptButton = apri;
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1074, 800);
            Controls.Add(contenuto);
            Controls.Add(separatore);
            Controls.Add(laterale);
            Margin = new Padding(3, 4, 3, 4);
            MinimumSize = new Size(935, 678);
            Name = "ProfiliForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "GpOne – scegli il database";
            Shown += ProfiliForm_Shown;
            laterale.ResumeLayout(false);
            voci.ResumeLayout(false);
            voci.PerformLayout();
            piede.ResumeLayout(false);
            contenuto.ResumeLayout(false);
            contenuto.PerformLayout();
            schedaElenco.ResumeLayout(false);
            barraApri.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel laterale;
        private System.Windows.Forms.FlowLayoutPanel voci;
        private GpOne.Desktop.VoceMenu voceDatabase;
        private System.Windows.Forms.Label lblAzioni;
        private GpOne.Desktop.VoceMenu nuovo;
        private GpOne.Desktop.VoceMenu modifica;
        private GpOne.Desktop.VoceMenu struttura;
        private GpOne.Desktop.VoceMenu elimina;
        private System.Windows.Forms.Panel piede;
        private System.Windows.Forms.Label notaPrivacy;
        private GpOne.Desktop.VoceMenu esci;
        private GpOne.Desktop.Intestazione logo;
        private GpOne.Desktop.PannelloSfondo contenuto;
        private GpOne.Desktop.Scheda schedaElenco;
        private GpOne.Desktop.ElencoVoci elenco;
        private GpOne.Desktop.PannelloTrasparente barraApri;
        private System.Windows.Forms.Label suggerimento;
        private GpOne.Desktop.Pulsante apri;
        private System.Windows.Forms.Label sottotitolo;
        private System.Windows.Forms.Label titolo;
        private System.Windows.Forms.Panel separatore;
    }
}
