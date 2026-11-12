using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace GpOne.Desktop;

// Controlli disegnati con lo stile GpOne del web. Si usano dal Designer come quelli standard:
// le proprietà serializzabili sono solo stile, icona e colori; testi e dati si impostano nel .cs.

/// <summary>Contenitore che sa disegnare il proprio sfondo (per i controlli figli "trasparenti" arrotondati).</summary>
internal interface IDisegnaSfondo
{
    void DisegnaSfondo(Graphics g);
}

public enum StilePulsante { Secondario, Primario, Testo, Pericolo }

/// <summary>Pulsante arrotondato: Primario a gradiente, Secondario con bordo, Testo senza sfondo.</summary>
public class Pulsante : Button
{
    private bool sopra, premuto;
    private StilePulsante stile;
    private string icona = string.Empty;

    public Pulsante()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
    }

    [DefaultValue(StilePulsante.Secondario), Category("GpOne")]
    public StilePulsante Stile { get => stile; set { stile = value; Invalidate(); } }

    /// <summary>Icona Segoe MDL2 Assets prima del testo (es. "").</summary>
    [DefaultValue(""), Category("GpOne")]
    public string Icona { get => icona; set { icona = value ?? string.Empty; Invalidate(); } }

    private Font CarattereTesto => Tema.Semigrassetto(9.75f);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var testo = TextRenderer.MeasureText(Text.Length == 0 ? " " : Text, CarattereTesto, Size.Empty, TextFormatFlags.NoPadding);
        var larghezza = Text.Length == 0 ? 0 : testo.Width;
        if (icona.Length > 0)
            larghezza += 18 + (Text.Length == 0 ? 0 : 6);
        return new Size(larghezza + 32, Math.Max(38, testo.Height + 18));
    }

    protected override void OnMouseEnter(EventArgs e) { sopra = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { sopra = premuto = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { premuto = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { premuto = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); if (AutoSize) PerformLayout(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Tema.DipingiSfondoGenitore(this, g);
        Tema.Qualita(g);

        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var forma = Tema.Arrotondato(r, Math.Min(12, r.Height / 2));
        Color testo;
        switch (stile)
        {
            case StilePulsante.Primario when Enabled:
                using (var b = Tema.Gradiente(r))
                    g.FillPath(b, forma);
                if (sopra || premuto)
                {
                    using var velo = new SolidBrush(premuto ? Color.FromArgb(30, 0, 0, 0) : Color.FromArgb(28, 255, 255, 255));
                    g.FillPath(velo, forma);
                }
                testo = Color.White;
                break;
            case StilePulsante.Primario:
                using (var b = new SolidBrush(Color.FromArgb(220, 214, 236)))
                    g.FillPath(b, forma);
                testo = Color.White;
                break;
            case StilePulsante.Testo:
                if (sopra && Enabled)
                {
                    using var b = new SolidBrush(premuto ? Color.FromArgb(226, 216, 255) : Tema.ViolaChiaro);
                    g.FillPath(b, forma);
                }
                testo = Enabled ? Tema.ViolaScuro : Tema.TestoSecondario;
                break;
            default:
                var pericolo = stile == StilePulsante.Pericolo;
                using (var b = new SolidBrush(!Enabled ? Tema.SuperficieAlta
                           : sopra ? (pericolo ? Tema.ErroreSfondo : Tema.ViolaChiaro) : Tema.Superficie))
                    g.FillPath(b, forma);
                using (var p = new Pen(sopra && Enabled ? (pericolo ? Tema.Rosa : Tema.Viola) : Tema.Bordo, 1.2f))
                    g.DrawPath(p, forma);
                testo = !Enabled ? Tema.TestoSecondario : pericolo ? Tema.RosaScuro : Tema.ViolaScuro;
                break;
        }

        if (Focused && ShowFocusCues)
        {
            using var anello = Tema.Arrotondato(RectangleF.Inflate(r, -2.5f, -2.5f), Math.Min(10, r.Height / 2 - 2));
            using var p = new Pen(Color.FromArgb(stile == StilePulsante.Primario ? 200 : 140, stile == StilePulsante.Primario ? Color.White : Tema.Viola), 1.5f);
            g.DrawPath(p, anello);
        }

        // Icona + testo centrati
        var misuraTesto = Text.Length == 0 ? Size.Empty : TextRenderer.MeasureText(g, Text, CarattereTesto, Size.Empty, TextFormatFlags.NoPadding);
        var larghezzaIcona = icona.Length == 0 ? 0 : 16;
        var spazio = icona.Length > 0 && Text.Length > 0 ? 7 : 0;
        var totale = larghezzaIcona + spazio + misuraTesto.Width;
        var x = (Width - totale) / 2;
        if (icona.Length > 0)
            TextRenderer.DrawText(g, icona, Tema.Icone(11f), new Rectangle(x, 0, larghezzaIcona + 2, Height), testo,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        if (Text.Length > 0)
            TextRenderer.DrawText(g, Text, CarattereTesto, new Rectangle(x + larghezzaIcona + spazio, 0, misuraTesto.Width + 4, Height), testo,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }
}

/// <summary>Sfondo dell'area principale: tinta chiara con aloni viola, rosa e arancio (come il web).</summary>
public class PannelloSfondo : Panel, IDisegnaSfondo
{
    public PannelloSfondo()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Tema.Sfondo;
    }

    protected override void OnPaintBackground(PaintEventArgs e) => DisegnaSfondo(e.Graphics);

    public void DisegnaSfondo(Graphics g)
    {
        g.Clear(Tema.Sfondo);
        Alone(g, new RectangleF(-Width * 0.35f, -Height * 0.45f, Width * 0.9f, Height * 0.9f), Color.FromArgb(40, Tema.Viola));
        Alone(g, new RectangleF(Width * 0.55f, -Height * 0.4f, Width * 0.75f, Height * 0.85f), Color.FromArgb(32, Tema.Rosa));
        Alone(g, new RectangleF(Width * 0.45f, Height * 0.55f, Width * 0.85f, Height * 0.8f), Color.FromArgb(30, Tema.Arancio));
    }

    private static void Alone(Graphics g, RectangleF r, Color centro)
    {
        if (r.Width < 2 || r.Height < 2)
            return;
        using var path = new GraphicsPath();
        path.AddEllipse(r);
        using var b = new PathGradientBrush(path) { CenterColor = centro, SurroundColors = [Color.FromArgb(0, centro)] };
        g.FillPath(b, path);
    }
}

/// <summary>Contenitore trasparente: mostra lo sfondo del contenitore sotto (gradiente o scheda).</summary>
public class PannelloTrasparente : Panel, IDisegnaSfondo
{
    public PannelloTrasparente()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e) => DisegnaSfondo(e.Graphics);
    public void DisegnaSfondo(Graphics g) => Tema.DipingiSfondoGenitore(this, g);
}

/// <summary>Riga di controlli trasparente (pulsanti, etichette di stato) che segue lo sfondo del contenitore.</summary>
public class RigaTrasparente : FlowLayoutPanel, IDisegnaSfondo
{
    public RigaTrasparente()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e) => DisegnaSfondo(e.Graphics);
    public void DisegnaSfondo(Graphics g) => Tema.DipingiSfondoGenitore(this, g);
}

/// <summary>Scheda bianca arrotondata con bordo e ombra leggera. <see cref="Rientro"/>: spazio fuori dalla scheda.</summary>
public class Scheda : Panel, IDisegnaSfondo
{
    private Color colore = Color.White;
    private Color coloreBordo = Tema.Bordo;
    private Padding rientro = new(0, 0, 0, 0);
    private int raggio = 18;

    public Scheda()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
    }

    [Category("GpOne")]
    public Color Colore { get => colore; set { colore = value; Invalidate(); } }
    private bool ShouldSerializeColore() => colore != Color.White;
    private void ResetColore() => Colore = Color.White;

    [Category("GpOne")]
    public Color ColoreBordo { get => coloreBordo; set { coloreBordo = value; Invalidate(); } }
    private bool ShouldSerializeColoreBordo() => coloreBordo != Tema.Bordo;
    private void ResetColoreBordo() => ColoreBordo = Tema.Bordo;

    /// <summary>Margine tra il bordo del controllo e la scheda disegnata (per distanziare le schede impilate).</summary>
    [Category("GpOne")]
    public Padding Rientro { get => rientro; set { rientro = value; Invalidate(); } }
    private bool ShouldSerializeRientro() => rientro != Padding.Empty;
    private void ResetRientro() => Rientro = Padding.Empty;

    [DefaultValue(18), Category("GpOne")]
    public int Raggio { get => raggio; set { raggio = value; Invalidate(); } }

    protected override void OnPaintBackground(PaintEventArgs e) => DisegnaSfondo(e.Graphics);

    public void DisegnaSfondo(Graphics g)
    {
        Tema.DipingiSfondoGenitore(this, g);
        Tema.Qualita(g);
        var r = new RectangleF(rientro.Left + 0.5f, rientro.Top + 0.5f,
            Width - rientro.Horizontal - 1.5f, Height - rientro.Vertical - 1.5f);
        if (r.Width < 4 || r.Height < 4)
            return;
        Tema.Ombra(g, r, raggio, 9);
        using var forma = Tema.Arrotondato(r, raggio);
        using (var b = new SolidBrush(colore))
            g.FillPath(b, forma);
        using (var p = new Pen(coloreBordo))
            g.DrawPath(p, forma);
    }
}

/// <summary>Casella della domanda: bianca, molto arrotondata, con bordo a gradiente (come il web).</summary>
public class Contorno : Panel, IDisegnaSfondo
{
    public Contorno()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e) => DisegnaSfondo(e.Graphics);

    public void DisegnaSfondo(Graphics g)
    {
        Tema.DipingiSfondoGenitore(this, g);
        Tema.Qualita(g);
        var r = new RectangleF(2, 2, Width - 5, Height - 6);
        if (r.Width < 10 || r.Height < 10)
            return;
        Tema.Ombra(g, r, 22, 12);
        using var forma = Tema.Arrotondato(r, 22);
        g.FillPath(Brushes.White, forma);
        using var pennello = Tema.Gradiente(r);
        using var p = new Pen(pennello, 2f);
        g.DrawPath(p, forma);
    }
}

/// <summary>Fumetto della domanda dell'utente: a destra, a gradiente, con l'avatar delle iniziali.</summary>
public class Fumetto : Control
{
    private const int Avatar = 36, Distanza = 12, MargineSotto = 14;
    private string etichetta = string.Empty;

    public Fumetto()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
    }

    /// <summary>Riga piccola sopra il testo (es. "Correzione"), vuota = nessuna.</summary>
    [DefaultValue(""), Category("GpOne")]
    public string Etichetta { get => etichetta; set { etichetta = value ?? string.Empty; AdattaAltezza(); Invalidate(); } }

    private static Font CarattereTesto => Tema.Carattere(11f);
    private static Font CarattereEtichetta => Tema.Semigrassetto(8.5f);

    private static string Iniziali
    {
        get
        {
            var nome = Environment.UserName;
            var parti = nome.Split(['.', ' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
            return (parti.Length switch { 0 => "?", 1 => parti[0][..Math.Min(2, parti[0].Length)], _ => $"{parti[0][0]}{parti[1][0]}" }).ToUpperInvariant();
        }
    }

    private (Rectangle Fumetto, Rectangle Testo) Misura()
    {
        var massima = Math.Max(80, Math.Min(680, (int)((Width - Avatar - Distanza) * 0.82)));
        var testo = TextRenderer.MeasureText(Text.Length == 0 ? " " : Text, CarattereTesto, new Size(massima - 32, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
        var altezzaEtichetta = etichetta.Length == 0 ? 0 : 20;
        var larghezza = Math.Max(testo.Width + 34, etichetta.Length == 0 ? 0 : 140);
        var fumetto = new Rectangle(Width - Avatar - Distanza - larghezza, 0, larghezza, testo.Height + 24 + altezzaEtichetta);
        return (fumetto, new Rectangle(fumetto.X + 16, fumetto.Y + 12 + altezzaEtichetta, testo.Width + 4, testo.Height));
    }

    private void AdattaAltezza()
    {
        var altezza = Math.Max(Avatar, Misura().Fumetto.Height) + MargineSotto;
        if (Height != altezza)
            Height = altezza;
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); AdattaAltezza(); Invalidate(); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); AdattaAltezza(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Tema.DipingiSfondoGenitore(this, g);
        Tema.Qualita(g);
        var (fumetto, testo) = Misura();

        Tema.Ombra(g, fumetto, 18, 8);
        using (var forma = new GraphicsPath())
        {
            // Angolo in basso a destra meno arrotondato, verso l'avatar
            float r = 18, d = r * 2, piccolo = 8;
            RectangleF f = fumetto;
            forma.AddArc(f.X, f.Y, d, d, 180, 90);
            forma.AddArc(f.Right - d, f.Y, d, d, 270, 90);
            forma.AddArc(f.Right - piccolo, f.Bottom - piccolo, piccolo, piccolo, 0, 90);
            forma.AddArc(f.X, f.Bottom - d, d, d, 90, 90);
            forma.CloseFigure();
            using var b = Tema.Gradiente(f);
            g.FillPath(b, forma);
        }
        if (etichetta.Length > 0)
        {
            TextRenderer.DrawText(g, Tema.Icona.Modifica, Tema.Icone(9f), new Point(fumetto.X + 16, fumetto.Y + 12), Color.FromArgb(235, Color.White), TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, etichetta, CarattereEtichetta, new Point(fumetto.X + 34, fumetto.Y + 10), Color.FromArgb(235, Color.White), TextFormatFlags.NoPadding);
        }
        TextRenderer.DrawText(g, Text, CarattereTesto, testo, Color.White,
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);

        var avatar = new Rectangle(Width - Avatar - 1, Math.Max(0, fumetto.Bottom - Avatar), Avatar, Avatar);
        using (var forma = Tema.Arrotondato(avatar, 12))
        using (var b = Tema.GradienteFreddo(avatar))
            g.FillPath(b, forma);
        TextRenderer.DrawText(g, Iniziali, Tema.Semigrassetto(9.5f), avatar, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

public enum VarianteEtichetta { Viola, Ok, Attenzione, Errore, Neutro }

/// <summary>Etichetta di stato a pillola colorata (righe, tempo, troncato…).</summary>
public class Etichetta : Label
{
    private VarianteEtichetta variante;
    private string icona = string.Empty;

    public Etichetta()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        AutoSize = true;
    }

    [DefaultValue(VarianteEtichetta.Viola), Category("GpOne")]
    public VarianteEtichetta Variante { get => variante; set { variante = value; Invalidate(); } }

    [DefaultValue(""), Category("GpOne")]
    public string Icona { get => icona; set { icona = value ?? string.Empty; AggiornaMisura(); } }

    [DefaultValue(true)]
    public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }

    private static Font CarattereTesto => Tema.Semigrassetto(8.75f);

    private void AggiornaMisura() { if (AutoSize) Size = GetPreferredSize(Size.Empty); Invalidate(); }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var testo = TextRenderer.MeasureText(Text.Length == 0 ? " " : Text, CarattereTesto, Size.Empty, TextFormatFlags.NoPadding);
        return new Size(testo.Width + 22 + (icona.Length > 0 ? 18 : 0), 26);
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); AggiornaMisura(); }

    private (Color Sfondo, Color Testo) Colori => variante switch
    {
        VarianteEtichetta.Ok => (Color.FromArgb(222, 247, 239), Tema.VerdeScuro),
        VarianteEtichetta.Attenzione => (Color.FromArgb(255, 238, 214), Tema.ArancioScuro),
        VarianteEtichetta.Errore => (Color.FromArgb(255, 227, 237), Tema.RosaScuro),
        VarianteEtichetta.Neutro => (Tema.SuperficieAlta, Tema.TestoSecondario),
        _ => (Color.FromArgb(234, 226, 255), Tema.ViolaScuro)
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Tema.DipingiSfondoGenitore(this, g);
        Tema.Qualita(g);
        var (sfondo, colore) = Colori;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var forma = Tema.Arrotondato(r, r.Height / 2))
        using (var b = new SolidBrush(sfondo))
            g.FillPath(b, forma);
        var x = 11;
        if (icona.Length > 0)
        {
            TextRenderer.DrawText(g, icona, Tema.Icone(9f), new Rectangle(x, 0, 16, Height), colore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            x += 18;
        }
        TextRenderer.DrawText(g, Text, CarattereTesto, new Rectangle(x, 0, Width - x, Height), colore,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }
}

/// <summary>Voce disegnata da <see cref="ElencoVoci"/>: titolo, dettaglio e icona colorata.</summary>
public interface IVoceElenco
{
    string Titolo { get; }
    string Dettaglio { get; }
    /// <summary>Glifo Segoe MDL2 Assets.</summary>
    string Icona { get; }
    Color Colore { get; }
}

/// <summary>Elenco a voci arrotondate con icona, titolo e dettaglio (conversazione e storico).</summary>
public class ElencoVoci : ListBox
{
    public ElencoVoci()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 60;
        BorderStyle = BorderStyle.None;
        IntegralHeight = false;
        BackColor = Color.White;
    }

    [DefaultValue(DrawMode.OwnerDrawFixed)]
    public override DrawMode DrawMode { get => base.DrawMode; set => base.DrawMode = value; }

    [DefaultValue(60)]
    public override int ItemHeight { get => base.ItemHeight; set => base.ItemHeight = value; }

    [DefaultValue(BorderStyle.None)]
    public new BorderStyle BorderStyle { get => base.BorderStyle; set => base.BorderStyle = value; }

    [DefaultValue(false)]
    public new bool IntegralHeight { get => base.IntegralHeight; set => base.IntegralHeight = value; }

    [DefaultValue(typeof(Color), "White")]
    public override Color BackColor { get => base.BackColor; set => base.BackColor = value; }

    private string testoVuoto = string.Empty;

    /// <summary>Messaggio mostrato al centro quando l'elenco è vuoto.</summary>
    [DefaultValue(""), Category("GpOne")]
    public string TestoVuoto { get => testoVuoto; set { testoVuoto = value ?? string.Empty; Invalidate(); } }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        const int WM_PAINT = 0x000F, WM_PRINTCLIENT = 0x0318;
        if (Items.Count > 0 || testoVuoto.Length == 0)
            return;
        if (m.Msg == WM_PAINT)
        {
            using var g = CreateGraphics();
            DisegnaVuoto(g);
        }
        else if (m.Msg == WM_PRINTCLIENT)
        {
            using var g = Graphics.FromHdc(m.WParam);
            DisegnaVuoto(g);
        }
    }

    private void DisegnaVuoto(Graphics g) =>
        TextRenderer.DrawText(g, testoVuoto, Tema.Carattere(10f), Rectangle.Inflate(ClientRectangle, -24, -24), Tema.TestoSecondario,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var g = e.Graphics;
        using (var fondo = new SolidBrush(BackColor))
            g.FillRectangle(fondo, e.Bounds);
        if (e.Index < 0 || e.Index >= Items.Count)
            return;
        Tema.Qualita(g);

        var selezionata = (e.State & DrawItemState.Selected) != 0;
        var r = new RectangleF(e.Bounds.X + 8, e.Bounds.Y + 3, e.Bounds.Width - 16, e.Bounds.Height - 6);
        if (selezionata)
        {
            using var forma = Tema.Arrotondato(r, 14);
            using var b = new SolidBrush(Tema.ViolaChiaro);
            g.FillPath(b, forma);
        }

        var voce = Items[e.Index] as IVoceElenco;
        var titolo = voce?.Titolo ?? Items[e.Index]?.ToString() ?? string.Empty;
        var x = (int)r.X + 10;
        if (voce is not null)
        {
            var bolla = new Rectangle(x, (int)(r.Y + (r.Height - 34) / 2), 34, 34);
            using (var forma = Tema.Arrotondato(bolla, 11))
            using (var b = new LinearGradientBrush(bolla, voce.Colore, ControlPaint.Light(voce.Colore, 0.35f), 45f))
                g.FillPath(b, forma);
            TextRenderer.DrawText(g, voce.Icona, Tema.Icone(11f), bolla, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            x = bolla.Right + 12;
        }

        var larghezza = (int)r.Right - x - 8;
        var flag = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        var unaRiga = titolo.ReplaceLineEndings(" ");
        if (voce is null || voce.Dettaglio.Length == 0)
        {
            TextRenderer.DrawText(g, unaRiga, Tema.Carattere(9.75f), new Rectangle(x, e.Bounds.Y, larghezza, e.Bounds.Height),
                Tema.Testo, flag | TextFormatFlags.VerticalCenter);
            return;
        }
        TextRenderer.DrawText(g, unaRiga, Tema.Semigrassetto(9.75f), new Rectangle(x, (int)r.Y + 9, larghezza, 22),
            selezionata ? Tema.ViolaScuro : Tema.Testo, flag);
        TextRenderer.DrawText(g, voce.Dettaglio, Tema.Carattere(8.75f), new Rectangle(x, (int)r.Y + 30, larghezza, 20),
            Tema.TestoSecondario, flag);
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }
}

/// <summary>
/// Schede in stile Material: testo con icona, scheda attiva in viola con barra a gradiente sotto, niente bordi di sistema.
/// L'icona di ogni pagina è il glifo nel suo <see cref="Control.Tag"/>.
/// </summary>
public class SchedeTab : TabControl, IDisegnaSfondo
{
    private Color coloreSfondo = Color.White;

    public SchedeTab()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(140, 42);
    }

    [Category("GpOne")]
    public Color ColoreSfondo { get => coloreSfondo; set { coloreSfondo = value; foreach (TabPage p in TabPages) p.BackColor = value; Invalidate(); } }
    private bool ShouldSerializeColoreSfondo() => coloreSfondo != Color.White;
    private void ResetColoreSfondo() => ColoreSfondo = Color.White;

    [DefaultValue(TabSizeMode.Fixed)]
    public new TabSizeMode SizeMode { get => base.SizeMode; set => base.SizeMode = value; }

    private bool riempi;

    /// <summary>Le linguette si dividono tutta la larghezza (barra laterale), invece di avere larghezza fissa.</summary>
    [DefaultValue(false), Category("GpOne")]
    public bool Riempi { get => riempi; set { riempi = value; AdattaLinguette(); } }

    private bool adattando;

    private void AdattaLinguette()
    {
        if (!riempi || adattando || TabCount == 0 || Width <= 0)
            return;
        var larghezza = Math.Max(60, (Width - 2) / TabCount);
        if (ItemSize.Width == larghezza)
            return;
        // ItemSize ridimensiona il controllo per ricalcolare le linguette: OnResize non deve rientrare qui
        adattando = true;
        try
        {
            ItemSize = new Size(larghezza, ItemSize.Height);
        }
        finally
        {
            adattando = false;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        AdattaLinguette();
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is TabPage pagina)
        {
            pagina.UseVisualStyleBackColor = false;
            pagina.BackColor = coloreSfondo;
        }
        AdattaLinguette();
    }

    /// <summary>Area delle pagine: tutta la larghezza, subito sotto le linguette (senza bordi).</summary>
    public override Rectangle DisplayRectangle
    {
        get
        {
            var alto = ItemSize.Height + 4;
            return new Rectangle(0, alto, Width, Math.Max(0, Height - alto));
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    public void DisegnaSfondo(Graphics g)
    {
        using var b = new SolidBrush(coloreSfondo);
        g.FillRectangle(b, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        DisegnaSfondo(g);
        Tema.Qualita(g);
        var altezza = ItemSize.Height + 4;
        using (var linea = new Pen(Tema.Bordo))
            g.DrawLine(linea, 0, altezza - 1, Width, altezza - 1);

        for (var i = 0; i < TabPages.Count; i++)
        {
            var pagina = TabPages[i];
            var r = GetTabRect(i);
            r = new Rectangle(r.X, 0, r.Width, altezza);
            var attiva = i == SelectedIndex;
            var colore = attiva ? Tema.ViolaScuro : Tema.TestoSecondario;
            var font = attiva ? Tema.Semigrassetto(9.75f) : Tema.Carattere(9.75f);
            var icona = pagina.Tag as string ?? string.Empty;
            var testo = TextRenderer.MeasureText(g, pagina.Text, font, Size.Empty, TextFormatFlags.NoPadding);
            var totale = testo.Width + (icona.Length > 0 ? 24 : 0);
            var x = r.X + (r.Width - totale) / 2;
            if (icona.Length > 0)
            {
                TextRenderer.DrawText(g, icona, Tema.Icone(11f), new Rectangle(x, r.Y, 18, r.Height - 4), colore,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                x += 24;
            }
            TextRenderer.DrawText(g, pagina.Text, font, new Rectangle(x, r.Y, testo.Width + 4, r.Height - 4), colore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            if (attiva)
            {
                var barra = new RectangleF(r.X + r.Width / 2f - totale / 2f - 6, altezza - 4, totale + 12, 3.5f);
                using var forma = Tema.Arrotondato(barra, 1.75f);
                using var b = Tema.Gradiente(barra);
                g.FillPath(b, forma);
            }
        }
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }
}

/// <summary>Intestazione della barra laterale: logo, nome dell'applicazione e database aperto.</summary>
public class Intestazione : Control
{
    private string sottotitolo = string.Empty;

    public Intestazione()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Sottotitolo { get => sottotitolo; set { sottotitolo = value ?? string.Empty; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Color.White);
        Tema.Qualita(g);
        var logo = new RectangleF(16, (Height - 40) / 2f, 40, 40);
        Tema.DisegnaLogo(g, logo);
        var x = (int)logo.Right + 12;
        TextRenderer.DrawText(g, "GpOne", Tema.Titolo(14f), new Point(x, (int)logo.Y - 3), Tema.Testo, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, sottotitolo, Tema.Carattere(8.75f), new Rectangle(x, (int)logo.Y + 23, Width - x - 8, 20),
            Tema.TestoSecondario, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }
}

/// <summary>
/// Pagina di benvenuto: saluto, nota sulla riservatezza e suggerimenti di domanda cliccabili (come nel web).
/// Tutto disegnato: nessun controllo figlio.
/// </summary>
public class PannelloBenvenuto : Control
{
    private sealed record Suggerimento(string Icona, Color Colore, string Titolo, string Testo);

    private static readonly Suggerimento[] Suggerimenti =
    [
        new(Tema.Icona.Grafico, Tema.Viola, "Andamento", "Fatturato per mese di quest'anno confrontato con l'anno scorso"),
        new("", Tema.Rosa, "Classifiche", "I 10 clienti con il fatturato più alto"),
        new("", Tema.Arancio, "Ordini", "Ordini aperti raggruppati per stato"),
        new("", Tema.Ciano, "Prodotti", "I prodotti più venduti negli ultimi 3 mesi"),
    ];

    private readonly List<(Rectangle Area, Suggerimento Voce)> carte = [];
    private int sopra = -1;
    private string nomeDatabase = string.Empty;
    private string privacy = string.Empty;

    public PannelloBenvenuto()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
    }

    /// <summary>Testo del suggerimento scelto dall'utente.</summary>
    public event EventHandler<string>? SuggerimentoScelto;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string NomeDatabase { get => nomeDatabase; set { nomeDatabase = value ?? string.Empty; Invalidate(); } }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Privacy { get => privacy; set { privacy = value ?? string.Empty; Invalidate(); } }

    private static string Nome
    {
        get
        {
            var n = Environment.UserName.Split(['.', ' ', '_'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return n.Length == 0 ? "" : char.ToUpperInvariant(n[0]) + n[1..].ToLowerInvariant();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Tema.DipingiSfondoGenitore(this, g);
        Tema.Qualita(g);
        carte.Clear();

        var colonna = Math.Min(780, Width - 48);
        var due = colonna >= 520;
        var altezzaCarta = 78;
        var righeCarte = due ? 2 : 4;
        var altezza = 96 + 22 + 50 + 60 + 50 + 30 + righeCarte * (altezzaCarta + 12);
        var x0 = (Width - colonna) / 2;
        var y = Math.Max(16, (Height - altezza) / 2);

        // Logo
        Tema.Ombra(g, new RectangleF(Width / 2f - 44, y, 88, 88), 22, 14);
        Tema.DisegnaLogo(g, new RectangleF(Width / 2f - 44, y, 88, 88));
        y += 110;

        // Titolo: "Ciao Nome, " + "cosa vuoi sapere?" a gradiente
        var font = Tema.Titolo(22f);
        var saluto = Nome.Length > 0 ? $"Ciao {Nome}, " : "Ciao, ";
        var domanda = "cosa vuoi sapere?";
        using var formato = (StringFormat)StringFormat.GenericTypographic.Clone();
        formato.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        var l1 = g.MeasureString(saluto, font, PointF.Empty, formato).Width;
        var l2 = g.MeasureString(domanda, font, PointF.Empty, formato).Width;
        var xt = (Width - l1 - l2) / 2f;
        using (var b = new SolidBrush(Tema.Testo))
            g.DrawString(saluto, font, b, xt, y, formato);
        Tema.TestoGradiente(g, domanda, font, new PointF(xt + l1, y));
        y += 50;

        // Sottotitolo
        var sotto = $"Chiedi in italiano qualsiasi cosa sui dati di {nomeDatabase}: rispondo con tabelle e grafici.";
        var rs = new Rectangle(x0, y, colonna, 46);
        TextRenderer.DrawText(g, sotto, Tema.Carattere(11f), rs, Tema.TestoSecondario,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        y += 52;

        // Pillola riservatezza
        if (privacy.Length > 0)
        {
            var fp = Tema.Semigrassetto(9f);
            var mp = TextRenderer.MeasureText(g, privacy, fp, Size.Empty, TextFormatFlags.NoPadding);
            var larghezza = Math.Min(colonna, mp.Width + 52);
            var pill = new Rectangle((Width - larghezza) / 2, y, larghezza, 34);
            using (var forma = Tema.Arrotondato(pill, 17))
            using (var b = new SolidBrush(Color.FromArgb(222, 247, 239)))
                g.FillPath(b, forma);
            TextRenderer.DrawText(g, Tema.Icona.Lucchetto, Tema.Icone(10f), new Rectangle(pill.X + 16, pill.Y, 18, pill.Height), Tema.VerdeScuro,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, privacy, fp, new Rectangle(pill.X + 38, pill.Y, pill.Width - 46, pill.Height), Tema.VerdeScuro,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            y += 58;
        }

        // Suggerimenti
        var larghezzaCarta = due ? (colonna - 12) / 2 : colonna;
        for (var i = 0; i < Suggerimenti.Length; i++)
        {
            var riga = due ? i / 2 : i;
            var col = due ? i % 2 : 0;
            var area = new Rectangle(x0 + col * (larghezzaCarta + 12), y + riga * (altezzaCarta + 12), larghezzaCarta, altezzaCarta);
            carte.Add((area, Suggerimenti[i]));
            DisegnaCarta(g, area, Suggerimenti[i], i == sopra);
        }
    }

    private static void DisegnaCarta(Graphics g, Rectangle area, Suggerimento s, bool evidenziata)
    {
        RectangleF r = area;
        if (evidenziata)
            r.Offset(0, -2);
        Tema.Ombra(g, r, 16, evidenziata ? 18 : 9);
        using (var forma = Tema.Arrotondato(r, 16))
        {
            g.FillPath(Brushes.White, forma);
            using var p = new Pen(evidenziata ? Tema.Viola : Tema.Bordo, evidenziata ? 1.5f : 1f);
            g.DrawPath(p, forma);
        }
        var bolla = new Rectangle((int)r.X + 14, (int)r.Y + 14, 36, 36);
        using (var forma = Tema.Arrotondato(bolla, 11))
        using (var b = new LinearGradientBrush(bolla, s.Colore, ControlPaint.Light(s.Colore, 0.45f), 45f))
            g.FillPath(b, forma);
        TextRenderer.DrawText(g, s.Icona, Tema.Icone(12f), bolla, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        var x = bolla.Right + 12;
        TextRenderer.DrawText(g, s.Titolo, Tema.Semigrassetto(10f), new Rectangle(x, (int)r.Y + 12, (int)r.Right - x - 12, 20),
            Tema.Testo, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, s.Testo, Tema.Carattere(9f), new Rectangle(x, (int)r.Y + 33, (int)r.Right - x - 12, 38),
            Tema.TestoSecondario, TextFormatFlags.NoPadding | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var indice = carte.FindIndex(c => c.Area.Contains(e.Location));
        if (indice == sopra)
            return;
        sopra = indice;
        Cursor = indice >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (sopra < 0)
            return;
        sopra = -1;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var carta = carte.FirstOrDefault(c => c.Area.Contains(e.Location));
        if (carta.Voce is not null)
            SuggerimentoScelto?.Invoke(this, carta.Voce.Testo);
    }
}

/// <summary>Voce del menu laterale: icona e testo, evidenziata in viola con la barra a gradiente quando è selezionata.</summary>
public class VoceMenu : Control
{
    private string icona = string.Empty;
    private bool selezionata, pericolo, sopra;

    public VoceMenu()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    /// <summary>Icona Segoe MDL2 Assets.</summary>
    [DefaultValue(""), Category("GpOne")]
    public string Icona { get => icona; set { icona = value ?? string.Empty; Invalidate(); } }

    [DefaultValue(false), Category("GpOne")]
    public bool Selezionata { get => selezionata; set { selezionata = value; Invalidate(); } }

    /// <summary>Azione distruttiva (es. Elimina): testo rosso.</summary>
    [DefaultValue(false), Category("GpOne")]
    public bool Pericolo { get => pericolo; set { pericolo = value; Invalidate(); } }

    protected override Size DefaultSize => new(240, 46);

    protected override void OnMouseEnter(EventArgs e) { sopra = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { sopra = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Color.White);
        Tema.Qualita(g);
        var r = new RectangleF(10, 3, Width - 20, Height - 6);

        if (selezionata || (sopra && Enabled))
        {
            using var forma = Tema.Arrotondato(r, 12);
            using var b = new SolidBrush(selezionata ? Tema.ViolaChiaro : pericolo ? Tema.ErroreSfondo : Tema.SuperficieAlta);
            g.FillPath(b, forma);
        }
        if (selezionata)
        {
            var barra = new RectangleF(r.X, r.Y + 9, 4, r.Height - 18);
            using var forma = Tema.Arrotondato(barra, 2);
            using var b = Tema.Gradiente(barra);
            g.FillPath(b, forma);
        }
        if (Focused && ShowFocusCues)
        {
            using var forma = Tema.Arrotondato(RectangleF.Inflate(r, -1, -1), 11);
            using var p = new Pen(Color.FromArgb(150, Tema.Viola), 1.5f);
            g.DrawPath(p, forma);
        }

        var colore = !Enabled ? Color.FromArgb(150, Tema.TestoSecondario)
            : selezionata ? Tema.ViolaScuro
            : pericolo ? Tema.RosaScuro
            : Tema.Testo;
        var coloreIcona = !Enabled ? colore : selezionata ? Tema.Viola : pericolo ? Tema.Rosa : Tema.TestoSecondario;
        if (icona.Length > 0)
            TextRenderer.DrawText(g, icona, Tema.Icone(12f), new Rectangle((int)r.X + 16, 0, 22, Height), coloreIcona,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, Text, selezionata ? Tema.Semigrassetto(10f) : Tema.Carattere(10f),
            new Rectangle((int)r.X + 50, 0, (int)r.Width - 56, Height), colore,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }
}
