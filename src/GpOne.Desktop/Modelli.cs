
using GpOne.Core;
using GpOne.Motore;

namespace GpOne.Desktop;

/// <summary>Risposta mostrata all'utente: i risultati restano solo in memoria (non vengono salvati né mandati all'AI).</summary>
public sealed record RispostaDomanda(
    long Id,
    string Domanda,
    string? Sql,
    string Spiegazione,
    GraficoProposto? Grafico,
    RisultatoQuery? Risultato,
    int DurataMs,
    string? Errore,
    string? Valutazione,
    string? Chiarimento = null,
    bool DatiInviati = false)
{
    public IReadOnlyList<ColonnaRisultato> Colonne => Risultato?.Colonne ?? [];
    public IReadOnlyList<object?[]> Righe => Risultato?.Righe ?? [];
    public bool Troncato => Risultato?.Troncato ?? false;
    public bool Confermabile => Sql is not null && Errore is null && Risultato is not null;
}

/// <summary>Un passaggio della conversazione: la domanda (o la correzione) e la risposta di GpOne.</summary>
public sealed class Turno(string domanda, string? correzione = null) : IVoceElenco
{
    public string Titolo => Correzione switch
    {
        null => Domanda,
        var c when c.StartsWith("Chiarimento: ", StringComparison.Ordinal) => c,
        var c => $"Correzione: {c}"
    };

    public string Dettaglio =>
        InAttesa ? "In corso…"
        : Errore is not null || Risposta?.Errore is not null ? "Non riuscita"
        : Risposta?.Chiarimento is not null ? "L'AI chiede un chiarimento"
        : Risposta?.Valutazione == Valutazioni.Corretta ? $"Confermata · {Risposta.Righe.Count:N0} righe"
        : Risposta is { Sql: not null } r ? $"{r.Righe.Count:N0} righe · {r.DurataMs / 1000.0:N1} s"
        : "Nessuna query";

    public string Icona =>
        InAttesa ? Tema.Icona.Tempo
        : Errore is not null || Risposta?.Errore is not null ? Tema.Icona.Attenzione
        : Risposta?.Chiarimento is not null ? Tema.Icona.Domanda
        : Risposta?.Valutazione == Valutazioni.Corretta ? Tema.Icona.Conferma
        : Correzione is not null ? Tema.Icona.Modifica
        : Tema.Icona.Chat;

    public Color Colore =>
        InAttesa ? Tema.Arancio
        : Errore is not null || Risposta?.Errore is not null ? Tema.Rosa
        : Risposta?.Chiarimento is not null ? Tema.Ciano
        : Risposta?.Valutazione == Valutazioni.Corretta ? Tema.Verde
        : Tema.Viola;

    public string Domanda { get; } = domanda;
    public string? Correzione { get; } = correzione;
    public RispostaDomanda? Risposta { get; set; }
    public string? Errore { get; set; }
    /// <summary>Eccezione completa (tipo, cause, stack), copiata insieme al messaggio con il pulsante Copia.</summary>
    public string? ErroreDettagli { get; set; }
    public bool InAttesa => Risposta is null && Errore is null;

    public override string ToString()
    {
        var stato = InAttesa ? "⏳ " : Errore is not null || Risposta?.Errore is not null ? "⚠ "
            : Risposta?.Valutazione == Valutazioni.Corretta ? "✔ " : "";
        return stato + (Correzione is null ? Domanda : $"Correzione: {Correzione}");
    }
}
