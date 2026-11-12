namespace GpOne.Desktop
{
    partial class MessaggioForm
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
            intestazione = new System.Windows.Forms.TableLayoutPanel();
            icona = new System.Windows.Forms.PictureBox();
            titolo = new System.Windows.Forms.Label();
            contenitore = new System.Windows.Forms.Panel();
            testo = new System.Windows.Forms.TextBox();
            barra = new System.Windows.Forms.FlowLayoutPanel();
            ok = new GpOne.Desktop.Pulsante();
            copia = new GpOne.Desktop.Pulsante();
            intestazione.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)icona).BeginInit();
            contenitore.SuspendLayout();
            barra.SuspendLayout();
            SuspendLayout();
            //
            // intestazione
            //
            intestazione.AutoSize = true;
            intestazione.ColumnCount = 2;
            intestazione.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            intestazione.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            intestazione.Controls.Add(icona, 0, 0);
            intestazione.Controls.Add(titolo, 1, 0);
            intestazione.Dock = System.Windows.Forms.DockStyle.Top;
            intestazione.Location = new System.Drawing.Point(0, 0);
            intestazione.Name = "intestazione";
            intestazione.Padding = new System.Windows.Forms.Padding(8, 8, 8, 0);
            intestazione.RowCount = 1;
            intestazione.RowStyles.Add(new System.Windows.Forms.RowStyle());
            intestazione.Size = new System.Drawing.Size(560, 46);
            intestazione.TabIndex = 2;
            //
            // icona
            //
            icona.Name = "icona";
            icona.Size = new System.Drawing.Size(32, 32);
            icona.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            icona.TabStop = false;
            //
            // titolo
            //
            titolo.Anchor = System.Windows.Forms.AnchorStyles.Left;
            titolo.AutoSize = true;
            titolo.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            titolo.Name = "titolo";
            titolo.Text = "Messaggio";
            //
            // contenitore
            //
            contenitore.Controls.Add(testo);
            contenitore.Dock = System.Windows.Forms.DockStyle.Fill;
            contenitore.Location = new System.Drawing.Point(0, 46);
            contenitore.Name = "contenitore";
            contenitore.Padding = new System.Windows.Forms.Padding(10, 6, 10, 0);
            contenitore.Size = new System.Drawing.Size(560, 225);
            contenitore.TabIndex = 0;
            //
            // testo
            //
            testo.BackColor = System.Drawing.SystemColors.Window;
            testo.Dock = System.Windows.Forms.DockStyle.Fill;
            testo.Multiline = true;
            testo.Name = "testo";
            testo.ReadOnly = true;
            testo.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            testo.TabIndex = 0;
            testo.WordWrap = true;
            //
            // barra
            //
            barra.AutoSize = true;
            barra.Controls.Add(ok);
            barra.Controls.Add(copia);
            barra.Dock = System.Windows.Forms.DockStyle.Bottom;
            barra.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            barra.Location = new System.Drawing.Point(0, 271);
            barra.Name = "barra";
            barra.Padding = new System.Windows.Forms.Padding(8);
            barra.Size = new System.Drawing.Size(560, 49);
            barra.TabIndex = 1;
            //
            // ok
            //
            ok.AutoSize = true;
            ok.DialogResult = System.Windows.Forms.DialogResult.OK;
            ok.Name = "ok";
            ok.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
            ok.TabIndex = 0;
            ok.Text = "OK";
            //
            // copia
            //
            copia.AutoSize = true;
            copia.Name = "copia";
            copia.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
            copia.TabIndex = 1;
            copia.Text = "Copia";
            copia.Click += Copia_Click;
            //
            // MessaggioForm
            //
            AcceptButton = ok;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = ok;
            ClientSize = new System.Drawing.Size(560, 320);
            Controls.Add(contenitore);
            Controls.Add(intestazione);
            Controls.Add(barra);
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(360, 220);
            Name = "MessaggioForm";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "GpOne";
            intestazione.ResumeLayout(false);
            intestazione.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)icona).EndInit();
            contenitore.ResumeLayout(false);
            contenitore.PerformLayout();
            barra.ResumeLayout(false);
            barra.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel intestazione;
        private System.Windows.Forms.PictureBox icona;
        private System.Windows.Forms.Label titolo;
        private System.Windows.Forms.Panel contenitore;
        private System.Windows.Forms.TextBox testo;
        private System.Windows.Forms.FlowLayoutPanel barra;
        private GpOne.Desktop.Pulsante ok;
        private GpOne.Desktop.Pulsante copia;
    }
}
