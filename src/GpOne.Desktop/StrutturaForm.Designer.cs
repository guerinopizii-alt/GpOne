namespace GpOne.Desktop
{
    partial class StrutturaForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(StrutturaForm));
            nota = new Label();
            divisore = new SplitContainer();
            albero = new TreeView();
            ricerca = new TableLayoutPanel();
            filtroTabelle = new TextBox();
            filtro = new TextBox();
            soloAbilitati = new CheckBox();
            rileggi = new Pulsante();
            descrizione = new TextBox();
            lblDescrizione = new Label();
            selezione = new Label();
            barra = new FlowLayoutPanel();
            annulla = new Pulsante();
            salva = new Pulsante();
            riepilogo = new Label();
            ((System.ComponentModel.ISupportInitialize)divisore).BeginInit();
            divisore.Panel1.SuspendLayout();
            divisore.Panel2.SuspendLayout();
            divisore.SuspendLayout();
            ricerca.SuspendLayout();
            barra.SuspendLayout();
            SuspendLayout();
            // 
            // nota
            // 
            nota.Dock = DockStyle.Top;
            nota.Location = new Point(0, 0);
            nota.Name = "nota";
            nota.Padding = new Padding(9, 8, 9, 0);
            nota.Size = new Size(1051, 59);
            nota.TabIndex = 1;
            nota.Text = resources.GetString("nota.Text");
            // 
            // divisore
            // 
            divisore.Dock = DockStyle.Fill;
            divisore.Location = new Point(0, 59);
            divisore.Margin = new Padding(3, 4, 3, 4);
            divisore.Name = "divisore";
            // 
            // divisore.Panel1
            // 
            divisore.Panel1.Controls.Add(albero);
            divisore.Panel1.Controls.Add(ricerca);
            // 
            // divisore.Panel2
            // 
            divisore.Panel2.Controls.Add(descrizione);
            divisore.Panel2.Controls.Add(lblDescrizione);
            divisore.Panel2.Controls.Add(selezione);
            divisore.Panel2.Padding = new Padding(9, 11, 9, 11);
            divisore.Size = new Size(1051, 670);
            divisore.SplitterDistance = 594;
            divisore.SplitterWidth = 5;
            divisore.TabIndex = 0;
            // 
            // albero
            // 
            albero.CheckBoxes = true;
            albero.Dock = DockStyle.Fill;
            albero.HideSelection = false;
            albero.Location = new Point(0, 45);
            albero.Margin = new Padding(3, 4, 3, 4);
            albero.Name = "albero";
            albero.Size = new Size(594, 625);
            albero.TabIndex = 1;
            albero.AfterCheck += Albero_AfterCheck;
            albero.AfterSelect += Albero_AfterSelect;
            // 
            // ricerca
            // 
            ricerca.ColumnCount = 4;
            ricerca.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            ricerca.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            ricerca.ColumnStyles.Add(new ColumnStyle());
            ricerca.ColumnStyles.Add(new ColumnStyle());
            ricerca.Controls.Add(filtroTabelle, 0, 0);
            ricerca.Controls.Add(filtro, 1, 0);
            ricerca.Controls.Add(soloAbilitati, 2, 0);
            ricerca.Controls.Add(rileggi, 3, 0);
            ricerca.Dock = DockStyle.Top;
            ricerca.Location = new Point(0, 0);
            ricerca.Margin = new Padding(3, 4, 3, 4);
            ricerca.Name = "ricerca";
            ricerca.Padding = new Padding(5, 3, 5, 3);
            ricerca.RowCount = 1;
            ricerca.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            ricerca.Size = new Size(594, 45);
            ricerca.TabIndex = 0;
            // 
            // filtroTabelle
            // 
            filtroTabelle.Dock = DockStyle.Fill;
            filtroTabelle.Location = new Point(8, 7);
            filtroTabelle.Margin = new Padding(3, 4, 3, 4);
            filtroTabelle.Name = "filtroTabelle";
            filtroTabelle.PlaceholderText = "Filtra per nome tabella…";
            filtroTabelle.Size = new Size(127, 27);
            filtroTabelle.TabIndex = 0;
            filtroTabelle.TextChanged += Filtro_Changed;
            // 
            // filtro
            // 
            filtro.Dock = DockStyle.Fill;
            filtro.Location = new Point(141, 7);
            filtro.Margin = new Padding(3, 4, 3, 4);
            filtro.Name = "filtro";
            filtro.PlaceholderText = "Cerca tabella o colonna…";
            filtro.Size = new Size(127, 27);
            filtro.TabIndex = 1;
            filtro.TextChanged += Filtro_Changed;
            // 
            // soloAbilitati
            // 
            soloAbilitati.Anchor = AnchorStyles.Left;
            soloAbilitati.AutoSize = true;
            soloAbilitati.Location = new Point(274, 10);
            soloAbilitati.Margin = new Padding(3, 4, 3, 4);
            soloAbilitati.Name = "soloAbilitati";
            soloAbilitati.Size = new Size(120, 24);
            soloAbilitati.TabIndex = 2;
            soloAbilitati.Text = "Solo abilitate";
            soloAbilitati.CheckedChanged += Filtro_Changed;
            // 
            // rileggi
            // 
            rileggi.AutoSize = true;
            rileggi.FlatStyle = FlatStyle.Flat;
            rileggi.Location = new Point(400, 7);
            rileggi.Margin = new Padding(3, 4, 3, 4);
            rileggi.Name = "rileggi";
            rileggi.Size = new Size(186, 31);
            rileggi.TabIndex = 3;
            rileggi.Text = "Rileggi dal database";
            rileggi.Click += Rileggi_Click;
            // 
            // descrizione
            // 
            descrizione.AcceptsReturn = true;
            descrizione.Dock = DockStyle.Fill;
            descrizione.Enabled = false;
            descrizione.Location = new Point(9, 99);
            descrizione.Margin = new Padding(3, 4, 3, 4);
            descrizione.Multiline = true;
            descrizione.Name = "descrizione";
            descrizione.ScrollBars = ScrollBars.Vertical;
            descrizione.Size = new Size(434, 560);
            descrizione.TabIndex = 2;
            descrizione.TextChanged += Descrizione_TextChanged;
            // 
            // lblDescrizione
            // 
            lblDescrizione.Dock = DockStyle.Top;
            lblDescrizione.Location = new Point(9, 70);
            lblDescrizione.Name = "lblDescrizione";
            lblDescrizione.Size = new Size(434, 29);
            lblDescrizione.TabIndex = 1;
            lblDescrizione.Text = "Descrizione per l'AI";
            // 
            // selezione
            // 
            selezione.Dock = DockStyle.Top;
            selezione.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            selezione.Location = new Point(9, 11);
            selezione.Name = "selezione";
            selezione.Size = new Size(434, 59);
            selezione.TabIndex = 0;
            selezione.Text = "Seleziona una tabella o una colonna";
            // 
            // barra
            // 
            barra.AutoSize = true;
            barra.Controls.Add(annulla);
            barra.Controls.Add(salva);
            barra.Controls.Add(riepilogo);
            barra.Dock = DockStyle.Bottom;
            barra.FlowDirection = FlowDirection.RightToLeft;
            barra.Location = new Point(0, 729);
            barra.Margin = new Padding(3, 4, 3, 4);
            barra.Name = "barra";
            barra.Padding = new Padding(9, 11, 9, 11);
            barra.Size = new Size(1051, 71);
            barra.TabIndex = 2;
            // 
            // annulla
            // 
            annulla.AutoSize = true;
            annulla.DialogResult = DialogResult.Cancel;
            annulla.FlatStyle = FlatStyle.Flat;
            annulla.Location = new Point(940, 15);
            annulla.Margin = new Padding(3, 4, 3, 4);
            annulla.Name = "annulla";
            annulla.Padding = new Padding(7, 3, 7, 3);
            annulla.Size = new Size(90, 41);
            annulla.TabIndex = 1;
            annulla.Text = "Annulla";
            // 
            // salva
            // 
            salva.AutoSize = true;
            salva.FlatStyle = FlatStyle.Flat;
            salva.Location = new Point(862, 15);
            salva.Margin = new Padding(3, 4, 3, 4);
            salva.Name = "salva";
            salva.Padding = new Padding(7, 3, 7, 3);
            salva.Size = new Size(72, 41);
            salva.Stile = StilePulsante.Primario;
            salva.TabIndex = 0;
            salva.Text = "Salva";
            salva.Click += Salva_Click;
            // 
            // riepilogo
            // 
            riepilogo.AutoSize = true;
            riepilogo.Location = new Point(845, 23);
            riepilogo.Margin = new Padding(3, 12, 14, 4);
            riepilogo.Name = "riepilogo";
            riepilogo.Size = new Size(0, 20);
            riepilogo.TabIndex = 2;
            // 
            // StrutturaForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = annulla;
            ClientSize = new Size(1051, 800);
            Controls.Add(divisore);
            Controls.Add(nota);
            Controls.Add(barra);
            Margin = new Padding(3, 4, 3, 4);
            MinimizeBox = false;
            MinimumSize = new Size(797, 544);
            Name = "StrutturaForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Struttura del database";
            FormClosing += StrutturaForm_FormClosing;
            Shown += StrutturaForm_Shown;
            divisore.Panel1.ResumeLayout(false);
            divisore.Panel2.ResumeLayout(false);
            divisore.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)divisore).EndInit();
            divisore.ResumeLayout(false);
            ricerca.ResumeLayout(false);
            ricerca.PerformLayout();
            barra.ResumeLayout(false);
            barra.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label nota;
        private System.Windows.Forms.SplitContainer divisore;
        private System.Windows.Forms.TreeView albero;
        private System.Windows.Forms.TableLayoutPanel ricerca;
        private System.Windows.Forms.TextBox filtroTabelle;
        private System.Windows.Forms.TextBox filtro;
        private System.Windows.Forms.CheckBox soloAbilitati;
        private GpOne.Desktop.Pulsante rileggi;
        private System.Windows.Forms.TextBox descrizione;
        private System.Windows.Forms.Label lblDescrizione;
        private System.Windows.Forms.Label selezione;
        private System.Windows.Forms.FlowLayoutPanel barra;
        private GpOne.Desktop.Pulsante annulla;
        private GpOne.Desktop.Pulsante salva;
        private System.Windows.Forms.Label riepilogo;
    }
}
