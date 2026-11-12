using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace GpOne.Desktop;

/// <summary>
/// Colori, caratteri e disegni comuni del desktop, gli stessi del web (styles.scss): viola #7c4dff, rosa #ff4f8b, arancio #ffa53d.
/// </summary>
internal static class Tema
{
    public static readonly Color Viola = Color.FromArgb(124, 77, 255);
    public static readonly Color ViolaScuro = Color.FromArgb(91, 43, 214);
    public static readonly Color ViolaChiaro = Color.FromArgb(239, 233, 255);
    public static readonly Color Rosa = Color.FromArgb(255, 79, 139);
    public static readonly Color RosaScuro = Color.FromArgb(194, 24, 91);
    public static readonly Color Arancio = Color.FromArgb(255, 165, 61);
    public static readonly Color ArancioScuro = Color.FromArgb(178, 94, 0);
    public static readonly Color Ciano = Color.FromArgb(34, 195, 238);
    public static readonly Color Verde = Color.FromArgb(45, 212, 167);
    public static readonly Color VerdeScuro = Color.FromArgb(15, 143, 110);

    public static readonly Color Sfondo = Color.FromArgb(248, 246, 255);
    public static readonly Color Superficie = Color.White;
    public static readonly Color SuperficieAlta = Color.FromArgb(243, 240, 250);
    public static readonly Color Bordo = Color.FromArgb(228, 222, 242);
    public static readonly Color Testo = Color.FromArgb(28, 27, 41);
    public static readonly Color TestoSecondario = Color.FromArgb(104, 100, 128);
    public static readonly Color SqlSfondo = Color.FromArgb(30, 27, 58);
    public static readonly Color SqlTesto = Color.FromArgb(233, 227, 255);
    public static readonly Color ErroreSfondo = Color.FromArgb(255, 240, 245);

    private static readonly string Famiglia = FontFamily.Families.Any(f => f.Name == "Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
    private static readonly string FamigliaTitoli = FontFamily.Families.Any(f => f.Name == "Segoe UI Variable Display") ? "Segoe UI Variable Display" : "Segoe UI";
    /// <summary>Icone di sistema (Windows 10 e 11).</summary>
    public const string FamigliaIcone = "Segoe MDL2 Assets";

    private static readonly Dictionary<(string, float, FontStyle), Font> Caratteri = [];

    public static Font Carattere(float punti, FontStyle stile = FontStyle.Regular) => Font(Famiglia, punti, stile);
    public static Font Titolo(float punti, FontStyle stile = FontStyle.Bold) => Font(FamigliaTitoli, punti, stile);
    public static Font Icone(float punti) => Font(FamigliaIcone, punti, FontStyle.Regular);
    /// <summary>Semigrassetto (pulsanti, etichette, titoli di elenco).</summary>
    public static Font Semigrassetto(float punti) => Font("Segoe UI Semibold", punti, FontStyle.Regular);

    /// <summary>
    /// Ridipinge sotto <paramref name="controllo"/> lo sfondo e il disegno del contenitore: trasparenza vera
    /// per controlli arrotondati appoggiati su sfondi a gradiente o su schede.
    /// </summary>
    public static void DipingiSfondoGenitore(Control controllo, Graphics g)
    {
        var genitore = controllo.Parent;
        var scostamento = controllo.Location;
        while (genitore is not null)
        {
            if (genitore is IDisegnaSfondo disegno)
            {
                var stato = g.Save();
                g.TranslateTransform(-scostamento.X, -scostamento.Y);
                disegno.DisegnaSfondo(g);
                g.Restore(stato);
                return;
            }
            if (genitore.BackColor.A == 255)
            {
                g.Clear(genitore.BackColor);
                return;
            }
            scostamento.Offset(genitore.Location);
            genitore = genitore.Parent;
        }
        g.Clear(Sfondo);
    }

    private static Font Font(string famiglia, float punti, FontStyle stile)
    {
        lock (Caratteri)
        {
            if (!Caratteri.TryGetValue((famiglia, punti, stile), out var f))
                Caratteri[(famiglia, punti, stile)] = f = new Font(famiglia, punti, stile);
            return f;
        }
    }

    /// <summary>Icone Segoe MDL2 Assets usate nell'applicazione.</summary>
    public static class Icona
    {
        public const string Chat = "";
        public const string Storico = "";
        public const string Invia = "";
        public const string Aggiungi = "";
        public const string Conferma = "";
        public const string Errore = "";
        public const string Attenzione = "";
        public const string Info = "";
        public const string Domanda = "";
        public const string Scarica = "";
        public const string MiPiace = "";
        public const string NonMiPiace = "";
        public const string Modifica = "";
        public const string Copia = "";
        public const string Lucchetto = "";
        public const string Grafico = "";
        public const string Griglia = "";
        public const string Codice = "";
        public const string Cerca = "";
        public const string Impostazioni = "";
        public const string Annulla = "";
        public const string Tempo = "";
    }

    /// <summary>Rettangolo con angoli arrotondati.</summary>
    public static GraphicsPath Arrotondato(RectangleF r, float raggio)
    {
        var path = new GraphicsPath();
        var d = Math.Min(raggio * 2, Math.Min(r.Width, r.Height));
        if (d <= 1)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Gradiente GpOne viola → rosa → arancio in diagonale.</summary>
    public static LinearGradientBrush Gradiente(RectangleF r, int alfa = 255)
    {
        var area = r.Width < 1 || r.Height < 1 ? new RectangleF(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)) : r;
        var pennello = new LinearGradientBrush(area, Viola, Arancio, 35f);
        pennello.InterpolationColors = new ColorBlend
        {
            Colors = [Color.FromArgb(alfa, Viola), Color.FromArgb(alfa, Rosa), Color.FromArgb(alfa, Arancio)],
            Positions = [0f, 0.6f, 1f]
        };
        return pennello;
    }

    /// <summary>Gradiente "freddo" viola → ciano (avatar, accenti).</summary>
    public static LinearGradientBrush GradienteFreddo(RectangleF r) =>
        new(r.Width < 1 ? new RectangleF(r.X, r.Y, 1, 1) : r, Viola, Ciano, 35f);

    public static void Qualita(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    }

    /// <summary>Ombra morbida sotto una scheda (più rettangoli trasparenti sfumati).</summary>
    public static void Ombra(Graphics g, RectangleF r, float raggio, int intensita = 10)
    {
        for (var i = 1; i <= 4; i++)
        {
            var rr = new RectangleF(r.X - i + 1, r.Y - i + 3, r.Width + (i - 1) * 2, r.Height + (i - 1) * 2);
            using var path = Arrotondato(rr, raggio + i);
            using var b = new SolidBrush(Color.FromArgb(Math.Max(1, intensita - i * 2), 80, 40, 180));
            g.FillPath(b, path);
        }
    }

    /// <summary>Logo GpOne (lo stesso del web): quadrato a gradiente, fumetto bianco con tre barre.</summary>
    public static void DisegnaLogo(Graphics g, RectangleF r)
    {
        var s = r.Width / 64f;
        PointF P(float x, float y) => new(r.X + x * s, r.Y + y * s);
        RectangleF R(float x, float y, float w, float h) => new(r.X + x * s, r.Y + y * s, w * s, h * s);

        using (var sfondo = Arrotondato(r, 16 * s))
        using (var b = Gradiente(r))
            g.FillPath(b, sfondo);

        using (var fumetto = new GraphicsPath())
        {
            fumetto.AddPath(Arrotondato(R(15, 12, 34, 30), 6 * s), false);
            fumetto.AddPolygon([P(21, 41), P(30, 41), P(20, 50)]);
            g.FillPath(Brushes.White, fumetto);
        }
        void Barra(float x, float y, float h, Color c)
        {
            using var p = Arrotondato(R(x, y, 5, h), 2 * s);
            using var b = new SolidBrush(c);
            g.FillPath(b, p);
        }
        Barra(22, 27, 9, Viola);
        Barra(30, 21, 15, Rosa);
        Barra(38, 24, 12, Arancio);
        using (var punto = new SolidBrush(Color.FromArgb(230, Color.White)))
            g.FillEllipse(punto, R(48, 10, 6, 6));
    }

    private static Icon? icona;

    /// <summary>Icona della finestra disegnata dal logo.</summary>
    public static Icon IconaApp
    {
        get
        {
            if (icona is not null)
                return icona;
            using var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                Qualita(g);
                DisegnaLogo(g, new RectangleF(0, 0, 64, 64));
            }
            return icona = Icon.FromHandle(bmp.GetHicon());
        }
    }

    /// <summary>Testo con gradiente (titoli).</summary>
    public static void TestoGradiente(Graphics g, string testo, Font font, PointF origine)
    {
        using var path = new GraphicsPath();
        path.AddString(testo, font.FontFamily, (int)font.Style, g.DpiY * font.SizeInPoints / 72f, origine, StringFormat.GenericTypographic);
        var limiti = path.GetBounds();
        using var b = Gradiente(limiti);
        g.FillPath(b, path);
    }

    /// <summary>
    /// Aspetto GpOne per una finestra disegnata con il Designer: sfondo, griglie, caselle, menu e barre.
    /// Il pulsante predefinito (AcceptButton) diventa quello principale a gradiente.
    /// </summary>
    public static void Applica(Form form)
    {
        form.BackColor = Sfondo;
        form.ForeColor = Testo;
        form.Icon = IconaApp;
        if (form.AcceptButton is Pulsante principale)
            principale.Stile = StilePulsante.Primario;
        ApplicaControlli(form.Controls);
    }

    private static void ApplicaControlli(Control.ControlCollection controlli)
    {
        foreach (Control c in controlli)
        {
            switch (c)
            {
                case DataGridView griglia:
                    StileGriglia(griglia);
                    break;
                case TextBox { ReadOnly: false } casella:
                    casella.BackColor = Superficie;
                    casella.ForeColor = Testo;
                    break;
                case ListBox elenco when elenco is not ElencoVoci:
                    elenco.BorderStyle = BorderStyle.None;
                    elenco.BackColor = Superficie;
                    break;
                case ToolStrip barra:
                    barra.Renderer = Renderer;
                    barra.BackColor = Superficie;
                    break;
                case CheckBox casellaSpunta:
                    casellaSpunta.ForeColor = Testo;
                    break;
            }
            if (c.HasChildren)
                ApplicaControlli(c.Controls);
        }
    }

    public static void StileGriglia(DataGridView griglia)
    {
        griglia.EnableHeadersVisualStyles = false;
        griglia.BorderStyle = BorderStyle.None;
        griglia.BackgroundColor = Superficie;
        griglia.GridColor = Color.FromArgb(238, 234, 247);
        griglia.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        griglia.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        griglia.RowHeadersVisible = false;
        griglia.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        griglia.ColumnHeadersHeight = 38;
        griglia.RowTemplate.Height = 32;
        griglia.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = SuperficieAlta,
            ForeColor = ViolaScuro,
            SelectionBackColor = SuperficieAlta,
            SelectionForeColor = ViolaScuro,
            Font = Carattere(9.5f, FontStyle.Bold),
            Padding = new Padding(8, 0, 8, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };
        griglia.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Superficie,
            ForeColor = Testo,
            SelectionBackColor = ViolaChiaro,
            SelectionForeColor = Testo,
            Font = Carattere(9.5f),
            Padding = new Padding(8, 0, 8, 0)
        };
        griglia.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(251, 250, 255) };
    }

    public static readonly ToolStripRenderer Renderer = new RendererGpOne();

    private sealed class RendererGpOne() : ToolStripProfessionalRenderer(new ColoriMenu())
    {
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown)
                base.OnRenderToolStripBorder(e);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.Selected && e.Item.Enabled && e.Item.Owner is not StatusStrip)
                e.TextColor = ViolaScuro;
            base.OnRenderItemText(e);
        }
    }

    private sealed class ColoriMenu : ProfessionalColorTable
    {
        public override Color MenuItemSelected => ViolaChiaro;
        public override Color MenuItemSelectedGradientBegin => ViolaChiaro;
        public override Color MenuItemSelectedGradientEnd => ViolaChiaro;
        public override Color MenuItemPressedGradientBegin => ViolaChiaro;
        public override Color MenuItemPressedGradientEnd => ViolaChiaro;
        public override Color MenuItemBorder => Color.FromArgb(214, 200, 255);
        public override Color MenuBorder => Bordo;
        public override Color ToolStripDropDownBackground => Superficie;
        public override Color ImageMarginGradientBegin => Superficie;
        public override Color ImageMarginGradientMiddle => Superficie;
        public override Color ImageMarginGradientEnd => Superficie;
        public override Color MenuStripGradientBegin => Superficie;
        public override Color MenuStripGradientEnd => Superficie;
        public override Color StatusStripGradientBegin => Superficie;
        public override Color StatusStripGradientEnd => Superficie;
        public override Color SeparatorDark => Bordo;
        public override Color SeparatorLight => Superficie;
    }
}
