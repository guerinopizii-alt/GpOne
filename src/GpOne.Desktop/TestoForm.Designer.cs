namespace GpOne.Desktop
{
    partial class TestoForm
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
            domanda = new System.Windows.Forms.Label();
            contenitore = new System.Windows.Forms.Panel();
            campo = new System.Windows.Forms.TextBox();
            barra = new System.Windows.Forms.FlowLayoutPanel();
            annulla = new GpOne.Desktop.Pulsante();
            ok = new GpOne.Desktop.Pulsante();
            contenitore.SuspendLayout();
            barra.SuspendLayout();
            SuspendLayout();
            //
            // domanda
            //
            domanda.Dock = System.Windows.Forms.DockStyle.Top;
            domanda.Location = new System.Drawing.Point(0, 0);
            domanda.Name = "domanda";
            domanda.Padding = new System.Windows.Forms.Padding(10, 8, 10, 0);
            domanda.Size = new System.Drawing.Size(480, 40);
            domanda.TabIndex = 1;
            domanda.Text = "Testo";
            //
            // contenitore
            //
            contenitore.Controls.Add(campo);
            contenitore.Dock = System.Windows.Forms.DockStyle.Fill;
            contenitore.Location = new System.Drawing.Point(0, 40);
            contenitore.Name = "contenitore";
            contenitore.Padding = new System.Windows.Forms.Padding(10, 4, 10, 0);
            contenitore.Size = new System.Drawing.Size(480, 131);
            contenitore.TabIndex = 0;
            //
            // campo
            //
            campo.AcceptsReturn = true;
            campo.Dock = System.Windows.Forms.DockStyle.Fill;
            campo.Multiline = true;
            campo.Name = "campo";
            campo.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            campo.TabIndex = 0;
            campo.KeyDown += Campo_KeyDown;
            //
            // barra
            //
            barra.AutoSize = true;
            barra.Controls.Add(annulla);
            barra.Controls.Add(ok);
            barra.Dock = System.Windows.Forms.DockStyle.Bottom;
            barra.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            barra.Location = new System.Drawing.Point(0, 171);
            barra.Name = "barra";
            barra.Padding = new System.Windows.Forms.Padding(8);
            barra.Size = new System.Drawing.Size(480, 49);
            barra.TabIndex = 2;
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
            ok.DialogResult = System.Windows.Forms.DialogResult.OK;
            ok.Name = "ok";
            ok.Stile = GpOne.Desktop.StilePulsante.Primario;
            ok.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
            ok.TabIndex = 0;
            ok.Text = "Riprova";
            //
            // TestoForm
            //
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = annulla;
            ClientSize = new System.Drawing.Size(480, 220);
            Controls.Add(contenitore);
            Controls.Add(domanda);
            Controls.Add(barra);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "TestoForm";
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "GpOne";
            contenitore.ResumeLayout(false);
            contenitore.PerformLayout();
            barra.ResumeLayout(false);
            barra.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label domanda;
        private System.Windows.Forms.Panel contenitore;
        private System.Windows.Forms.TextBox campo;
        private System.Windows.Forms.FlowLayoutPanel barra;
        private GpOne.Desktop.Pulsante annulla;
        private GpOne.Desktop.Pulsante ok;
    }
}
