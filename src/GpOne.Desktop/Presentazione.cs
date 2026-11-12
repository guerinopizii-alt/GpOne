using System.Data;
using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using GpOne.Core;
using GpOne.Motore;
using ScottPlot;
using ScottPlot.WinForms;
using Color = ScottPlot.Color;

namespace GpOne.Desktop;

/// <summary>Tabella, grafico ed export dei risultati: tutto in locale, i valori non lasciano mai il PC.</summary>
public static class Presentazione
{
    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");
    private static readonly string[] Interi = ["int", "bigint", "smallint", "tinyint"];
    private static readonly string[] Decimali = ["decimal", "numeric", "money", "smallmoney", "float", "real"];
    private static readonly string[] Date = ["date", "datetime", "datetime2", "smalldatetime", "datetimeoffset"];

    /// <summary>Colori GpOne, come nel grafico del web.</summary>
    private static readonly string[] Palette = ["#7c4dff", "#ff4f8b", "#ffa53d", "#22c3ee", "#2dd4a7", "#a77bff", "#ff86b0", "#ffc061"];

    private static bool Di(string tipo, string[] tipi) => tipi.Contains(tipo, StringComparer.OrdinalIgnoreCase);

    /// <summary>Valore tipizzato (numeri, date) per ordinare e formattare correttamente.</summary>
    private static object? Valore(object? valore, string tipo) => valore switch
    {
        null => null,
        string s when Di(tipo, Date) && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) => d,
        _ => valore
    };

    // ---- Tabella ----

    public static void MostraTabella(DataGridView griglia, IReadOnlyList<ColonnaRisultato> colonne, IReadOnlyList<object?[]> righe)
    {
        var tabella = new DataTable();
        for (var c = 0; c < colonne.Count; c++)
        {
            var tipo = colonne[c].Tipo;
            var tipoNet = Di(tipo, Interi) ? typeof(long)
                : Di(tipo, Decimali) ? typeof(decimal)
                : Di(tipo, Date) ? typeof(DateTime)
                : tipo.Equals("bit", StringComparison.OrdinalIgnoreCase) ? typeof(bool)
                : typeof(string);
            // Nomi di colonna duplicati (es. due "Totale") non sono ammessi in un DataTable
            var nome = colonne[c].Nome;
            while (tabella.Columns.Contains(nome))
                nome += "_";
            tabella.Columns.Add(nome, tipoNet).Caption = colonne[c].Nome;
        }

        tabella.BeginLoadData();
        foreach (var riga in righe)
        {
            var valori = new object[colonne.Count];
            for (var c = 0; c < colonne.Count; c++)
            {
                var v = Valore(riga[c], colonne[c].Tipo);
                var tipoColonna = tabella.Columns[c].DataType;
                try
                {
                    valori[c] = v is null ? DBNull.Value
                        : tipoColonna == typeof(string) ? Convert.ToString(v, Italiano)!
                        : Convert.ChangeType(v, tipoColonna, CultureInfo.InvariantCulture);
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
                {
                    valori[c] = DBNull.Value;
                }
            }
            tabella.Rows.Add(valori);
        }
        tabella.EndLoadData();

        griglia.DataSource = null;
        griglia.Columns.Clear();
        griglia.DataSource = tabella;
        for (var c = 0; c < griglia.Columns.Count && c < colonne.Count; c++)
        {
            var colonna = griglia.Columns[c];
            colonna.HeaderText = colonne[c].Nome;
            var tipo = colonne[c].Tipo;
            if (Di(tipo, Interi))
                colonna.DefaultCellStyle.Format = "N0";
            else if (Di(tipo, Decimali))
                colonna.DefaultCellStyle.Format = "N2";
            else if (tipo.Equals("date", StringComparison.OrdinalIgnoreCase))
                colonna.DefaultCellStyle.Format = "dd/MM/yyyy";
            else if (Di(tipo, Date))
                colonna.DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
            if (Di(tipo, Interi) || Di(tipo, Decimali))
                colonna.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colonna.DefaultCellStyle.FormatProvider = Italiano;
        }
        griglia.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        griglia.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader);
        foreach (DataGridViewColumn colonna in griglia.Columns)
            colonna.Width = Math.Max(colonna.Width + 24, TextRenderer.MeasureText(colonna.HeaderText, griglia.ColumnHeadersDefaultCellStyle.Font).Width + 40);
        // Poche colonne: occupano tutta la larghezza, come la tabella del web
        if (griglia.Columns.Cast<DataGridViewColumn>().Sum(c => c.Width) < griglia.ClientSize.Width)
            griglia.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        griglia.ClearSelection();
        griglia.CurrentCell = null;
    }

    /// <summary>Stile GpOne del grafico: sfondo bianco, griglia leggera, niente cornici scure.</summary>
    private static void Stile(Plot plot)
    {
        plot.Font.Set("Segoe UI");
        plot.FigureBackground.Color = Colors.White;
        plot.DataBackground.Color = Colors.White;
        plot.Axes.Color(Color.FromHex("#686480"));
        plot.Axes.Top.FrameLineStyle.Width = 0;
        plot.Axes.Right.FrameLineStyle.Width = 0;
        plot.Axes.Left.FrameLineStyle.Color = Color.FromHex("#e4def2");
        plot.Axes.Bottom.FrameLineStyle.Color = Color.FromHex("#e4def2");
        plot.Axes.Left.MajorTickStyle.Length = 0;
        plot.Axes.Left.MinorTickStyle.Length = 0;
        plot.Axes.Bottom.MajorTickStyle.Length = 0;
        plot.Axes.Bottom.MinorTickStyle.Length = 0;
        plot.Grid.MajorLineColor = Color.FromHex("#eeeaf7");
        plot.Grid.XAxisStyle.MajorLineStyle.Width = 0;
        plot.Legend.OutlineWidth = 0;
        plot.Legend.ShadowColor = Colors.Transparent;
        plot.Legend.FontColor = Color.FromHex("#1c1b29");
    }

    // ---- Grafico ----

    /// <summary>Disegna il grafico proposto dall'AI (che ha scelto tipo e colonne senza vedere i dati). False se non disegnabile.</summary>
    public static bool MostraGrafico(FormsPlot area, GraficoProposto grafico, IReadOnlyList<ColonnaRisultato> colonne, IReadOnlyList<object?[]> righe)
    {
        // Grafico nuovo a ogni risposta: la torta nasconde assi e griglia
        area.Reset();
        var plot = area.Plot;
        Stile(plot);

        int Indice(string nome) => colonne.ToList().FindIndex(c => c.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase));
        var ix = Indice(grafico.X);
        var serie = grafico.Y.Select(y => (Nome: y, I: Indice(y))).Where(s => s.I >= 0).ToList();
        if (ix < 0 || serie.Count == 0 || righe.Count == 0)
        {
            area.Refresh();
            return false;
        }

        var etichette = righe.Select(r => Convert.ToString(Valore(r[ix], colonne[ix].Tipo) is DateTime d ? d.ToString("dd/MM/yyyy", Italiano) : r[ix], Italiano) ?? "").ToArray();
        double Numero(object? v) => v switch
        {
            null => 0,
            IConvertible c => double.TryParse(Convert.ToString(c, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0,
            _ => 0
        };
        Color Colore(int n) => Color.FromHex(Palette[n % Palette.Length]);
        var posizioni = Enumerable.Range(0, righe.Count).Select(i => (double)i).ToArray();

        switch (grafico.Tipo)
        {
            case "torta":
            {
                var valori = righe.Select(r => Numero(r[serie[0].I])).ToArray();
                var fette = valori.Select((v, k) => new PieSlice { Value = v, FillColor = Colore(k), LegendText = etichette[k] }).ToList();
                plot.Add.Pie(fette);
                plot.Axes.Frameless();
                plot.HideGrid();
                plot.ShowLegend(Edge.Right);
                break;
            }
            case "linee":
                for (var n = 0; n < serie.Count; n++)
                {
                    var linea = plot.Add.Scatter(posizioni, righe.Select(r => Numero(r[serie[n].I])).ToArray());
                    linea.LegendText = colonne[serie[n].I].Nome;
                    linea.Color = Colore(n);
                    linea.LineWidth = 3;
                    linea.MarkerSize = 6;
                }
                ImpostaEtichette(plot, posizioni, etichette);
                plot.ShowLegend(Edge.Bottom);
                break;
            default:
            {
                // Barre affiancate: una serie per colonna Y
                var larghezza = 0.8 / serie.Count;
                for (var n = 0; n < serie.Count; n++)
                {
                    var barre = righe.Select((r, i) => new Bar
                    {
                        Position = i - 0.4 + larghezza * (n + 0.5),
                        Value = Numero(r[serie[n].I]),
                        Size = larghezza * 0.75,
                        FillColor = Colore(n),
                        LineWidth = 0
                    }).ToList();
                    var gruppo = plot.Add.Bars(barre);
                    gruppo.LegendText = colonne[serie[n].I].Nome;
                }
                ImpostaEtichette(plot, posizioni, etichette);
                plot.Axes.Margins(bottom: 0);
                plot.ShowLegend(Edge.Bottom);
                break;
            }
        }

        area.Refresh();
        return true;
    }

    private static void ImpostaEtichette(Plot plot, double[] posizioni, string[] etichette)
    {
        plot.Axes.Bottom.SetTicks(posizioni, etichette);
        if (etichette.Length > 8 || etichette.Any(e => e.Length > 12))
        {
            plot.Axes.Bottom.TickLabelStyle.Rotation = -45;
            plot.Axes.Bottom.TickLabelStyle.Alignment = Alignment.MiddleRight;
        }
    }

    // ---- Export ----

    public static void EsportaExcel(string percorso, string domanda, IReadOnlyList<ColonnaRisultato> colonne, IReadOnlyList<object?[]> righe)
    {
        using var workbook = new XLWorkbook();
        var foglio = workbook.Worksheets.Add("Risultato");
        for (var c = 0; c < colonne.Count; c++)
            foglio.Cell(1, c + 1).Value = colonne[c].Nome;
        for (var r = 0; r < righe.Count; r++)
            for (var c = 0; c < colonne.Count; c++)
                foglio.Cell(r + 2, c + 1).Value = Valore(righe[r][c], colonne[c].Tipo) switch
                {
                    null => Blank.Value,
                    DateTime d => d,
                    bool b => b,
                    string s => s,
                    IConvertible n and not char => Convert.ToDecimal(n, CultureInfo.InvariantCulture),
                    var v => v.ToString()
                };
        foglio.Row(1).Style.Font.Bold = true;
        foglio.Columns().AdjustToContents(1, Math.Min(righe.Count + 1, 200));
        workbook.Worksheets.Add("Domanda").Cell(1, 1).Value = domanda;
        workbook.SaveAs(percorso);
    }

    public static void EsportaCsv(string percorso, IReadOnlyList<ColonnaRisultato> colonne, IReadOnlyList<object?[]> righe)
    {
        static string Campo(string? s) => s is null ? "" : s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', colonne.Select(c => Campo(c.Nome))));
        foreach (var riga in righe)
            sb.AppendLine(string.Join(';', riga.Select(v => Campo(v is IFormattable f ? f.ToString(null, Italiano) : v?.ToString()))));
        File.WriteAllText(percorso, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}
